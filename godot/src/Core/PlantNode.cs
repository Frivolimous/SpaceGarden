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
    // grow-in), and a 0-1 glow that a blob absorbed here sets, in the blob's colour, and that fades.
    public float Age;
    public float Glow;
    public uint GlowRgb;

    // The economy (see Garden.EconomyStep). Power held, up to Store; a part that couldn't pay its upkeep is Wilting
    // until it fills back up. Fruit holds no power and wilts with its parent. WiltLook eases 0-1 towards Wilting, for
    // drawing.
    public float Power;
    public bool Wilting;
    // A lab's gene blob, or a grove's growth blob, in the making: it leaves at 1, whether or not anything will absorb
    // it.
    public float Blob;
    public float WiltLook;

    // What growth blobs are forming here (DESIGN.md, Economy, Blobs): a fruit of its own kind, or a bud of a new
    // branch, its kind rolled when it started; and how much growth it has absorbed (it completes at FruitGrowth or
    // BudGrowth). A bud holds its link and its cap slot while it forms. FormingLook is the growth as shown: it rises
    // as each blob arrives (the rules count a blob when it leaves).
    public NodeKind? Forming;
    public float Growth;
    public float FormingLook;
    public bool FormingFruit => Forming is { } kind && NodeSpecs.All[kind].IsFruit;
    public bool FormingBud => Forming is { } kind && !NodeSpecs.All[kind].IsFruit;

    // Presentation, as the original's node intensity: the fill as shown, eased towards the real one, plus a small
    // spring that a flash of power arriving kicks, so the part swells and brightens past its level and settles back.
    public float FillLook;
    public float Bounce, BounceSpeed;

    // Links allowed, counting the parent's. The spec's, except the core's, which is the garden's Capacity.CoreLinks.
    public int MaxLinks;

    public PlantNode(int id, NodeSpec spec, PlantNode? parent, Vector2 position)
    {
        Id = id;
        Spec = spec;
        Parent = parent;
        Depth = parent == null ? 0 : parent.Depth + 1;
        Position = PreviousPosition = position;
        MaxLinks = spec.MaxLinks;
    }

    public float Store => Spec.IsFruit ? 0 : Parent == null ? Tuning.CorePowerStore : Tuning.NodePowerStore;
    public float Fill => Spec.IsFruit ? 0 : Power / Store;

    public int Links => Branches.Count + (Parent == null ? 0 : 1);
    // A forming bud holds a link, and a forming fruit a fruit slot.
    public bool CanBranch => Links + (FormingBud ? 1 : 0) < MaxLinks;
    public bool CanFruit => Spec.Fruit != null && Fruits.Count + (FormingFruit ? 1 : 0) < Spec.MaxFruits;

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
    // 0-1 light that a flash sets and that fades. Power flowing along the link (either way, per real hour, as of the
    // last economy step) sets how often it flashes; FlashPhase counts up to the next.
    public float Flash;
    public float FlowPerHour;
    public float FlashPhase;
    public bool FlowsToTarget; // which way the power ran (outward, or back towards the origin)

    public Link(PlantNode origin, PlantNode target)
    {
        Origin = origin;
        Target = target;
        float r = origin.Spec.Radius + target.Spec.Radius;
        Length = target.Spec.IsFruit ? r : r * 1.5f;
    }
}

// A branch just pruned (Nodes[0] was cut from its parent), every link in it cut too, its parts drifting loose for
// CutFadeSeconds. Fade runs 1 to 0 over that time, from the cut, for drawing.
public sealed class CutBranch(List<PlantNode> nodes)
{
    public readonly List<PlantNode> Nodes = nodes;
    public float Seconds;
    public float Fade => Math.Clamp(1 - Seconds / Tuning.CutFadeSeconds, 0, 1);
}

// A point of light travelling along the stems, the TS game's transfer "blob". T runs 0-1 along the current link.
public sealed class Mote
{
    public PlantNode From, To;
    public float T;
    // A blob follows the Route its walk was worked out on when it left (Route[Leg] is From). A growth blob that a
    // part absorbs ends there and adds Growth to what's forming as it arrives (Completes: it finished it, and the new
    // part shows then).
    public readonly List<PlantNode> Route;
    public int Leg;
    public uint Rgb;
    public float Growth;
    public bool Completes;
    // Whether the part at the end of its route absorbs it (else it fades there).
    public bool Absorbed;

    public Mote(List<PlantNode> route, uint rgb)
    {
        Route = route;
        From = route[0];
        To = route[1];
        Rgb = rgb;
    }

    public Vector2 Position => Vector2.Lerp(From.Position, To.Position, T);
}
