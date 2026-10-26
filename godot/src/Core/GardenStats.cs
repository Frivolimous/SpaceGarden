namespace SpaceGarden.Core;

// What the hover readout tells a player tending the garden (DESIGN.md, Desktop widget): a snapshot of its power, its
// caps, its flowers, what's forming, and how fast genes reach the core. Worked out from the garden as it is now; it
// changes nothing.
public sealed record GardenStats(
    float PowerHeld, float PowerStore, float Production, float Upkeep, int Wilting,
    int Stems, int Generators, int Groves, int Labs, int CoreLinksUsed, Capacity Capacity,
    int Fruit, int FruitRoom, IReadOnlyDictionary<NodeKind, int> BudsForming,
    double Genes, double GenesPerHour, UnlockStep? NextUnlock)
{
    public float PowerFill => PowerStore > 0 ? PowerHeld / PowerStore : 0;

    // More upkeep than production: the garden is running down, and something wilts once the stores are empty.
    public bool Draining => Upkeep > Production;

    // Genes still needed for the next unlock, and how long they take at the current rate, in growth seconds (null:
    // nothing left to unlock, or no genes on their way).
    public double GenesToNext => NextUnlock is { } next ? Math.Max(0, next.Genes - Genes) : 0;
    public double? SecondsToNext => NextUnlock == null || GenesPerHour <= 0 ? null : GenesToNext / GenesPerHour * 3600;

    public static GardenStats Of(Garden garden)
    {
        var (production, upkeep) = garden.PowerBalance();
        float held = 0, store = 0;
        int wilting = 0, stems = 0, generators = 0, groves = 0, labs = 0, fruit = 0, fruitRoom = 0;
        double genesPerHour = 0;
        var buds = new Dictionary<NodeKind, int>();
        foreach (var n in garden.Nodes)
        {
            if (n.Spec.IsFruit)
            {
                fruit++;
                continue;
            }
            held += n.Power;
            store += n.Store;
            fruitRoom += n.Spec.MaxFruits;
            if (n.Wilting) wilting++;
            if (n.FormingBud) buds[n.Forming!.Value] = buds.GetValueOrDefault(n.Forming!.Value) + 1;
            switch (n.Spec.Kind)
            {
                case NodeKind.Stem: stems++; break;
                case NodeKind.Generator: generators++; break;
                case NodeKind.Grove: groves++; break;
                case NodeKind.Lab:
                    labs++;
                    genesPerHour += GenesPerHourFrom(garden, n);
                    break;
            }
        }
        return new GardenStats(held, store, production, upkeep, wilting, stems, generators, groves, labs,
            garden.Core.Branches.Count, garden.Capacity, fruit, fruitRoom, buds, garden.Genes, genesPerHour, garden.NextUnlock);
    }

    // The genes a lab gets to the core per growth hour, on average: the blobs it makes now, times the chance each
    // finds the core.
    public static double GenesPerHourFrom(Garden garden, PlantNode lab) =>
        Garden.BlobsPerHour(lab) * Tuning.BlobGenes * GeneReach(garden, lab);

    // The chance a gene blob leaving this lab finds the core before it fades (DESIGN.md, Economy, Blobs): the blob's
    // walk (Garden.Walk) followed exactly, as odds instead of dice. At each part it takes any link but the one it came
    // by (that one too at a dead end), each alike, for up to BlobMaxHops links. Depends only on the plant's shape.
    public static double GeneReach(Garden garden, PlantNode lab)
    {
        var core = garden.Core;
        if (lab == core) return 1;
        // The odds of being at a part, having come from another (null: still at the lab), after each hop.
        var now = new Dictionary<(PlantNode At, PlantNode? From), double> { [(lab, null)] = 1 };
        var next = new Dictionary<(PlantNode At, PlantNode? From), double>();
        double reached = 0;
        for (int hop = 0; hop < Tuning.BlobMaxHops && now.Count > 0; hop++)
        {
            next.Clear();
            foreach (var ((at, from), odds) in now)
            {
                int options = at.Branches.Count + (at.Parent != null ? 1 : 0);
                if (options == 0) continue;
                var skip = options > 1 ? from : null;
                double each = odds / (skip != null ? options - 1 : options);
                void Go(PlantNode to)
                {
                    if (to == skip) return;
                    if (to == core) reached += each;
                    else next[(to, at)] = next.GetValueOrDefault((to, at)) + each;
                }
                foreach (var branch in at.Branches) Go(branch);
                if (at.Parent != null) Go(at.Parent);
            }
            (now, next) = (next, now);
        }
        return reached;
    }
}
