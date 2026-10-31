namespace SpaceGarden.Core;

// Which of a kind's numbers its Hub path raises (its main effect): the power it makes, the blobs it sends (genes,
// growth or buffs), how fast it sends power on (the stem kinds), or its own quirk (Boost.Effect: an Amplifier's chance
// to double a blob, a Comet Bloom's burst, a Moonflower's pull on buds, a Nebula Bloom's boost to seedlings).
public enum HubAxis { Gen, Blobs, Transfer, Effect }

// A hand-crafted milestone on a path (DESIGN.md, The Hub): reaching Level costs Cost points instead of 1, and adds
// this on top of the level's usual boost: fruit slots, a transfer or upkeep multiplier. Text is the panel's line.
public sealed record HubMilestone(int Level, int Cost, string Text, int Fruits = 0, float Transfer = 1, float Upkeep = 1);

// A kind's path in the Hub's panel: what its levels raise (Axis, named in Effect for the panel: "power"), and its
// milestones.
public sealed record HubPath(NodeKind Kind, HubAxis Axis, string Effect, params HubMilestone[] Milestones)
{
    // What reaching a level costs: its milestone's cost, or a point.
    public int Cost(int level) => Milestones.FirstOrDefault(m => m.Level == level)?.Cost ?? 1;

    public HubMilestone? MilestoneAt(int level) => Milestones.FirstOrDefault(m => m.Level == level);
}

// The Hub's paths (DESIGN.md, The Hub): one for every main kind the garden can grow, the basic ones then the wild
// flowers in the table's order (19 in all). First passes for Jeremy's review: the milestones are on the basic kinds,
// a fruit slot more at level 5 and a cheaper or quicker part at level 10.
public static class Hubs
{
    public static readonly IReadOnlyList<HubPath> Paths = Build();

    public static HubPath Path(NodeKind kind) => Paths.First(p => p.Kind == kind);

    public static bool HasPath(NodeKind kind) => Paths.Any(p => p.Kind == kind);

    static List<HubPath> Build()
    {
        var paths = new List<HubPath>
        {
            new(NodeKind.Stem, HubAxis.Transfer, "transfer",
                new HubMilestone(5, 2, "Stems hold +1 leaf", Fruits: 1),
                new HubMilestone(10, 3, "Stems' upkeep ×0.75", Upkeep: 0.75f)),
            new(NodeKind.Generator, HubAxis.Gen, "power",
                new HubMilestone(5, 2, "Generators hold +1 burr", Fruits: 1),
                new HubMilestone(10, 3, "Generator transfer ×1.5", Transfer: 1.5f)),
            new(NodeKind.Lab, HubAxis.Blobs, "genes",
                new HubMilestone(5, 2, "Labs hold +1 research", Fruits: 1),
                new HubMilestone(10, 3, "Labs' upkeep ×0.75", Upkeep: 0.75f)),
            new(NodeKind.Grove, HubAxis.Blobs, "growth",
                new HubMilestone(5, 2, "Groves hold +1 berry", Fruits: 1),
                new HubMilestone(10, 3, "Groves' upkeep ×0.75", Upkeep: 0.75f)),
        };
        foreach (var spec in NodeSpecs.Wild)
        {
            var (axis, effect) = spec.Kind switch
            {
                NodeKind.Amplifier => (HubAxis.Effect, "chance to double a blob"),
                NodeKind.CometBloom => (HubAxis.Effect, "burst"),
                NodeKind.Moonflower => (HubAxis.Effect, "pull on buds"),
                NodeKind.NebulaBloom => (HubAxis.Effect, "boost to seedlings"),
                _ when spec.PowerPerSecond > 0 => (HubAxis.Gen, "power"),
                _ when spec.Sends == BlobKind.Genes => (HubAxis.Blobs, "genes"),
                _ when spec.Sends == BlobKind.Growth => (HubAxis.Blobs, "growth"),
                _ when spec.Sends == BlobKind.Buff => (HubAxis.Blobs, "buff blobs"),
                _ => (HubAxis.Transfer, "transfer"),
            };
            paths.Add(new HubPath(spec.Kind, axis, effect));
        }
        return paths;
    }
}

// The Hub's state in a save: its pool (genes not yet a point), the points to spend, the points earned and genes
// caught over the garden's life, and the levels bought on each path.
public sealed record HubSave(double Pool, int Points, int Earned, double Genes, Dictionary<NodeKind, int> Levels);
