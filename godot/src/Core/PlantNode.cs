using System.Numerics;

namespace SpaceGarden.Core;

public sealed class PlantNode
{
    public readonly int Id;
    public readonly NodeSpec Spec;
    public readonly PlantNode? Parent;
    public readonly List<PlantNode> Branches = new();
    public readonly List<PlantNode> Fruits = new();
    public readonly int Depth;

    public Vector2 Position;
    public Vector2 Velocity;
    // Where it was at the start of the last tick, so drawing can blend between ticks.
    public Vector2 PreviousPosition;
    public float Rotation;
    public float Spin;
    public bool Fixed;
    // The push from distant parts, kept between the ticks it's worked out on (see Garden.Repel).
    public Vector2 FarPush;
    // Its push on the rest of the plant: its own force, plus its fruit's when fruit only meets its family.
    public float PushForce;
    // This tick's near push from other parts (the far push is FarPush): fruit inherits its parent's.
    public Vector2 RepelPush;

    // Presentation state the rules keep so a save or a catch-up carries it: seconds since it sprouted (for the
    // grow-in), and a 0-1 glow that a passing mote sets and that fades.
    public float Age;
    public float Glow;

    public PlantNode(int id, NodeSpec spec, PlantNode? parent, Vector2 position)
    {
        Id = id;
        Spec = spec;
        Parent = parent;
        Depth = parent == null ? 0 : parent.Depth + 1;
        Position = PreviousPosition = position;
    }

    public int Links => Branches.Count + (Parent == null ? 0 : 1);
    public bool CanBranch => Links < Spec.MaxLinks;
    public bool CanFruit => Spec.Fruit != null && Fruits.Count < Spec.MaxFruits;

    public IEnumerable<PlantNode> Neighbours()
    {
        if (Parent != null) yield return Parent;
        foreach (var b in Branches) yield return b;
        foreach (var f in Fruits) yield return f;
    }
}

public sealed class Link
{
    public readonly PlantNode Origin, Target;
    public readonly float Length;
    public float Flash;

    public Link(PlantNode origin, PlantNode target)
    {
        Origin = origin;
        Target = target;
        float r = origin.Spec.Radius + target.Spec.Radius;
        Length = target.Spec.IsFruit ? r : r * 1.5f;
    }
}

// A point of light travelling along the stems, the TS game's transfer "blob". T runs 0-1 along the current link.
public sealed class Mote
{
    public PlantNode From, To;
    public float T;
    public int Hops;
    public uint Rgb;

    public Mote(PlantNode from, PlantNode to, uint rgb)
    {
        From = from;
        To = to;
        Rgb = rgb;
    }

    public Vector2 Position => Vector2.Lerp(From.Position, To.Position, T);
}
