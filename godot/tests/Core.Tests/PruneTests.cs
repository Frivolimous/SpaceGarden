using System.Numerics;

namespace SpaceGarden.Core.Tests;

public class PruneTests
{
    static int UndoTicks => (int)Math.Ceiling(Tuning.UndoPruneSeconds * Tuning.TicksPerSecond);

    static PlantNode Add(Garden garden, PlantNode parent, NodeKind kind) =>
        garden.SproutFrom(parent, kind) ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

    // core - trunk - fork, the fork carrying a lab with a research fruit and a stem with a leaf. The caps (3 stems,
    // no generators) are full.
    static (Garden Garden, PlantNode Trunk, PlantNode Fork) Branched()
    {
        var garden = Garden.New(Layout.Radial, seed: 7);
        garden.Capacity = new Capacity(3, 0, 1);
        garden.Unlocks = Tuning.UnlockTrack.Length; // fixed caps
        garden.WildGrowth = false;
        var trunk = Add(garden, garden.Core, NodeKind.Stem);
        var fork = Add(garden, trunk, NodeKind.Stem);
        var lab = Add(garden, fork, NodeKind.Lab);
        Add(garden, lab, NodeKind.Research);
        Add(garden, Add(garden, fork, NodeKind.Stem), NodeKind.Leaf);
        foreach (var n in garden.Nodes) n.Power = n.Store;
        return (garden, trunk, fork);
    }

    static string Shape(Garden garden) => string.Join(",", garden.Nodes
        .OrderBy(n => n.Id)
        .Select(n => $"{n.Id}<{n.Parent?.Id}:{n.Branches.Count}b{n.Fruits.Count}f"))
        + $"|links {garden.Links.Count}";

    [Fact]
    public void UndoPutsTheBranchBackAsItWas()
    {
        var (garden, _, fork) = Branched();
        var before = Shape(garden);
        var powers = garden.Nodes.ToDictionary(n => n.Id, n => n.Power);

        var removed = garden.Prune(fork);
        Assert.Equal(5, removed.Count);
        Assert.True(garden.CanUndoPrune);

        var back = garden.UndoPrune();
        Assert.Equal(removed, back);
        Assert.Equal(before, Shape(garden));
        Assert.All(garden.Nodes, n => Assert.Equal(powers[n.Id], n.Power));
        Assert.False(garden.CanUndoPrune);
        Assert.Null(garden.UndoPrune());
    }

    [Fact]
    public void RestoredPartsLinkUpAndSaveInGrowthOrder()
    {
        var (garden, _, fork) = Branched();
        garden.Prune(fork);
        garden.UndoPrune();

        // Every part but the core has exactly one link to its parent, and the physics runs on them.
        Assert.Equal(garden.Nodes.Count - 1, garden.Links.Count);
        Assert.All(garden.Links, l => Assert.Equal(l.Origin, l.Target.Parent));
        for (int i = 0; i < 60; i++) garden.Step();

        var copy = Garden.FromSave(garden.ToSave());
        Assert.Equal(Shape(garden).Split('|')[0], Shape(copy).Split('|')[0]);
    }

    [Fact]
    public void TheBranchFollowsItsParentIfThePlantMoved()
    {
        var (garden, trunk, fork) = Branched();
        var offset = fork.Position - trunk.Position;
        garden.Prune(fork);
        garden.Translate(new Vector2(200, -50));

        garden.UndoPrune();
        Assert.Equal(trunk.Position + offset, fork.Position);
    }

    [Fact]
    public void OnlyTheLastPruneCanBeUndone()
    {
        var (garden, trunk, fork) = Branched();
        var lab = fork.Branches.First(b => b.Spec.Kind == NodeKind.Lab);
        garden.Prune(lab);
        garden.Prune(fork);

        garden.UndoPrune();
        Assert.Contains(fork, garden.Nodes);
        Assert.DoesNotContain(lab, garden.Nodes);
        Assert.Null(garden.UndoPrune());
        Assert.Single(trunk.Branches);
    }

    [Fact]
    public void TheWindowClosesAfterUndoPruneSeconds()
    {
        var (garden, _, fork) = Branched();
        garden.Prune(fork);
        for (int i = 0; i < UndoTicks - 2; i++) garden.Step();
        Assert.True(garden.CanUndoPrune);
        for (int i = 0; i < 4; i++) garden.Step();
        Assert.False(garden.CanUndoPrune);
        Assert.Null(garden.UndoPrune());
    }

    [Fact]
    public void TimeAwayClosesTheWindow()
    {
        var (garden, _, fork) = Branched();
        garden.Prune(fork);
        garden.CatchUp(60);
        Assert.False(garden.CanUndoPrune);
    }

    [Fact]
    public void WildGrowthWaitsOutTheWindow()
    {
        var (garden, _, fork) = Branched();
        garden.WildGrowth = true;
        garden.Prune(fork);
        // Hours of growth (as at a fast growth speed) within the window: nothing grows into the freed room.
        int grew = garden.Grow(6 * 3600);
        Assert.Equal(0, grew);
        Assert.True(garden.CanUndoPrune);

        for (int i = 0; i < UndoTicks + 1; i++) garden.Step();
        Assert.True(garden.Grow(2 * 3600) > 0);
    }

    [Fact]
    public void ABudInTheFreedRoomBlocksTheUndo()
    {
        var (garden, trunk, fork) = Branched();
        garden.Prune(fork);
        var bud = Add(garden, trunk, NodeKind.Stem); // takes the trunk's free link and a stem slot
        var before = Shape(garden);

        Assert.False(garden.CanUndoPrune);
        Assert.Null(garden.UndoPrune());
        Assert.Equal(before, Shape(garden));

        // Pruning the bud makes it the last prune; the fork is gone for good.
        garden.Prune(bud);
        garden.UndoPrune();
        Assert.Contains(bud, garden.Nodes);
        Assert.DoesNotContain(fork, garden.Nodes);
    }

    [Fact]
    public void TheCoreCantBePrunedOrUndone()
    {
        var (garden, _, _) = Branched();
        Assert.Empty(garden.Prune(garden.Core));
        Assert.False(garden.CanUndoPrune);
    }
}
