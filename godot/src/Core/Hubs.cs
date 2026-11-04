namespace SpaceGarden.Core;

// What a Hub level raises on a kind, a share a level, adding up: the power it makes, the blobs it sends (genes, growth
// or buffs), how fast it sends power on, its upkeep (a negative share: less), its fruit slots or links (whole ones), or
// an Amplifier's chance to double a blob.
// (Jeremy, Nov 3: BuffStrength, every buff's bonus; Active, a Comet Bloom's active seconds; Moonlit, the Moonflower's
// bonus to wild flowers near it.)
public enum HubAxis { Gen, Blobs, Transfer, Upkeep, Fruits, Links, Chance, BuffStrength, Active, Moonlit }

// One thing every level of a path gives: Share more of an axis on a kind (the path's own, or its family's).
public sealed record HubEffect(NodeKind Kind, HubAxis Axis, float Share);

// A path's milestone (DESIGN.md, The Hub): reached at Level, on top of that level's own effects. Its effect on the
// path's kind: fruit slots or links more, its transfer, upkeep, store or life multiplied, a share of its output that
// holds whatever its store (OutputFloor), its gene blobs going further and faster (Reach), each of its leaves adding
// a share to its output (LeafBonus), its buds growing on their own faster (BudGrow). The rest are read where they act
// (Garden.HubAt): the stem cap, an Amplifier's triple, the buffs, a Moonflower's range and the Hub's own upkeep. Text is the panel's line.
public sealed record HubMilestone(int Level, string Text)
{
    public int Fruits { get; init; }
    public int Links { get; init; }
    public float Transfer { get; init; } = 1;
    public float Upkeep { get; init; } = 1;
    public float Store { get; init; } = 1;
    public float Life { get; init; } = 1;
    public float OutputFloor { get; init; }
    public float Reach { get; init; } = 1;
    public float LeafBonus { get; init; }
    public float BudGrow { get; init; } = 1;
    public float StoreAdd { get; init; } // power store added flat, after the multipliers (the stem's, Nov 3)
    public float LeafGen { get; init; } // power made, and sent on, once all its leaves are out (the Lichen's, Nov 3)
}

// A kind's path in the Hub's panel: each level's cost in points (so its number of levels), what every level gives
// (Each; PerLevel says it for the panel), and its milestones.
public sealed record HubPath(NodeKind Kind, int[] Costs, string PerLevel, HubEffect[] Each, params HubMilestone[] Milestones)
{
    public int MaxLevel => Costs.Length;

    // What reaching a level costs.
    public int Cost(int level) => Costs[level - 1];

    public HubMilestone? MilestoneAt(int level) => Milestones.FirstOrDefault(m => m.Level == level);
}

// The Hub's paths (DESIGN.md, The Hub; Jeremy's step 5 addendum, every number a placeholder): one for every main kind
// the garden can grow, the basic ones, the wild flowers in the table's order, then the Hub's own. Basic kinds have 5
// levels (milestones at 3 and 5), the wild flowers and the Hub 3 (a milestone at 3), 96 points in all: the Hub is meant
// to be finished. A wild flower's levels also boost its family: a stem, generator, grove or lab kind its base, the
// Amplifier, Resistor and passive-only kinds the core's power.
public static class Hubs
{
    static readonly int[] Basic = [1, 1, 2, 1, 3], Short = [1, 1, 2];

    public static readonly IReadOnlyList<HubPath> Paths = Build();

    public static HubPath Path(NodeKind kind) => Paths.First(p => p.Kind == kind);

    public static bool HasPath(NodeKind kind) => Paths.Any(p => p.Kind == kind);

    // Every level of every path.
    public static int TotalPoints => Paths.Sum(p => p.Costs.Sum());

    static List<HubPath> Build()
    {
        const NodeKind core = NodeKind.Core, stem = NodeKind.Stem, generator = NodeKind.Generator, grove = NodeKind.Grove,
            lab = NodeKind.Lab;
        HubPath Path(NodeKind kind, int[] costs, string each, HubEffect[] effects, params HubMilestone[] milestones) =>
            new(kind, costs, each, effects, milestones);
        HubEffect[] Own(NodeKind kind, HubAxis axis, float share, NodeKind family, HubAxis familyAxis, float familyShare) =>
            [new(kind, axis, share), new(family, familyAxis, familyShare)];
        // The specialists' and passive-only kinds' family: the core's power, +20% a level.
        HubEffect[] CorePower(NodeKind kind, HubAxis axis, float share) => Own(kind, axis, share, core, HubAxis.Gen, 0.2f);

        return
        [
            Path(stem, Basic, "+20% transfer", [new(stem, HubAxis.Transfer, 0.2f)],
                new HubMilestone(3, $"Stem max power +{Tuning.HubStemStore:0}") { StoreAdd = Tuning.HubStemStore },
                new HubMilestone(5, $"Stem cap +{Tuning.HubStemCap}")),
            Path(generator, Basic, "+20% power gen", [new(generator, HubAxis.Gen, 0.2f)],
                new HubMilestone(3, "Power transfer ×1.5") { Transfer = 1.5f },
                new HubMilestone(5, "Power gen 50-100% with stored power, instead of 0-100%") { OutputFloor = 0.5f }),
            Path(grove, Basic, "+20% growth", [new(grove, HubAxis.Blobs, 0.2f)],
                new HubMilestone(3, "+1 fruit slot") { Fruits = 1 },
                new HubMilestone(5, "+2 fruit slots") { Fruits = 2 }),
            Path(lab, Basic, "+20% genes", [new(lab, HubAxis.Blobs, 0.2f)],
                new HubMilestone(3, $"Gene motes that fade give {Tuning.HubFadedGenes3:0%} of their genes to the core"),
                new HubMilestone(5, $"Gene motes that fade give {Tuning.HubFadedGenes5:0%} of their genes to the core")),

            Path(NodeKind.Amplifier, Short, "+10% chance to double a mote's value; core +20% power gen",
                CorePower(NodeKind.Amplifier, HubAxis.Chance, 0.1f),
                new HubMilestone(3, $"{Tuning.HubTripleChance * 100:0}% chance to triple")),
            Path(NodeKind.Resistor, Short, "+1 glow; core +20% power gen", CorePower(NodeKind.Resistor, HubAxis.Fruits, 1),
                new HubMilestone(3, "+1 link") { Links = 1 }),
            Path(NodeKind.Tendril, Short, "+30% transfer; stems +10% transfer",
                Own(NodeKind.Tendril, HubAxis.Transfer, 0.3f, stem, HubAxis.Transfer, 0.1f),
                new HubMilestone(3, "+1 link") { Links = 1 }),
            Path(NodeKind.BigStem, Short, "+30% transfer; stems +10% transfer",
                Own(NodeKind.BigStem, HubAxis.Transfer, 0.3f, stem, HubAxis.Transfer, 0.1f),
                new HubMilestone(3, "+1 link") { Links = 1 }),
            Path(NodeKind.Pulsar, Short, "+30% power gen; generators +10% power gen",
                Own(NodeKind.Pulsar, HubAxis.Gen, 0.3f, generator, HubAxis.Gen, 0.1f),
                new HubMilestone(3, "Max power ×1.5: bigger bursts") { Store = 1.5f }),
            Path(NodeKind.TwinStar, Short, "+30% power gen; generators +10% power gen",
                Own(NodeKind.TwinStar, HubAxis.Gen, 0.3f, generator, HubAxis.Gen, 0.1f),
                new HubMilestone(3, "Transfer +50%") { Transfer = 1.5f }),
            Path(NodeKind.Ember, Short, "+30% power gen; generators +10% power gen",
                Own(NodeKind.Ember, HubAxis.Gen, 0.3f, generator, HubAxis.Gen, 0.1f),
                new HubMilestone(3, "Burns 50% longer (fades over 1.5 days)") { Life = 1.5f }),
            Path(NodeKind.LumberGrove, Short, "+30% growth; groves +10% growth",
                Own(NodeKind.LumberGrove, HubAxis.Blobs, 0.3f, grove, HubAxis.Blobs, 0.1f),
                new HubMilestone(3, "Drain −50%") { Upkeep = 0.5f }),
            Path(NodeKind.SeedPod, Short, "+30% growth; groves +10% growth",
                Own(NodeKind.SeedPod, HubAxis.Blobs, 0.3f, grove, HubAxis.Blobs, 0.1f),
                new HubMilestone(3, "Buds its motes start are twice as likely to grow wild")),
            Path(NodeKind.WildLab, Short, "+30% genes; labs +10% genes",
                Own(NodeKind.WildLab, HubAxis.Blobs, 0.3f, lab, HubAxis.Blobs, 0.1f),
                new HubMilestone(3, $"Max power +{Tuning.HubWildLabStore:0}: steadier") { StoreAdd = Tuning.HubWildLabStore }),
            Path(NodeKind.Lichen, Short, "+30% genes; labs +10% genes",
                Own(NodeKind.Lichen, HubAxis.Blobs, 0.3f, lab, HubAxis.Blobs, 0.1f),
                new HubMilestone(3, $"With all 4 leaves, makes {Tuning.HubLichenGen:0} power gen a second and transfers at {Tuning.HubLichenGen:0}")
                    { LeafGen = Tuning.HubLichenGen }),
            Path(NodeKind.Buffer, Short, "All buffs +10%", [new(NodeKind.Buffer, HubAxis.BuffStrength, 0.1f)],
                new HubMilestone(3, $"Buffs last +{Tuning.HubBuffSeconds:0} s")),
            Path(NodeKind.CometBloom, Short, "Active 2 minutes longer; core +20% power gen",
                CorePower(NodeKind.CometBloom, HubAxis.Active, 120),
                new HubMilestone(3, "Lingers half as long once dormant") { Life = 0.5f }),
            Path(NodeKind.Moonflower, Short, "Wildflowers near it +5% more; core +20% power gen",
                CorePower(NodeKind.Moonflower, HubAxis.Moonlit, 0.05f),
                new HubMilestone(3, $"Range from {Tuning.MoonflowerReach} links to {Tuning.HubMoonflowerReach}")),
            // (No Nebula Bloom path: it's retired, Nov 3. Levels bought on it before stay in the save, unused.)

            // The Hub's own (Jeremy, Oct 31): 3 links to start, +1 a level up to 6.
            Path(NodeKind.Hub, Short, "+1 link", [new(NodeKind.Hub, HubAxis.Links, 1)],
                new HubMilestone(3, "The drain each Hub upgrade adds −20%")),
        ];
    }
}

// The Hub's state in a save: its pool (genes not yet a point), the points to spend, the points earned and genes
// caught over the garden's life, and the levels bought on each path.
public sealed record HubSave(double Pool, int Points, int Earned, double Genes, Dictionary<NodeKind, int> Levels);
