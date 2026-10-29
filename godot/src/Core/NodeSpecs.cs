namespace SpaceGarden.Core;

public enum NodeKind { Core, Stem, Leaf, Generator, Burr, Lab, Research, Grove, Berry }

public enum Shape { Circle, Triangle, Square, Pentagon, Hexagon }

// One kind of plant part, all its numbers in one place, as the TS game's Nodes config. MaxLinks counts the link to
// the parent too (as in the TS game), so a stem with 3 has two branches; fruits have none and hang from their parent
// outside that count. Name heads the part's card (DESIGN.md, Information display); Description, a line on what it
// does, is off the card since step 3d and kept for the Almanac.
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
    // its neighbours choose where to send power; and how fast it can send power on (a second: each economy step it
    // sends that times the step's length, see Garden.Clump). Fruit holds power but costs and sends none.
    public float PowerStore { get; init; }
    public float PowerPerSecond { get; init; }
    public float PowerWeight { get; init; } = 1;
    public float PowerTransferPerSecond { get; init; }
    // How fast it can feed its fruit (the original's fruitClump; see Garden.FruitToFeed).
    public float PowerToFruitPerSecond { get; init; }

    // Blobs it makes an hour when full: growth blobs from a grove, gene blobs from a lab.
    public float BlobsPerHour { get; init; }

    // A fruit's effect on the part it hangs from (its flower): its full upkeep is multiplied by 1 plus the sum of
    // its fruits' FlowerPowerFactor (a leaf's -0.25: one leaf, three quarters; two, half), and its output (power, or
    // blobs) by 1 plus the sum of their FlowerBonus.
    public float FlowerPowerFactor { get; init; }
    public float FlowerBonus { get; init; }

    public bool IsFruit => MaxLinks == 0;
    public float InverseMass => 1 / Mass;
}

// The values the kinds share, as the original's NodeBase: its per-tick numbers times 60, to make them per second.
public static class NodeBase
{
    public const float PowerStore = 200f;
    public const float FruitStore = 100f;
    public const float PowerDrain = -4f;
    public const float StemDrain = -3f;
    public const float CoreGen = 24f;
    public const float SmallGen = 12f;
    public const float PowerTransfer = 30f;
    public const float PowerTransferSub = 15f;
    public const float PowerToFruit = 15f;
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
            PowerStore = NodeBase.PowerStore * 4, PowerPerSecond = NodeBase.CoreGen, PowerWeight = 1.5f,
            PowerTransferPerSecond = NodeBase.CoreGen * 1.2f,
        },
        [NodeKind.Stem] = new(NodeKind.Stem)
        {
            Name = "Stem", Description = "Structure: carries power and blobs. Cheap upkeep, less with leaves.",
            Radius = 10, Mass = 1, Force = 1, MaxLinks = 3, Shape = Shape.Circle, Rgb = 0x07E000,
            Fruit = NodeKind.Leaf, MaxFruits = 2, PowerToFruitPerSecond = NodeBase.PowerToFruit,
            PowerStore = NodeBase.PowerStore * 3/4, PowerPerSecond = NodeBase.StemDrain, PowerWeight = 1.1f,
            PowerTransferPerSecond = NodeBase.PowerTransfer,
        },
        [NodeKind.Generator] = new(NodeKind.Generator)
        {
            Name = "Generator", Description = "Makes power for its branch, at half the core's rate.",
            Radius = 15, Mass = 3, Force = 3, MaxLinks = 1, Shape = Shape.Pentagon, Rgb = 0xE5E570,
            Fruit = NodeKind.Burr, MaxFruits = 2, PowerToFruitPerSecond = NodeBase.PowerToFruit,
            PowerStore = NodeBase.PowerStore, PowerPerSecond = NodeBase.SmallGen,
            PowerTransferPerSecond = NodeBase.SmallGen * 1.5f,
        },
        [NodeKind.Lab] = new(NodeKind.Lab)
        {
            Name = "Lab", Description = "Sends gene blobs; only those that find the core count.",
            Radius = 15, Mass = 2, Force = 1.5f, MaxLinks = 1, Shape = Shape.Hexagon, Rgb = 0x9370E0,
            Fruit = NodeKind.Research, MaxFruits = 3, PowerToFruitPerSecond = NodeBase.PowerToFruit,
            PowerStore = NodeBase.PowerStore, PowerPerSecond = NodeBase.PowerDrain * 1.5f,
            PowerTransferPerSecond = NodeBase.PowerTransferSub,
            BlobsPerHour = 720, // one every 5 s
        },
        [NodeKind.Grove] = new(NodeKind.Grove)
        {
            Name = "Grove", Description = "Sends growth blobs that grow fruit and new wild parts.",
            Radius = 15, Mass = 2, Force = 1.5f, MaxLinks = 1, Shape = Shape.Square, Rgb = 0xE07000,
            Fruit = NodeKind.Berry, MaxFruits = 3, PowerToFruitPerSecond = NodeBase.PowerToFruit,
            PowerStore = NodeBase.PowerStore, PowerPerSecond = NodeBase.PowerDrain,
            PowerTransferPerSecond = NodeBase.PowerTransferSub,
            BlobsPerHour = 1200, // one every 3 s
        },
        [NodeKind.Leaf] = new(NodeKind.Leaf)
        {
            Name = "Leaf", Description = "Cuts its stem's upkeep by a quarter.",
            Radius = 5, Mass = 0.5f, Force = 0.2f, MaxLinks = 0, Shape = Shape.Triangle, Rgb = 0x03AA03,
            PowerStore = NodeBase.FruitStore, FlowerPowerFactor = -0.25f,
        },
        [NodeKind.Burr] = new(NodeKind.Burr)
        {
            Name = "Burr", Description = "Raises its generator's output a little.",
            Radius = 5, Mass = 0.5f, Force = 0.2f, MaxLinks = 0, Shape = Shape.Triangle, Rgb = 0xE02323,
            PowerStore = NodeBase.FruitStore, FlowerBonus = 0.1f,
        },
        [NodeKind.Research] = new(NodeKind.Research)
        {
            Name = "Research", Description = "Raises its lab's gene output a little.",
            Radius = 5, Mass = 0.5f, Force = 0.2f, MaxLinks = 0, Shape = Shape.Pentagon, Rgb = 0x9370E0,
            PowerStore = NodeBase.FruitStore, FlowerBonus = 0.1f,
        },
        [NodeKind.Berry] = new(NodeKind.Berry)
        {
            Name = "Berry", Description = "Raises its grove's growth output a little.",
            Radius = 5, Mass = 0.5f, Force = 0.2f, MaxLinks = 0, Shape = Shape.Circle, Rgb = 0xF0A040,
            PowerStore = NodeBase.FruitStore, FlowerBonus = 0.1f,
        },
    };
}
