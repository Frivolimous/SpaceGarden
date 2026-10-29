namespace SpaceGarden.Core;

public enum NodeKind { Core, Stem, Leaf, Generator, Burr, Lab, Research, Grove, Berry }

public enum Shape { Circle, Triangle, Square, Pentagon, Hexagon }

// One kind of plant part, all its numbers in one place, as the TS game's Nodes config. MaxLinks counts the link to
// the parent too (as in the TS game), so a stem with 3 has two branches; fruits have none and hang from their parent
// outside that count. Name and Description are for the selected part's panel (DESIGN.md, Desktop widget).
public sealed record NodeSpec(NodeKind Kind)
{
    public required string Name { get; init; }
    public required string Description { get; init; }

    // Physics and looks.
    public required float Radius { get; init; }
    public required float Mass { get; init; }
    public required float Force { get; init; }
    public required int MaxLinks { get; init; }
    public required Shape Shape { get; init; }
    public required uint Rgb { get; init; }
    public NodeKind? Fruit { get; init; }
    public int MaxFruits { get; init; }

    // Power (DESIGN.md, Economy), as the original's powerMax, powerGen, powerWeight and powerClump: how much it holds;
    // what it makes (above 0) or costs (below 0) a second when full; how much emptier than it is it counts as when
    // its neighbours choose where to send power; and the clump it sends. Fruit holds none.
    public float Store { get; init; }
    public float PowerPerSecond { get; init; }
    public float Weight { get; init; } = 1;
    public float Clump { get; init; }

    // Blobs it makes an hour when full: growth blobs from a grove, gene blobs from a lab.
    public float BlobsPerHour { get; init; }

    public bool IsFruit => MaxLinks == 0;
    public float InverseMass => 1 / Mass;
}

// The values the kinds share, as the original's NodeBase: its per-tick numbers times 60, to make them per second.
public static class NodeBase
{
    public const float Store = 100;
    public const float Drain = -0.1f * 60;
    public const float StemDrain = -0.05f * 60;
    public const float CoreGen = 0.4f * 60;
    public const float SmallGen = 0.2f * 60;
    public const float Clump = 10;
    public const float ClumpSub = 5;
}

// Ported from the TS game's data/NodeData.ts and data/Colors.ts (radius, mass, force, links, shape, colour) and its
// Nodes config (power). The core's links are the garden's Capacity.CoreLinks (1 at the start, raised by unlocks), not
// its spec's. The grove is the TS game's Fruit Grove (an orange square); its fruit, the TS "gen", is a berry here.
// Every kind has a name and description (StatsTests checks).
public static class NodeSpecs
{
    public static readonly IReadOnlyDictionary<NodeKind, NodeSpec> All = new Dictionary<NodeKind, NodeSpec>
    {
        [NodeKind.Core] = new(NodeKind.Core)
        {
            Name = "Core", Description = "The heart: makes the most power, and takes in genes.",
            Radius = 30, Mass = 30, Force = 30, MaxLinks = Tuning.StartCoreLinks, Shape = Shape.Circle, Rgb = 0xE5E570,
            Store = NodeBase.Store * 3, PowerPerSecond = NodeBase.CoreGen, Weight = 1.5f, Clump = NodeBase.Clump * 1.5f,
        },
        [NodeKind.Stem] = new(NodeKind.Stem)
        {
            Name = "Stem", Description = "Structure: carries power and blobs. Cheap upkeep, less with leaves.",
            Radius = 10, Mass = 1, Force = 1, MaxLinks = 3, Shape = Shape.Circle, Rgb = 0x07E000,
            Fruit = NodeKind.Leaf, MaxFruits = 2,
            Store = NodeBase.Store, PowerPerSecond = NodeBase.StemDrain, Weight = 1.1f, Clump = NodeBase.Clump,
        },
        [NodeKind.Generator] = new(NodeKind.Generator)
        {
            Name = "Generator", Description = "Makes power for its branch, at half the core's rate.",
            Radius = 15, Mass = 3, Force = 3, MaxLinks = 1, Shape = Shape.Pentagon, Rgb = 0xE5E570,
            Fruit = NodeKind.Burr, MaxFruits = 3,
            Store = NodeBase.Store, PowerPerSecond = NodeBase.SmallGen, Clump = NodeBase.Clump,
        },
        [NodeKind.Lab] = new(NodeKind.Lab)
        {
            Name = "Lab", Description = "Sends gene blobs; only those that find the core count.",
            Radius = 15, Mass = 2, Force = 1.5f, MaxLinks = 1, Shape = Shape.Hexagon, Rgb = 0x9370E0,
            Fruit = NodeKind.Research, MaxFruits = 3,
            Store = NodeBase.Store * 2, PowerPerSecond = NodeBase.Drain, Clump = NodeBase.ClumpSub,
            BlobsPerHour = 720, // one every 5 s
        },
        [NodeKind.Grove] = new(NodeKind.Grove)
        {
            Name = "Grove", Description = "Sends growth blobs that grow fruit and new wild parts.",
            Radius = 15, Mass = 2, Force = 1.5f, MaxLinks = 1, Shape = Shape.Square, Rgb = 0xE07000,
            Fruit = NodeKind.Berry, MaxFruits = 3,
            Store = NodeBase.Store, PowerPerSecond = NodeBase.Drain, Clump = NodeBase.ClumpSub,
            BlobsPerHour = 1200, // one every 3 s
        },
        [NodeKind.Leaf] = new(NodeKind.Leaf)
        {
            Name = "Leaf", Description = "Halves its stem's upkeep.",
            Radius = 5, Mass = 0.5f, Force = 0.2f, MaxLinks = 0, Shape = Shape.Triangle, Rgb = 0x03AA03,
        },
        [NodeKind.Burr] = new(NodeKind.Burr)
        {
            Name = "Burr", Description = "Raises its generator's output a little.",
            Radius = 5, Mass = 0.5f, Force = 0.2f, MaxLinks = 0, Shape = Shape.Triangle, Rgb = 0xE02323,
        },
        [NodeKind.Research] = new(NodeKind.Research)
        {
            Name = "Research", Description = "Raises its lab's gene output a little.",
            Radius = 5, Mass = 0.5f, Force = 0.2f, MaxLinks = 0, Shape = Shape.Pentagon, Rgb = 0x9370E0,
        },
        [NodeKind.Berry] = new(NodeKind.Berry)
        {
            Name = "Berry", Description = "Raises its grove's growth output a little.",
            Radius = 5, Mass = 0.5f, Force = 0.2f, MaxLinks = 0, Shape = Shape.Circle, Rgb = 0xF0A040,
        },
    };
}
