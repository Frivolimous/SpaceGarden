namespace SpaceGarden.Core;

// What the garden card tells a player tending the garden (DESIGN.md, Information display): a snapshot of its power,
// its caps, its flowers, what's forming, its genes to spend and how fast more reach the core. Worked out from the
// garden as it is now; it changes nothing. Seedlings counts the seedlings growing, and SeedlingPower and SeedlingStore
// sum their power and stores (kept apart from PowerHeld and PowerStore: their power goes to their evolutions).
public sealed record GardenStats(
    float PowerHeld, float PowerStore, float Production, float Upkeep, float FullProduction, float FullUpkeep,
    int Stems, int Generators, int Groves, int Labs, int CoreLinksUsed, Capacity Capacity,
    int Fruit, int FruitRoom, IReadOnlyDictionary<NodeKind, int> BudsForming,
    double Genes, double GeneBalance, double GenesPerHour, Upgrade? Cheapest, int WildFlowers = 0,
    int Seedlings = 0, float SeedlingPower = 0, float SeedlingStore = 0)
{
    public float PowerFill => PowerStore > 0 ? PowerHeld / PowerStore : 0;

    // The garden costs more when full than it can make when full, so it can't stay full: it settles lower, its far
    // flowers lowest and working slowest. (Production and upkeep now both follow the fills, so they always meet.)
    public bool Overloaded => FullUpkeep > FullProduction;

    // Genes the balance still lacks for the cheapest next upgrade, and how long they take at the current rate, in
    // growth seconds (null: every upgrade bought, or no genes on their way; 0: it can be bought now).
    public double GenesToNext => Cheapest is { } next ? Math.Max(0, next.Cost - GeneBalance) : 0;
    public double? SecondsToNext => Cheapest == null ? null : GenesToNext <= 0 ? 0
        : GenesPerHour <= 0 ? null : GenesToNext / GenesPerHour * 3600;

    public static GardenStats Of(Garden garden)
    {
        // Per second. A full core or generator makes nothing just then, but counts at its full rate, as on its card.
        var (_, upkeep) = garden.PowerBalance();
        float production = garden.Nodes.Sum(Garden.ShownProductionPerSecond);
        var (fullProduction, fullUpkeep) = garden.FullPowerBalance();
        float held = 0, store = 0;
        int stems = 0, generators = 0, groves = 0, labs = 0, fruit = 0, fruitRoom = 0;
        int seedlings = 0;
        float seedlingPower = 0, seedlingStore = 0;
        double genesPerHour = 0;
        var buds = new Dictionary<NodeKind, int>();
        foreach (var n in garden.Nodes)
        {
            if (n.Spec.IsFruit)
            {
                fruit++;
                continue;
            }
            if (n.IsSeedling)
            {
                seedlings++;
                seedlingPower += n.Power;
                seedlingStore += n.Store;
            }
            if (n.HoldsGardenPower) // (a seedling's power goes to its evolution: counted apart)
            {
                held += n.Power;
                store += Garden.PowerCap(n); // (an Ember's falls as it dims)
            }
            fruitRoom += n.MaxFruits;
            if (n.FormingBud) buds[n.Forming!.Value] = buds.GetValueOrDefault(n.Forming!.Value) + 1;
            switch (n.Spec.Kind)
            {
                case NodeKind.Grove: groves++; break;
                case NodeKind.Lab: labs++; break;
            }
            // By the slot it takes: a Tendril or Big Stem counts as a stem, a Pulsar, Twin Star or Ember as a generator.
            switch (Garden.SlotOf(n.Spec.Kind))
            {
                case NodeKind.Stem: stems++; break;
                case NodeKind.Generator: generators++; break;
            }
            // Wild labs (a Wild Lab, Lichen) send genes too.
            if (n.Spec.Sends == BlobKind.Genes) genesPerHour += GenesPerHourFrom(garden, n);
        }
        return new GardenStats(held, store, production, upkeep, fullProduction, fullUpkeep, stems, generators, groves, labs,
            garden.Core.Branches.Count, garden.Caps, fruit, fruitRoom, buds, garden.Genes, garden.GeneBalance, genesPerHour,
            garden.Cheapest, garden.WildCount, seedlings, seedlingPower, seedlingStore);
    }

    // The genes a lab gets to the core per growth hour, on average: the blobs it makes now, times the chance each
    // finds the core.
    public static double GenesPerHourFrom(Garden garden, PlantNode lab) =>
        Garden.BlobsPerHour(lab) * Tuning.BlobGenes * GeneReach(garden, lab);

    // The chance a gene blob leaving this lab finds the core before it fades (DESIGN.md, Economy, Blobs): the blob's
    // walk (Garden.Walk) followed exactly, as odds instead of dice. At each part it takes any link but the one it came
    // by (that one too at a dead end), each alike, for up to BlobMaxHops links; never onto a Resistor, and with Smart
    // Particles, at their chance, never into a dead end but the core (see Garden.Wanders). Depends only on the plant's
    // shape (and the evolutions bloomed).
    public static double GeneReach(Garden garden, PlantNode lab) => GeneReaches(garden, lab).Core;

    // The genes the Hub catches per growth hour, on average (all the labs' blobs, times the chance each finds the Hub
    // first: GeneReaches), for when its next point comes (Jeremy, Nov 3).
    public static double HubGenesPerHour(Garden garden)
    {
        if (!garden.Nodes.Any(n => n.IsHub && garden.CatchesGenes(n))) return 0;
        return garden.Nodes.Where(n => n.Spec.Sends == BlobKind.Genes)
            .Sum(lab => Garden.BlobsPerHour(lab) * Tuning.BlobGenes * GeneReaches(garden, lab).Hub);
    }

    // The chances a gene blob leaving this lab ends at the core, and at a Hub (one catching genes), as GeneReach.
    public static (double Core, double Hub) GeneReaches(Garden garden, PlantNode lab)
    {
        var core = garden.Core;
        if (lab == core) return (1, 0);
        // The odds of being at a part, having come from another (null: still at the lab), after each hop.
        var now = new Dictionary<(PlantNode At, PlantNode? From), double> { [(lab, null)] = 1 };
        var next = new Dictionary<(PlantNode At, PlantNode? From), double>();
        double reached = 0, caught = 0;
        int hops = (int)MathF.Round(Tuning.BlobMaxHops * lab.Boost.Reach); // (further with the labs' Hub milestone)
        for (int hop = 0; hop < hops && now.Count > 0; hop++)
        {
            next.Clear();
            foreach (var ((at, from), odds) in now)
            {
                // Smart Particles steer SmartChance of the steps where they can; the rest go as before.
                if (garden.SmartAt(at, BlobKind.Genes))
                {
                    Spread(at, from, odds * garden.SmartChance, smart: true);
                    Spread(at, from, odds * (1 - garden.SmartChance), smart: false);
                }
                else Spread(at, from, odds, smart: false);
            }
            (now, next) = (next, now);
        }
        return (reached, caught);

        // These odds of being at a part, having come from another, spread over its next links.
        void Spread(PlantNode at, PlantNode? from, double odds, bool smart)
        {
            if (odds <= 0) return;
            int options = 0; // the links it could take
            foreach (var branch in at.Branches) if (garden.Wanders(at, branch, BlobKind.Genes, smart)) options++;
            if (at.Parent is { } parent && garden.Wanders(at, parent, BlobKind.Genes, smart)) options++;
            if (options == 0) return;
            // (As Garden.StepFrom: the way it came is skipped when there's another, if it's an option at all.)
            var skip = options > 1 && from != null && garden.Wanders(at, from, BlobKind.Genes, smart) ? from : null;
            double each = odds / (skip != null ? options - 1 : options);
            void Go(PlantNode to)
            {
                if (to == skip || !garden.Wanders(at, to, BlobKind.Genes, smart)) return; // (an Amplifier's doubling isn't counted)
                // (The Hub keeps what it catches, until every path is complete: then blobs pass through it.)
                if (to == core) reached += each;
                else if (garden.CatchesGenes(to)) caught += each; // (a Hub)
                else next[(to, at)] = next.GetValueOrDefault((to, at)) + each;
            }
            foreach (var branch in at.Branches) Go(branch);
            if (at.Parent != null) Go(at.Parent);
        }
    }
}

// What a part's card shows (DESIGN.md, Information display): its power against its store, and its rates now (they
// follow its fill), per growth second or hour; Sends is how fast it can send power on. Makes, Sends and Upkeep are
// power a second; GrowthBlobsPerHour is a grove's growth blobs, GenesPerHour the genes a lab's blobs carry as they
// leave (production, not what reaches the core: see GardenStats.GeneReach). Links counts the parent's, against
// MaxLinks (the core's is Capacity.CoreLinks). Forming is the fruit or bud forming in it, if any, and how far along
// it is, 0-1. A full core or generator makes nothing, so its card shows Makes at its full rate instead. A seedling's
// Evolution is the one chosen, if any (its Store is then the evolution's target). Store is the most it can hold now
// (Garden.PowerCap: an Ember's falls as it dims; Jeremy, Nov 3).
public sealed record NodeStats(
    float Power, float Store, float Makes, float Sends, float Upkeep, float GrowthBlobsPerHour, float GenesPerHour,
    int Links, int MaxLinks, NodeKind? Forming, float FormingProgress, Evolution? Evolution = null)
{
    public static NodeStats Of(PlantNode node)
    {
        var spec = node.Spec;
        float blobs = Garden.BlobsPerHour(node);
        float progress = node.Forming is { } kind
            ? Math.Clamp(node.Growth / (NodeSpecs.All[kind].IsFruit ? Tuning.FruitGrowth : Tuning.BudGrowth), 0, 1)
            : 0;
        return new NodeStats(node.Power, Garden.PowerCap(node),
            Garden.ShownProductionPerSecond(node), spec.IsFruit ? 0 : Garden.SendsPerSecond(node),
            Garden.UpkeepPerSecond(node),
            spec.Sends == BlobKind.Growth ? blobs : 0, spec.Sends == BlobKind.Genes ? blobs * Tuning.BlobGenes : 0,
            node.Links, node.MaxLinks, node.Forming, progress, node.Evolution);
    }
}
