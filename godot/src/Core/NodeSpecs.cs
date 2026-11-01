namespace SpaceGarden.Core;

// The basic kinds, then the wild flowers (DESIGN.md, Wild flowers), in the table's order.
public enum NodeKind
{
    Core, Stem, Leaf, Generator, Burr, Lab, Research, Grove, Berry,
    Amplifier, Resistor, Tendril, BigStem, Pulsar, TwinStar, Ember, LumberGrove, SeedPod, WildLab, Lichen,
    Buffer, CometBloom, Moonflower, NebulaBloom,
    Wood, Glow, // the wild flowers' fruit
    Seedling, // planted by hand, it blooms into its evolution's wild flower (step 5c)
    Hub, // planted by hand once bought, one at a time: catches genes for points of its own (see Hubs.cs)
}

// The basic shapes, then the wild flowers' more complex ones, each radially symmetric (drawn from one atlas: see
// GardenView's Textures.Atlas).
public enum Shape
{
    Circle, Triangle, Square, Pentagon, Hexagon,
    Plus, Minus, Trefoil, Quatrefoil, Star5, Star10, Flame, Star8, Oval, Star6, Lobes6, Burst8, Gear, Rose, Swirl,
    Sprout, // the seedling's (step 5c)
    Wheel, // the Hub's
}

// What blobs a flower sends: growth blobs (groves), gene blobs (labs), buff blobs (a Buffer, a glow fruit), or gene
// and growth blobs mixed (a Comet Bloom's burst).
public enum BlobKind { None, Growth, Genes, Buff, All }

// When a wild flower kind can start to grow wild: from the first wild flower slot, or once its evolution is taken.
public enum WildUnlock { Start, Evolution }

// What makes a kind a wild flower (DESIGN.md, Wild flowers): the basic kind whose bud it can replace (null: passive
// only, any passive bud), when it unlocks, its quirk (the line on its card saying what it trades), and whether it takes
// its base kind's slot as well as a wild flower slot (BaseSlot: the stem and generator kinds, but the Amplifier and
// the Resistor; groves and labs are uncapped).
public sealed record WildSpec(NodeKind? Base, WildUnlock Unlock, string Quirk, bool BaseSlot = false)
{
    public bool PassiveOnly => Base == null;
}

// One kind of plant part, all its numbers in one place, as the TS game's Nodes config. MaxLinks counts the link to
// the parent too (as in the TS game), so a stem with 3 has two branches; fruits have none and hang from their parent
// outside that count. Name heads the part's card (DESIGN.md, Information display); Description, a line on what it
// does, is off the card since step 3d and kept for the Almanac's Codex.
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
    // Its links (to its parent and its branches) are this many times the usual length, and pull this hard when
    // stretched (1: as usual).
    public float LinkScale { get; init; } = 1;
    public float Spring { get; init; } = 1;

    // Set on a wild flower (null on the basic kinds).
    public WildSpec? Wild { get; init; }
    public bool IsWild => Wild != null;

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

    // Blobs it makes an hour when full, and which: growth blobs from a grove, gene blobs from a lab. They leave once
    // BlobBurst have built up (1: each as it's made).
    public float BlobsPerHour { get; init; }
    public BlobKind Sends { get; init; }
    public int BlobBurst { get; init; } = 1;

    // A fruit's effect on the part it hangs from (its flower): its full upkeep is multiplied by 1 plus the sum of
    // its fruits' FlowerPowerFactor (a leaf's -0.25: one leaf, three quarters; two, half), and its output (power, or
    // blobs) by 1 plus the sum of their FlowerBonus, times the flower's FruitBonusScale.
    public float FlowerPowerFactor { get; init; }
    public float FlowerBonus { get; init; }
    public float FruitBonusScale { get; init; } = 1;

    // Wild flowers' quirks (see Garden, Wild flowers). BurstShare: it sends nothing until it's full, then that share of
    // its power at once (0: clumps as usual). LifeSeconds: it ends itself after so long (0: never); if Dims, the power
    // it can hold falls with it to nothing (its store, and so its look and rate, stay: see Garden.PowerCap). DoublesBlobs: a blob passing through may become two
    // (AmplifyChance, once a blob); BlocksBlobs: no blob steps onto it. EmergeBurst: blobs it sends at once as it
    // appears. WildBoost: buds near it are this much likelier to grow wild, and wild flowers near it Moonlit.
    public float BurstShare { get; init; }
    public float LifeSeconds { get; init; }
    public bool Dims { get; init; }
    public bool DoublesBlobs { get; init; }
    public bool BlocksBlobs { get; init; }
    public int EmergeBurst { get; init; }
    public float WildBoost { get; init; }
    // SeedlingBoost: power reaching a seedling within Tuning.NebulaReach links of it counts this many times (a Nebula
    // Bloom's; not stacking).
    public float SeedlingBoost { get; init; }

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
// Nodes config (power). The core's links are the garden's Capacity.CoreLinks (1 at the start, raised by upgrades), not
// its spec's. The grove is the TS game's Fruit Grove (an orange square); its fruit, the TS "gen", is a berry here.
// Every kind has a name and description (StatsTests checks).
public static class NodeSpecs
{
    public static readonly IReadOnlyDictionary<NodeKind, NodeSpec> All = Build();

    // The wild flower kinds, in the table's order.
    public static readonly IReadOnlyList<NodeSpec> Wild = All.Values.Where(s => s.IsWild).OrderBy(s => s.Kind).ToList();

    static Dictionary<NodeKind, NodeSpec> Build()
    {
        var all = new Dictionary<NodeKind, NodeSpec>
        {
            [NodeKind.Core] = new(NodeKind.Core)
            {
                Name = "Core", Description = "The heart: makes the most power, and takes in genes.",
                Radius = 30, Mass = 30, Force = 30, MaxLinks = Tuning.StartCoreLinks, Shape = Shape.Circle, Rgb = 0xE5E570,
                PowerStore = NodeBase.PowerStore * 4, PowerPerSecond = NodeBase.CoreGen, PowerWeight = 1.5f,
                PowerTransferPerSecond = NodeBase.CoreGen * 1.8f,
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
                PowerTransferPerSecond = NodeBase.SmallGen * 2.15f,
            },
            [NodeKind.Lab] = new(NodeKind.Lab)
            {
                Name = "Lab", Description = "Sends gene blobs; only those that find the core count.",
                Radius = 15, Mass = 2, Force = 1.5f, MaxLinks = 1, Shape = Shape.Hexagon, Rgb = 0x9370E0,
                Fruit = NodeKind.Research, MaxFruits = 3, PowerToFruitPerSecond = NodeBase.PowerToFruit,
                PowerStore = NodeBase.PowerStore, PowerPerSecond = NodeBase.PowerDrain * 1.5f,
                PowerTransferPerSecond = NodeBase.PowerTransferSub,
                BlobsPerHour = 720, Sends = BlobKind.Genes, // one every 5 s
            },
            [NodeKind.Grove] = new(NodeKind.Grove)
            {
                Name = "Grove", Description = "Sends growth blobs that grow fruit and new parts.",
                Radius = 15, Mass = 2, Force = 1.5f, MaxLinks = 1, Shape = Shape.Square, Rgb = 0xE07000,
                Fruit = NodeKind.Berry, MaxFruits = 3, PowerToFruitPerSecond = NodeBase.PowerToFruit,
                PowerStore = NodeBase.PowerStore, PowerPerSecond = NodeBase.PowerDrain,
                PowerTransferPerSecond = NodeBase.PowerTransferSub,
                BlobsPerHour = 1200, Sends = BlobKind.Growth, // one every 3 s
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
        foreach (var spec in WildFlowers.All(all)) all[spec.Kind] = spec;
        // A seedling (DESIGN.md, Seedlings and evolutions): a small flower at the end of a branch, planted by hand, that
        // only takes power in (no upkeep, and no transfer to send any on), heavy so its neighbours favour it. Its store
        // is the choice's; once an evolution is chosen, the evolution's target (see PlantNode.Store).
        all[NodeKind.Seedling] = new(NodeKind.Seedling)
        {
            Name = "Seedling", Description = "Takes in power, then blooms into its chosen evolution's wild flower.",
            Radius = 11, Mass = 1.5f, Force = 1.5f, MaxLinks = 1, Shape = Shape.Sprout, Rgb = 0xB4F08C,
            PowerStore = NodeBase.PowerStore, PowerPerSecond = 0, PowerWeight = 1.5f,
            PowerTransferPerSecond = 0,
        };
        // The Hub (DESIGN.md, The Hub): a stem in its own shade of green, taking no stem slot, that catches gene blobs as
        // the core does, into a pool of its own. Its upkeep rises with every level bought on it (see Garden.UpdateBoosts).
        all[NodeKind.Hub] = new(NodeKind.Hub)
        {
            Name = "Hub", Description = "Catches gene blobs for points of its own, which boost each kind of part.",
            Radius = 25, Mass = 20, Force = 20, MaxLinks = 3, Shape = Shape.Wheel, Rgb = 0x2ED89A,
            PowerStore = NodeBase.PowerStore * 10f, PowerPerSecond = -Tuning.HubUpkeep, PowerWeight = 1.1f,
            PowerTransferPerSecond = NodeBase.PowerTransfer * 0.5f,
        };
        return all;
    }
}
