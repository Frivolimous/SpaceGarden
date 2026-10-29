using System.Numerics;

namespace SpaceGarden.Core.Tests;

public class ShapingTests
{
    static PlantNode Add(Garden garden, PlantNode parent, NodeKind kind) =>
        garden.SproutFrom(parent, kind) ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

    // core - trunk - a - b - c, a line of stems, with every tool allowed and nothing growing.
    static (Garden Garden, PlantNode Trunk, PlantNode Tip) Line(int seed = 7)
    {
        var garden = Garden.New(Layout.Radial, seed);
        garden.Capacity = new Capacity(5, 0, 1, Pots: 2, Trellises: 2, Wells: 2);
        garden.Unlocks = Tuning.UnlockTrack.Length; // fixed caps
        garden.WildGrowth = false;
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
    public void ToolsUnlockAlongTheTrack()
    {
        var garden = Garden.New(Layout.Radial, seed: 7);
        Assert.True(garden.CanPlace(ShapingKind.Pot));
        Assert.False(garden.CanPlace(ShapingKind.Trellis));
        Assert.False(garden.CanPlace(ShapingKind.Well));
        Assert.NotNull(garden.Place(ShapingKind.Pot, Vector2.Zero));
        Assert.Null(garden.Place(ShapingKind.Pot, Vector2.Zero)); // one pot to start
        Assert.Null(garden.Place(ShapingKind.Trellis, Vector2.Zero));

        garden.Deliver(7_200);
        Assert.True(garden.CanPlace(ShapingKind.Trellis));
        Assert.False(garden.CanPlace(ShapingKind.Pot));
        garden.Deliver(15_000 - 7_200);
        Assert.True(garden.CanPlace(ShapingKind.Pot));
        Assert.False(garden.CanPlace(ShapingKind.Well));
        garden.Deliver(70_000 - 15_000);
        Assert.True(garden.CanPlace(ShapingKind.Well));

        // Taking one away makes room again.
        garden.Place(ShapingKind.Well, Vector2.Zero);
        Assert.False(garden.CanPlace(ShapingKind.Well));
        garden.Remove(garden.Shaping.Single(t => t.Kind == ShapingKind.Well));
        Assert.True(garden.CanPlace(ShapingKind.Well));
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
    public void WildGrowthBeyondAPotHeadsTheWayItFaces()
    {
        var (garden, trunk, tip) = Line();
        var pot = garden.Place(ShapingKind.Pot, tip.Position, MathF.Atan2(-1, 0))!; // facing up
        garden.Tie(tip, pot);
        garden.Capacity = garden.Capacity with { Stems = 40 };
        int up = 0;
        for (int i = 0; i < 20; i++)
        {
            var bud = garden.SproutFrom(tip, NodeKind.Stem)!;
            if (bud.Position.Y < tip.Position.Y) up++;
            garden.Prune(bud);
            garden.ForgetPrune();
        }
        Assert.True(up >= 18, $"{up} of 20 buds headed up");
    }
}
