namespace SpaceGarden.Core;

public enum NodeKind { Core, Stem, Leaf, Generator, Burr, Lab, Research, Grove, Berry }

public enum Shape { Circle, Triangle, Square, Pentagon, Hexagon }

// One kind of plant part. MaxLinks counts the link to the parent too (as in the TS game), so a stem with 3 has
// two branches; fruits have none and hang from their parent outside that count.
public sealed record NodeSpec(
    NodeKind Kind, float Radius, float Mass, float Force, int MaxLinks, Shape Shape, uint Rgb,
    NodeKind? Fruit = null, int MaxFruits = 0)
{
    public bool IsFruit => MaxLinks == 0;
    public float InverseMass { get; } = 1 / Mass;
}

// Ported from the TS game's data/NodeData.ts and data/Colors.ts (radius, mass, force, links, shape, colour).
// The core's links are the garden's Capacity.CoreLinks (1 at the start, raised by unlocks), not its spec's.
// The grove is the TS game's Fruit Grove (an orange square); its fruit, the TS "gen", is a berry here.
public static class NodeSpecs
{
    public static readonly IReadOnlyDictionary<NodeKind, NodeSpec> All = new Dictionary<NodeKind, NodeSpec>
    {
        [NodeKind.Core] = new(NodeKind.Core, 30, 30, 30, Tuning.StartCoreLinks, Shape.Circle, 0xE5E570),
        [NodeKind.Stem] = new(NodeKind.Stem, 10, 1, 1, 3, Shape.Circle, 0x07E000, NodeKind.Leaf, 2),
        [NodeKind.Generator] = new(NodeKind.Generator, 15, 3, 3, 1, Shape.Pentagon, 0xE5E570, NodeKind.Burr, 3),
        [NodeKind.Lab] = new(NodeKind.Lab, 15, 2, 1.5f, 1, Shape.Hexagon, 0x9370E0, NodeKind.Research, 3),
        [NodeKind.Grove] = new(NodeKind.Grove, 15, 2, 1.5f, 1, Shape.Square, 0xE07000, NodeKind.Berry, 3),
        [NodeKind.Leaf] = new(NodeKind.Leaf, 5, 0.5f, 0.2f, 0, Shape.Triangle, 0x03AA03),
        [NodeKind.Burr] = new(NodeKind.Burr, 5, 0.5f, 0.2f, 0, Shape.Triangle, 0xE02323),
        [NodeKind.Research] = new(NodeKind.Research, 5, 0.5f, 0.2f, 0, Shape.Pentagon, 0x9370E0),
        [NodeKind.Berry] = new(NodeKind.Berry, 5, 0.5f, 0.2f, 0, Shape.Circle, 0xF0A040),
    };
}
