using System.Numerics;

namespace SpaceGarden.Core.Tests;

public class ShapingTests
{
    static PlantNode Add(Garden garden, PlantNode parent, NodeKind kind) =>
        garden.GrowBranch(parent, kind) ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

    // core - trunk - a - b - c, a line of stems, with every tool allowed and nothing growing.
    static (Garden Garden, PlantNode Trunk, PlantNode Tip) Line(int seed = 7)
    {
        var garden = Garden.New(Layout.Radial, seed);
        garden.Capacity = new Capacity(5, 0, 1, Pots: 2, Trellises: 2, Wells: 2);
        garden.PassiveGrowth = false;
        var trunk = Add(garden, garden.Core, NodeKind.Stem);
        var tip = trunk;
        for (int i = 0; i < 3; i++) tip = Add(garden, tip, NodeKind.Stem);
        for (int i = 0; i < 600; i++) garden.Step(); // settled
        return (garden, trunk, tip);
    }

    static void Run(Garden garden, int ticks)
    {
        for (int i = 0; i < ticks; i++) garden.Step();
    }

    // ==== GRAVITY POT ==== \\

    [Fact]
    public void APotDrawsItsPartToItAndLocksItThere()
    {
        var (garden, trunk, _) = Line();
        var at = garden.Core.Position + new Vector2(150, 40);
        var pot = garden.Place(ShapingKind.Pot, at)!;
        Assert.True(garden.Tie(trunk, pot));
        Run(garden, 300);
        Assert.True(Vector2.Distance(trunk.Position, at) < 0.5f, $"the trunk is {Vector2.Distance(trunk.Position, at)} from its pot");
        Assert.Equal(Vector2.Zero, trunk.Velocity);
        // Moving the pot carries the part along.
        pot.Position += new Vector2(-60, 0);
        Run(garden, 300);
        Assert.True(Vector2.Distance(trunk.Position, pot.Position) < 0.5f);
        Assert.Same(trunk, garden.Potted(pot));
    }

    [Fact]
    public void AnEmptyPotDroppedOnAPartPlantsIt()
    {
        var (garden, trunk, tip) = Line();
        var pot = garden.Place(ShapingKind.Pot, trunk.Position + new Vector2(3, 0))!;
        Assert.Same(trunk, garden.PlantUnder(pot, 5));
        Assert.Same(pot, trunk.TiedTo);
        // A full pot keeps its part; off the plant nothing is planted.
        pot.Position = tip.Position;
        Assert.Null(garden.PlantUnder(pot, 5));
        Assert.Same(trunk, garden.Potted(pot));
        var empty = garden.Place(ShapingKind.Pot, garden.Core.Position + new Vector2(500, 500))!;
        Assert.Null(garden.PlantUnder(empty, 5));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void APotPushesThePartsLinkedToItsPartTheWayItFaces(int way)
    {
        // The pot sits off to the core's right, facing down (or up): the stem growing out of the potted one leans that
        // way. Only the potted stem's neighbours are pushed (that stem, and the core it grew from): the stems further
        // out just follow through their links.
        var (garden, trunk, tip) = Line();
        var next = trunk.Branches[0];
        var pot = garden.Place(ShapingKind.Pot, garden.Core.Position + new Vector2(120, 0), way * MathF.PI / 2)!;
        garden.Tie(trunk, pot);
        Run(garden, 1800);
        var lean = Vector2.Normalize(next.Position - pot.Position);
        Assert.True(Vector2.Dot(lean, pot.Facing) > 0.3f, $"the next stem leans {lean}, the pot faces {pot.Facing}");
        Assert.Equal(pot.Facing, next.Lean);
        Assert.Equal(pot.Facing, garden.Core.Lean);
        Assert.Equal(Vector2.Zero, next.Branches[0].Lean);
        Assert.Equal(Vector2.Zero, tip.Lean);
    }

    [Fact]
    public void APotPushesItsPartsParentToo()
    {
        // The middle of the line potted: the stem it grew from leans the pot's way, as does the one growing out of it,
        // but not the core, two links back.
        var (garden, trunk, tip) = Line();
        var middle = trunk.Branches[0];
        var pot = garden.Place(ShapingKind.Pot, middle.Position, MathF.PI / 2)!;
        garden.Tie(middle, pot);
        garden.Step();
        Assert.Equal(pot.Facing, trunk.Lean);
        Assert.Equal(pot.Facing, middle.Branches[0].Lean);
        Assert.Equal(Vector2.Zero, garden.Core.Lean);
        Assert.Equal(Vector2.Zero, tip.Lean);
    }

    [Fact]
    public void APotHoldsOnePart()
    {
        var (garden, trunk, tip) = Line();
        var pot = garden.Place(ShapingKind.Pot, garden.Core.Position + new Vector2(120, 0))!;
        garden.Tie(trunk, pot);
        garden.Tie(tip, pot);
        Assert.Null(trunk.TiedTo);
        Assert.Same(tip, garden.Potted(pot));
    }

    [Fact]
    public void FruitAndWellsTieNothing()
    {
        var (garden, trunk, _) = Line();
        var well = garden.Place(ShapingKind.Well, garden.Core.Position + new Vector2(0, 200))!;
        Assert.False(garden.Tie(trunk, well));
        var pot = garden.Place(ShapingKind.Pot, garden.Core.Position)!;
        var leaf = garden.GrowFruit(trunk)!;
        Assert.False(garden.Tie(leaf, pot));
    }

    // ==== TRELLIS ==== \\

    [Fact]
    public void ATrellisHoldsItsPartsOnItsLine()
    {
        var (garden, trunk, tip) = Line();
        var middle = trunk.Branches[0];
        var trellis = garden.Place(ShapingKind.Trellis, garden.Core.Position + new Vector2(0, -140))!;
        Assert.Equal(0, trellis.Angle); // level
        foreach (var node in new[] { middle, tip }) garden.Tie(node, trellis);
        Run(garden, 1200);
        var (a, b) = trellis.Ends;
        foreach (var node in new[] { middle, tip })
        {
            Assert.True(Vector2.Distance(node.Position, trellis.OnLine(node.Position)) < 0.01f, $"{node.Position} is off the line");
            Assert.InRange(node.Position.X, a.X - 0.01f, b.X + 0.01f);
            Assert.Equal(trellis.Position.Y, node.Position.Y, 2);
        }
        // They slide along it: the plant's push keeps them apart.
        Assert.True(Vector2.Distance(middle.Position, tip.Position) > 10);
        // Untied, a part is free again.
        garden.Untie(tip);
        Run(garden, 600);
        Assert.True(Math.Abs(tip.Position.Y - trellis.Position.Y) > 0.1f);
    }

    [Fact]
    public void ATrellisEndMovesAndTheOtherStays()
    {
        var trellis = new ShapingTool(ShapingKind.Trellis, Vector2.Zero, 0, 100);
        trellis.MoveEnd(ShapingHandle.EndB, new Vector2(0, 200));
        var (a, b) = trellis.Ends;
        Assert.True(Vector2.Distance(a, new Vector2(-50, 0)) < 0.01f);
        Assert.True(Vector2.Distance(b, new Vector2(0, 200)) < 0.01f);
        trellis.MoveEnd(ShapingHandle.EndA, b); // never shorter than its least
        Assert.Equal(ShapingTool.TrellisMinLength, trellis.Length, 2);
    }

    [Fact]
    public void ATrellisEndIsEasyToTakeEvenOnItsLine()
    {
        var garden = Garden.New(Layout.Radial, seed: 7);
        garden.Capacity = garden.Capacity with { Trellises = 1 };
        var trellis = garden.Place(ShapingKind.Trellis, new Vector2(500, 500))!; // level, 160 long
        var (a, b) = trellis.Ends;
        // On the line, just inside an end: the end, not the body, though the line is nearer.
        Assert.Equal((trellis, ShapingHandle.EndB), garden.ShapingAt(b - new Vector2(12, 0), 5));
        Assert.Equal((trellis, ShapingHandle.EndA), garden.ShapingAt(a + new Vector2(-8, 10), 5));
        // Its middle is still the body.
        Assert.Equal((trellis, ShapingHandle.Body), garden.ShapingAt(trellis.Position + new Vector2(0, 4), 5));
    }

    // ==== GRAVITY WELL ==== \\

    [Fact]
    public void AWellPullsTheWholePlantToIt()
    {
        // Core and all: a free plant drifts to the well, like gravity.
        var (garden, _, tip) = Line();
        var well = garden.Place(ShapingKind.Well, garden.Core.Position + new Vector2(500, 0))!;
        float coreWas = Vector2.Distance(garden.Core.Position, well.Position);
        float tipWas = Vector2.Distance(tip.Position, well.Position);
        Run(garden, 3600);
        float coreNow = Vector2.Distance(garden.Core.Position, well.Position);
        Assert.True(coreNow < coreWas - 100, $"the core went from {coreWas} to {coreNow} from the well");
        Assert.True(Vector2.Distance(tip.Position, well.Position) < tipWas - 100);
    }

    [Fact]
    public void APottedCoreKeepsThePlantAndItHangsTowardsTheWell()
    {
        // The core held in a pot: the plant stays, and its parts hang towards the well instead.
        var (plain, _, _) = Line();
        var (held, _, _) = Line();
        var coreAt = held.Core.Position;
        var pot = held.Place(ShapingKind.Pot, coreAt)!;
        held.Tie(held.Core, pot);
        var pot2 = plain.Place(ShapingKind.Pot, plain.Core.Position)!; // the same pot, with no well
        plain.Tie(plain.Core, pot2);
        held.Place(ShapingKind.Well, coreAt + new Vector2(500, 0));
        Run(plain, 1800);
        Run(held, 1800);
        static float Reach(Garden g) => g.Nodes.Skip(1).Average(n => n.Position.X - g.Core.Position.X);
        Assert.True(Reach(held) > Reach(plain) + 15, $"reaching {Reach(held)} towards the well against {Reach(plain)}");
        Assert.True(Vector2.Distance(held.Core.Position, coreAt) < 0.5f);
    }

    // ==== CAPS, SAVES, PRUNES ==== \\

    [Fact]
    public void ToolsComeWithTheGardensSize()
    {
        var garden = Garden.New(Layout.Radial, seed: 7);
        garden.Capacity = garden.Capacity with { Stems = 100, Generators = 100 };
        garden.PassiveGrowth = false;
        Assert.True(garden.CanPlace(ShapingKind.Pot));
        Assert.False(garden.CanPlace(ShapingKind.Trellis));
        Assert.False(garden.CanPlace(ShapingKind.Well));
        Assert.NotNull(garden.Place(ShapingKind.Pot, Vector2.Zero));
        Assert.Null(garden.Place(ShapingKind.Pot, Vector2.Zero)); // one pot to start
        Assert.Null(garden.Place(ShapingKind.Trellis, Vector2.Zero));

        // A line of stems off the core: the garden's size is its main nodes, the core too.
        var tip = garden.Core;
        void GrowTo(int size)
        {
            while (garden.Tracking.MostMainNodes < size) tip = Add(garden, tip, NodeKind.Stem);
        }
        GrowTo(14);
        Assert.False(garden.CanPlace(ShapingKind.Trellis));
        GrowTo(15);
        Assert.True(garden.CanPlace(ShapingKind.Trellis));
        Assert.False(garden.CanPlace(ShapingKind.Pot));
        GrowTo(25);
        Assert.True(garden.CanPlace(ShapingKind.Pot));
        Assert.False(garden.CanPlace(ShapingKind.Well));
        GrowTo(40);
        Assert.True(garden.CanPlace(ShapingKind.Well));
        Assert.Equal(new[] { "Trellis gained at garden size 15", "Second Gravity Pot gained at garden size 25",
            "Gravity Well gained at garden size 40" },
            garden.Tracking.Journal.Where(e => e.Kind == JournalKind.Tool).Select(e => e.Text));

        // Pruning never takes a tool away: the size is the most the garden has had.
        garden.Prune(garden.Core.Branches[0]);
        Assert.True(garden.CanPlace(ShapingKind.Well));
        Assert.Equal(2, garden.Capacity.Pots);

        // Taking one away makes room again.
        garden.Place(ShapingKind.Well, Vector2.Zero);
        Assert.False(garden.CanPlace(ShapingKind.Well));
        garden.Remove(garden.Shaping.Single(t => t.Kind == ShapingKind.Well));
        Assert.True(garden.CanPlace(ShapingKind.Well));
    }

    [Fact]
    public void ToolsByGardenSizeFollowTheDocs()
    {
        // DESIGN.md, Upgrade tracks: a pot at the start, a trellis at 15, a second pot at 25, a well at 40, +1 pot at
        // 60 and 100, then every 50; a second well at 100, a third at 150.
        Assert.Equal((1, 0, 0), Tuning.ToolsAtSize(1));
        Assert.Equal((1, 0, 0), Tuning.ToolsAtSize(14));
        Assert.Equal((1, 1, 0), Tuning.ToolsAtSize(15));
        Assert.Equal((2, 1, 0), Tuning.ToolsAtSize(25));
        Assert.Equal((2, 1, 1), Tuning.ToolsAtSize(40));
        Assert.Equal((3, 1, 1), Tuning.ToolsAtSize(60));
        Assert.Equal((3, 1, 1), Tuning.ToolsAtSize(99));
        Assert.Equal((4, 1, 2), Tuning.ToolsAtSize(100));
        Assert.Equal((4, 1, 2), Tuning.ToolsAtSize(149));
        Assert.Equal((5, 1, 3), Tuning.ToolsAtSize(150));
        Assert.Equal((6, 1, 3), Tuning.ToolsAtSize(200));

        Assert.Equal(15, Tuning.NextToolSize(ShapingKind.Trellis, 3));
        Assert.Null(Tuning.NextToolSize(ShapingKind.Trellis, 15));
        Assert.Equal(40, Tuning.NextToolSize(ShapingKind.Well, 39));
        Assert.Equal(100, Tuning.NextToolSize(ShapingKind.Well, 40));
        Assert.Equal(150, Tuning.NextToolSize(ShapingKind.Well, 120));
        Assert.Null(Tuning.NextToolSize(ShapingKind.Well, 150));
        Assert.Equal(25, Tuning.NextToolSize(ShapingKind.Pot, 1));
        Assert.Equal(60, Tuning.NextToolSize(ShapingKind.Pot, 25));
        Assert.Equal(100, Tuning.NextToolSize(ShapingKind.Pot, 60));
        Assert.Equal(150, Tuning.NextToolSize(ShapingKind.Pot, 100));
        Assert.Equal(200, Tuning.NextToolSize(ShapingKind.Pot, 170));
    }

    [Fact]
    public void ToolsAndWhatTheyHoldAreSaved()
    {
        var (garden, trunk, tip) = Line();
        var middle = trunk.Branches[0];
        var pot = garden.Place(ShapingKind.Pot, new Vector2(10, 20), 1.2f)!;
        var trellis = garden.Place(ShapingKind.Trellis, new Vector2(-30, 5))!;
        trellis.MoveEnd(ShapingHandle.EndB, new Vector2(100, 90));
        garden.Place(ShapingKind.Well, new Vector2(300, -40));
        garden.Tie(trunk, pot);
        garden.Tie(middle, trellis);
        garden.Tie(tip, trellis);

        var loaded = Garden.FromSave(garden.ToSave());
        Assert.Equal(3, loaded.Shaping.Count);
        for (int i = 0; i < 3; i++)
        {
            var (was, now) = (garden.Shaping[i], loaded.Shaping[i]);
            Assert.Equal(was.Kind, now.Kind);
            Assert.Equal(was.Position, now.Position);
            Assert.Equal(was.Angle, now.Angle);
            Assert.Equal(was.Length, now.Length);
        }
        PlantNode Same(PlantNode n) => loaded.Nodes[garden.Nodes.IndexOf(n)];
        Assert.Same(loaded.Shaping[0], Same(trunk).TiedTo);
        Assert.Same(loaded.Shaping[1], Same(middle).TiedTo);
        Assert.Same(loaded.Shaping[1], Same(tip).TiedTo);
        Assert.Null(loaded.Core.TiedTo);
        Assert.Equal(garden.Capacity, loaded.Capacity);
    }

    [Fact]
    public void APrunedPartLeavesItsPotAndAnUndoPutsItBack()
    {
        var (garden, trunk, tip) = Line();
        var pot = garden.Place(ShapingKind.Pot, tip.Position)!;
        garden.Tie(tip, pot);
        garden.Prune(tip);
        Assert.Null(garden.Potted(pot));
        garden.UndoPrune();
        Assert.Same(tip, garden.Potted(pot));

        // Not if the pot has gone meanwhile.
        garden.Prune(tip);
        garden.Remove(pot);
        garden.UndoPrune();
        Assert.Null(tip.TiedTo);
    }

    [Fact]
    public void PassiveGrowthBeyondAPotHeadsTheWayItFaces()
    {
        var (garden, trunk, tip) = Line();
        var pot = garden.Place(ShapingKind.Pot, tip.Position, MathF.Atan2(-1, 0))!; // facing up
        garden.Tie(tip, pot);
        garden.Capacity = garden.Capacity with { Stems = 40 };
        int up = 0;
        for (int i = 0; i < 20; i++)
        {
            var bud = garden.GrowBranch(tip, NodeKind.Stem)!;
            if (bud.Position.Y < tip.Position.Y) up++;
            garden.Prune(bud);
            garden.ForgetPrune();
        }
        Assert.True(up >= 18, $"{up} of 20 buds headed up");
    }

    // ==== Resizing (Nov 3) ==== \\

    // With no part tied, the core goes where it's put and the plant keeps its shape round it; tools go where they're put.
    [Fact]
    public void ReanchoringWithNothingTiedMovesThePlantWithTheCore()
    {
        var (garden, trunk, tip) = Line();
        var well = garden.Place(ShapingKind.Well, garden.Core.Position + new Vector2(400, -300))!;
        var offset = tip.Position - garden.Core.Position;
        var coreTo = garden.Core.Position + new Vector2(50, 20);
        var wellTo = well.Position + new Vector2(-200, 100);
        garden.Reanchor(new Dictionary<ShapingTool, Vector2> { [well] = wellTo }, coreTo);
        Assert.Equal(coreTo, garden.Core.Position);
        Assert.True(Vector2.Distance(offset, tip.Position - garden.Core.Position) < 1e-3f);
        Assert.Equal(wellTo, well.Position);
    }

    // A potted part locks onto its pot where the pot goes, and the plant follows it, not the core's spot.
    [Fact]
    public void ReanchoringLocksTiedPartsOntoTheirTools()
    {
        var (garden, trunk, tip) = Line();
        var pot = garden.Place(ShapingKind.Pot, trunk.Position)!;
        Assert.True(garden.Tie(trunk, pot));
        var offset = garden.Core.Position - trunk.Position;
        var potTo = pot.Position + new Vector2(-120, 60);
        garden.Reanchor(new Dictionary<ShapingTool, Vector2> { [pot] = potTo }, garden.Core.Position + new Vector2(999, 999));
        Assert.True(Vector2.Distance(potTo, trunk.Position) < 1e-3f);
        Assert.True(Vector2.Distance(potTo + offset, garden.Core.Position) < 1e-3f); // with the anchor, not to coreTo
    }

    [Fact]
    public void WithTwoWellsEachPartFeelsOnlyItsNearest()
    {
        // core (held where it is) with two branches of three stems; each branch's well off to its outer side, so the
        // branches swing apart, each towards its own (with every well pulling every part, they'd pull alike).
        var garden = Garden.New(Layout.Radial, seed: 7);
        garden.Capacity = new Capacity(8, 0, 2, Pots: 1, Wells: 2);
        garden.PassiveGrowth = false;
        var tips = new List<PlantNode>();
        for (int b = 0; b < 2; b++)
        {
            var tip = garden.Core;
            for (int i = 0; i < 3; i++) tip = Add(garden, tip, NodeKind.Stem);
            tips.Add(tip);
        }
        for (int i = 0; i < 600; i++) garden.Step(); // settled
        garden.Core.Fixed = true;
        var core = garden.Core.Position;
        float AngleOf(Vector2 p) => MathF.Atan2(p.Y - core.Y, p.X - core.X);
        float Between(float a, float b) => MathF.Abs(MathF.IEEERemainder(a - b, MathF.Tau));
        var angles = tips.Select(t => AngleOf(t.Position)).ToList();
        float side = MathF.Sign(MathF.IEEERemainder(angles[0] - angles[1], MathF.Tau)); // tip 0's outer side
        var wells = new List<ShapingTool>();
        for (int i = 0; i < 2; i++)
        {
            float at = angles[i] + (i == 0 ? side : -side) * 0.8f;
            wells.Add(garden.Place(ShapingKind.Well, core + new Vector2(MathF.Cos(at), MathF.Sin(at)) * 400)!);
        }
        float spread = Between(angles[0], angles[1]);
        for (int i = 0; i < 3000; i++) garden.Step();
        var now = tips.Select(t => AngleOf(t.Position)).ToList();
        for (int i = 0; i < 2; i++)
        {
            float well = AngleOf(wells[i].Position);
            Assert.True(Between(now[i], well) < Between(angles[i], well) - 0.2f,
                $"tip {i}: {Between(angles[i], well):0.00} to {Between(now[i], well):0.00} from its well");
        }
        Assert.True(Between(now[0], now[1]) > spread + 0.4f, $"spread {spread:0.00} to {Between(now[0], now[1]):0.00}");
    }
}
