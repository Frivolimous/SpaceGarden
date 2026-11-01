namespace SpaceGarden.Core.Tests;

// A smart, attentive player, for pacing runs (DESIGN.md: "simulations assume a smart player who is actively tending"):
// the plant is stupid, so this is the brain. Each check-in it
//   - spends its genes on the cheapest next upgrade it can afford, again and again, saving for a generator when short
//     of power or for the next core upgrade when it costs at most twice the next stem (Research);
//   - plants a seedling in every free seedling slot and picks each seedling's evolution at random (Seedlings);
//   - prunes wild flowers that don't pay for their link (PruneWild);
//   - prunes passive groves past the ones it wants (the farthest first);
//   - prunes drains: while the garden would cost more than it makes at full power, the lab furthest from the core
//     goes (passive ones are usually far out), then any lab too far out for its genes to find the core;
//   - makes room: when the stem or generator cap has room but no free link, prunes the farthest flowers (MakeRoom);
//   - fills the stem cap with stems, nearest the core first;
//   - adds labs near the core (within MaxLabDepth) while the power to spare covers one, keeping a small reserve and
//     a free link for the plant to grow on;
//   - fills the generator cap with generators where the plant is hungriest (lowest on power);
//   - keeps a grove for every GroveEvery stems, planted mid-plant.
public static class SmartPlayer
{
    const int GroveEvery = 15;
    const float Reserve = 1; // power a second

    // Check-ins every minute for the first hour (a new player watches the garden start), then every two hours while
    // awake: planted at 9:00, asleep from 1:00 to 9:00.
    public const double CheckHours = 2;
    public static bool Awake(double hours) => hours % 24 < 16;
    public static bool CheckIn(double seconds) => (long)Math.Round(seconds) is var s
        && (s < 3600 ? s % 60 == 0 : s % (long)(CheckHours * 3600) == 0 && Awake(s / 3600.0));

    public static void Tend(Garden garden)
    {
        Research(garden);
        PruneDrains(garden);
        MakeRoom(garden);
        // The opening: the first grove at once, and the first lab as soon as a part has a link to spare beside it.
        if (Count(garden, NodeKind.Grove) == 0 && !Forming(garden, NodeKind.Grove)) Bud(garden, NodeKind.Grove, nearFirst: true);
        if (Count(garden, NodeKind.Lab) == 0 && !Forming(garden, NodeKind.Lab) && FreeLinks(garden) > 1)
            Bud(garden, NodeKind.Lab, nearFirst: true, maxDepth: MaxLabDepth);
        Seedlings(garden);
        while (Bud(garden, NodeKind.Stem, nearFirst: true)) { }
        while (Spare(garden) >= -NodeSpecs.All[NodeKind.Lab].PowerPerSecond + Reserve && FreeLinks(garden) > 1
            && Bud(garden, NodeKind.Lab, nearFirst: true, maxDepth: MaxLabDepth)) { }
        while (Bud(garden, NodeKind.Generator, nearFirst: false, hungriest: true)) { }
        while (garden.Nodes.Count(IsGrove) < GrovesWanted(garden) && Bud(garden, NodeKind.Grove, nearFirst: false, middle: true)) { }
        // The player doesn't undo, and the 30 s undo window passes (counted in physics ticks, which a pacing run
        // doesn't run; left open, it would hold off passive buds for good).
        garden.ForgetPrune();
    }

    static int GrovesWanted(Garden garden) => 1 + Count(garden, NodeKind.Stem) / GroveEvery;

    // Spends the gene balance. While the garden is short of power (it couldn't keep another lab full) and every
    // generator it may have is grown, the next generator comes first: bought if the balance covers it, saved for if
    // not (more stems would only cost more upkeep). Otherwise the cheapest next upgrade across the three tracks, while
    // the balance covers it: the cheap early stems first, then generators and the core's track by turns as their costs
    // climb past the stems'. But when the next core upgrade costs at most twice the next stem, it saves for that
    // instead (Jeremy, Nov 1), so the core track isn't left behind the stems. (Each seedling slot
    // bought is planted at the next check-in: see Seedlings.)
    static void Research(Garden garden)
    {
        while (true)
        {
            bool shortOfPower = Spare(garden) < -NodeSpecs.All[NodeKind.Lab].PowerPerSecond + Reserve
                && Count(garden, NodeKind.Generator) >= garden.Capacity.Generators;
            var core = garden.Next(Track.Core);
            var next = shortOfPower && garden.Next(Track.Generators) is { } generator ? generator
                : core != null && garden.Next(Track.Stems) is { } stem && core.Cost <= 2 * stem.Cost ? core
                : garden.Cheapest;
            if (next == null || !garden.Buy(next.Track)) return;
        }
    }

    // Every free seedling slot gets a seedling, as near the core as there's room (its neighbours there are fullest, so it
    // fills fastest), and every seedling waiting for its choice takes one of its offers at random (from the garden's
    // own dice: the seedling and the evolutions so far, so a run repeats).
    static void Seedlings(Garden garden)
    {
        // Only once the first lab is out, and never on the last free link (it would end the plant's growth there).
        while (garden.FreeSeedlingSlots > 0 && Count(garden, NodeKind.Lab) > 0 && FreeLinks(garden) > 1
            && Bud(garden, NodeKind.Seedling, nearFirst: true)) { }
        foreach (var seedling in garden.Nodes.Where(n => n.Choosing).ToList())
        {
            var offers = seedling.Offers!.ToList();
            var dice = new Random(seedling.Id * 7919 + garden.Evolved.Count);
            while (offers.Count > 0)
            {
                var pick = offers[dice.Next(offers.Count)];
                if (garden.Choose(seedling, pick)) break;
                offers.Remove(pick);
            }
        }
    }

    // Labs deeper than this lose most of their gene blobs on the way (they wander), so they aren't worth their upkeep.
    const int MaxLabDepth = 4;

    static int FreeLinks(Garden garden) => garden.Nodes.Count(n => !n.Spec.IsFruit && n.CanBranch);

    // While the stem or generator cap has room but no link can take one (passive flowers took them all), the farthest
    // flower off a stem goes, a lab before a grove, always keeping one of each: its link then takes a stem or generator.
    static void MakeRoom(Garden garden)
    {
        while ((NeedsRoom(garden, NodeKind.Stem, garden.Capacity.Stems)
                || NeedsRoom(garden, NodeKind.Generator, garden.Capacity.Generators))
            && garden.Nodes
                .Where(n => n.Spec.Kind is NodeKind.Lab or NodeKind.Grove && n.Parent!.Spec.Kind == NodeKind.Stem
                    && Count(garden, n.Spec.Kind) > 1)
                .OrderBy(n => n.Spec.Kind == NodeKind.Lab ? 0 : 1).ThenByDescending(n => n.Depth)
                .FirstOrDefault() is { } flower)
            garden.Prune(flower);
    }

    static bool NeedsRoom(Garden garden, NodeKind kind, int cap) => Count(garden, kind) < cap
        && !garden.Nodes.Any(n => !n.Spec.IsFruit && n.Parent != null && garden.CanSproutFrom(n, kind));

    static int Count(Garden garden, NodeKind kind) => garden.Nodes.Count(n => n.Spec.Kind == kind);
    static bool Forming(Garden garden, NodeKind kind) => garden.Nodes.Any(n => n.Forming == kind);

    // What the garden would make at full power, less what all its parts would cost: below 0 it's overloaded, and
    // settles lower, its far labs lowest and slowest.
    static float Spare(Garden garden) => garden.FullPowerBalance() is var (made, used) ? made - used : 0;

    static void PruneDrains(Garden garden)
    {
        PruneWild(garden);
        // Groves past the one-per-GroveEvery-stems the garden needs only drain it and take slots: plain groves go before
        // the wild ones (a Seed Pod, then a Lumber Grove last), the farthest first.
        while (garden.Nodes.Count(IsGrove) > GrovesWanted(garden)
            && garden.Nodes.Where(IsGrove).OrderBy(n => GroveRank(n.Spec.Kind)).ThenByDescending(n => n.Depth).FirstOrDefault()
                is { } grove)
            garden.Prune(grove);
        while (Spare(garden) < 0 && FarthestLab(garden, _ => true) is { } lab) garden.Prune(lab);
        while (FarthestLab(garden, n => n.Depth > MaxLabDepth) is { } lab) garden.Prune(lab);
    }

    static PlantNode? FarthestLab(Garden garden, Func<PlantNode, bool> which) => garden.Nodes
        .Where(n => IsLab(n) && which(n)).MaxBy(n => n.Depth);

    // Wild Labs and Lichens are labs and Lumber Groves and Seed Pods groves, to the player's rules.
    static bool IsLab(PlantNode n) => n.Spec.Kind is NodeKind.Lab or NodeKind.WildLab or NodeKind.Lichen;
    static bool IsGrove(PlantNode n) => n.Spec.Kind is NodeKind.Grove or NodeKind.SeedPod or NodeKind.LumberGrove;
    static int GroveRank(NodeKind kind) => kind switch { NodeKind.Grove => 0, NodeKind.SeedPod => 1, _ => 2 };

    // Wild flowers (passive, or bloomed from a seedling: pruning one takes only the flower, never its effect) that don't
    // pay for their link go, and a new one may grow elsewhere in the freed slot (a reroll, which the player wants):
    //   - a Resistor (it bounces blobs, genes too), a Moonflower, a Nebula Bloom or a Buffer, always;
    //   - an Amplifier too far out for genes to pass it on their way to the core (past MaxLabDepth);
    //   - an Ember once it has faded below a generator (halfway through its life), so a generator takes its slot.
    // Only at a branch's end: an Amplifier or Resistor with parts grown past it would take them all with it.
    // Wild Labs and Lichens follow the labs' rules, Lumber Groves and Seed Pods the groves'; Pulsars, Twin Stars,
    // Tendrils, Big Stems and Comet Blooms (which wither on their own) stay.
    static void PruneWild(Garden garden)
    {
        foreach (var flower in garden.Nodes.Where(n => n.Branches.Count == 0 && n.Spec.Kind switch
            {
                NodeKind.Resistor or NodeKind.Moonflower or NodeKind.NebulaBloom or NodeKind.Buffer => true,
                NodeKind.Amplifier => n.Depth > MaxLabDepth,
                NodeKind.Ember => n.Age > Tuning.EmberLifeSeconds / 2,
                _ => false,
            }).ToList())
            if (garden.Nodes.Contains(flower)) garden.Prune(flower);
    }

    // A bud of this kind at a free link: the nearest the core, the furthest, or the one in the middle (by depth).
    // Only stems grow on the core. Returns whether one started (it forms, then emerges).
    static bool Bud(Garden garden, NodeKind kind, bool nearFirst, bool middle = false, int maxDepth = int.MaxValue,
        bool hungriest = false)
    {
        var spots = garden.Nodes
            .Where(n => !n.Spec.IsFruit && (n.Parent != null || kind == NodeKind.Stem) && n.Depth < maxDepth
                && garden.CanSproutFrom(n, kind))
            .OrderBy(n => hungriest ? n.Fill : 0)
            .ThenBy(n => nearFirst ? n.Depth : -n.Depth).ThenBy(n => n.Id).ToList();
        if (spots.Count == 0) return false;
        var spot = middle ? spots.OrderBy(n => n.Depth).ElementAt(spots.Count / 2) : spots[0];
        return garden.SproutFrom(spot, kind);
    }
}
