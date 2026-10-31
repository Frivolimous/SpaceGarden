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
    // Held still where it is: only an upright (planted) garden's core, while it isn't held.
    public bool Fixed;
    // The pot or trellis holding it, if any (see Garden.Tie), and the way a potted neighbour's pot pushes it (zero if
    // no neighbour is potted: see Garden.Shape).
    public ShapingTool? TiedTo;
    public Vector2 Lean;
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

    // The economy (see Garden.EconomyStep). Power held, about up to Store (a transfer can top it a little past).
    // Fruit holds power its part feeds it, and costs nothing.
    public float Power;
    // A lab's gene blob, or a grove's growth blob, in the making: it leaves at 1, whether or not anything will absorb
    // it.
    public float Blob;

    // Growth seconds since it grew (a wild flower's life cycle runs on it: an Ember's dimming, a Comet Bloom's
    // withering, a Pulsar's bursts). A Pulsar's power built up and not yet released (see Garden.EconomyStep). Seconds
    // left of a buff (from a Buffer or a glow fruit), which raises what it makes (not saved: it's brief).
    public double Life;
    public float Charge;
    public float Buff;
    // How much more a buff makes it make (Tuning.BuffBonus, stronger with the Buffer's Hub milestone), set as it's buffed.
    public float BuffBonus = Tuning.BuffBonus;
    // A wild flower within MoonflowerReach links of a Moonflower, which makes it a little more (worked out each
    // economy step).
    public bool Moonlit;

    // The garden's evolutions' effect on its kind (see Garden.Boosts): set as it's added, and again as one blooms.
    public Boost Boost = Boost.None;

    // A seedling (step 5c) fills with power like any part: full, it offers its evolutions (rolled then, so tapping again
    // shows the same), and the one chosen sets its store (its target), which it blooms at. SeedBoost: how many times the
    // power reaching it counts (a Nebula Bloom within reach: worked out each economy step).
    public List<EvolutionKind>? Offers;
    public Evolution? Evolution;
    public float SeedBoost = 1;
    public bool IsSeedling => Spec.Kind == NodeKind.Seedling;
    public bool IsHub => Spec.Kind == NodeKind.Hub;
    // Waiting for the player to choose its evolution.
    public bool Choosing => IsSeedling && Evolution == null && Offers != null;

    // What growth blobs are forming here (DESIGN.md, Economy, Blobs): a fruit of its own kind, or a bud of a new
    // branch, its kind rolled when it started; and how much growth it has absorbed (it completes at FruitGrowth or
    // BudGrowth). A bud holds its link and its cap slot while it forms. FormingLook is the growth as shown: it rises
    // as each blob arrives (the rules count a blob when it leaves).
    public NodeKind? Forming;
    public float Growth;
    public float FormingLook;
    public bool FormingFruit => Forming is { } kind && NodeSpecs.All[kind].IsFruit;
    public bool FormingBud => Forming is { } kind && !NodeSpecs.All[kind].IsFruit;

    // Presentation, as the original's node intensity: the fill as shown, eased towards the real one.
    public float FillLook;

    // Its power level as drawn (DESIGN.md, Visual language): its brightness, and its size as a share of its radius.
    public float Level => LevelOf(FillLook);

    // See Tuning.FillLookCurve: FillLookFloor empty, 1 full, steepest near full; past full slower, up to FillLookMax.
    public static float LevelOf(float fill) => fill > 1
        ? Math.Min(Tuning.FillLookMax, 1 + (fill - 1) * Tuning.OverFillLook)
        : Tuning.FillLookFloor + (1 - Tuning.FillLookFloor) * MathF.Pow(Math.Max(0, fill), Tuning.FillLookCurve);

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

    // How much power it holds (none for fruit), and how full it is.
    // (A seedling's, once its evolution is chosen, is the evolution's target; a Pulsar's more with its Hub milestone.)
    public float Store => Evolution is { } evolution ? (float)evolution.Target : Spec.PowerStore * Boost.Store;
    // Whether its power counts as the garden's (the cards' and the history's): not fruit, nor a seedling's, which is
    // spent on its evolution.
    public bool HoldsGardenPower => !Spec.IsFruit && !IsSeedling;
    public float Fill => Store == 0 ? 0 : Power / Store;

    public int Links => Branches.Count + (Parent == null ? 0 : 1);
    // A forming bud holds a link, and a forming fruit a fruit slot.
    public bool CanBranch => Links + (FormingBud ? 1 : 0) < MaxLinks;
    public bool CanFruit => Spec.Fruit != null && Fruits.Count + (FormingFruit ? 1 : 0) < MaxFruits;
    // Its spec's, plus any its evolutions add (Big Stem: a leaf more on each stem).
    public int MaxFruits => Spec.MaxFruits + Boost.Fruits;

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
    // 0-1 light that a flash sets and that fades: each power transfer along the link flashes it, FlashIn seconds
    // after the economy step that made it (see Tuning.FlashPeak).
    public float Flash;
    // The last flash's own brightness, and seconds since it (for the wipe drawn along the link).
    public float FlashStrength, FlashAge = float.MaxValue;
    public float FlashIn = -1; // seconds to a flash still to show, or below 0 for none
    public bool FlowsToTarget; // which way the power ran (outward, or back towards the origin)

    public Link(PlantNode origin, PlantNode target)
    {
        Origin = origin;
        Target = target;
        float r = origin.Spec.Radius + target.Spec.Radius;
        // A Tendril's links are long and soft both ways: to its parent, and to what grows from it (not its fruit).
        Length = target.Spec.IsFruit ? r : r * 1.5f * Math.Max(origin.Spec.LinkScale, target.Spec.LinkScale);
        Spring = target.Spec.IsFruit ? 1 : Math.Min(origin.Spec.Spring, target.Spec.Spring);
    }

    // How hard it pulls when stretched, against the usual (a Tendril's link is soft).
    public readonly float Spring;
}

// A branch just pruned (Nodes[0] was cut from its parent), every link in it cut too, its parts drifting loose for
// CutFadeSeconds. FadeOf runs 1 to 0 from the cut, for drawing: each part a little quicker or slower than the next
// (up to CutFadeJitter of the time quicker, picked by its id, so the garden's dice aren't touched).
public sealed class CutBranch(List<PlantNode> nodes)
{
    public readonly List<PlantNode> Nodes = nodes;
    public float Seconds;

    public float FadeOf(PlantNode node)
    {
        float jitter = (uint)(node.Id * 2654435761u) % 1000 / 1000f;
        return Math.Clamp(1 - Seconds / (Tuning.CutFadeSeconds * (1 - Tuning.CutFadeJitter * jitter)), 0, 1);
    }
}

// A point of light travelling along the stems, the TS game's transfer "blob". T runs 0-1 along the current link.
public sealed class Mote
{
    public PlantNode From, To;
    public float T;
    // Seconds before it sets off (and shows): the second blob an Amplifier makes waits for the first to reach it.
    public float Delay;
    // How many times as fast as usual it travels (a lab's gene blobs with its Hub milestone).
    public float Speed = 1;
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
