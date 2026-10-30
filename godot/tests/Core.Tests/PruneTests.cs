using System.Numerics;

namespace SpaceGarden.Core.Tests;

public class PruneTests
{
    static int UndoTicks => (int)Math.Ceiling(Tuning.UndoPruneSeconds * Tuning.TicksPerSecond);

    // Fruit only grows from groves' blobs now, so tests grow it directly.
    static PlantNode Add(Garden garden, PlantNode parent, NodeKind kind) =>
        (NodeSpecs.All[kind].IsFruit ? garden.GrowFruit(parent) : garden.GrowBranch(parent, kind)) ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

    // core - trunk - fork, the fork carrying a lab with a research fruit and a stem with a leaf. The caps (3 stems,
    // no generators) are full.
    static (Garden Garden, PlantNode Trunk, PlantNode Fork) Branched()
    {
        var garden = Garden.New(Layout.Radial, seed: 7);
        garden.Capacity = new Capacity(3, 0, 1);
        garden.PassiveGrowth = false;
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
    public void ACutBranchFallsApartDriftsOffAndFadesThenGoes()
    {
        var (garden, trunk, fork) = Branched();
        for (int i = 0; i < 600; i++) garden.Step(); // settled
        float before = Vector2.Distance(fork.Position, trunk.Position);
        var leaf = fork.Branches.Single(b => b.Spec.Kind == NodeKind.Stem).Fruits[0];
        float leafBefore = Vector2.Distance(leaf.Position, leaf.Parent!.Position);
        var removed = garden.Prune(fork);

        var cut = Assert.Single(garden.Cut);
        Assert.Equal(removed, cut.Nodes);
        garden.Step();
        Assert.All(removed, n => Assert.True(cut.FadeOf(n) < 1)); // fading from the cut
        Assert.True(removed.Select(cut.FadeOf).Distinct().Count() > 1); // each at its own rate
        int fadeTicks = (int)(Tuning.CutFadeSeconds * Tuning.TicksPerSecond);
        for (int i = 0; i < fadeTicks - 3; i++) garden.Step();

        Assert.Single(garden.Cut);
        float after = Vector2.Distance(fork.Position, trunk.Position);
        Assert.True(after > before + 10, $"drifted from {before:0.0} to {after:0.0}");
        // Nothing holds its parts together any more: even a leaf drifts off its stem.
        float leafAfter = Vector2.Distance(leaf.Position, leaf.Parent!.Position);
        Assert.True(leafAfter > leafBefore + 5, $"leaf from {leafBefore:0.0} to {leafAfter:0.0}");
        Assert.All(removed, n => Assert.InRange(cut.FadeOf(n), 0, 0.02f));

        for (int i = 0; i < 4; i++) garden.Step();
        Assert.Empty(garden.Cut);
    }

    [Fact]
    public void UndoWhileItDriftsPutsItBackWhereItWas()
    {
        var (garden, trunk, fork) = Branched();
        var offset = fork.Position - trunk.Position;
        garden.Prune(fork);
        for (int i = 0; i < 60; i++) garden.Step();

        garden.UndoPrune();
        Assert.Empty(garden.Cut);
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
    public void PassiveGrowthWaitsOutTheWindow()
    {
        // A grove beside the fork: once the fork is cut, its blobs start no bud and no fruit on the trunk while the
        // prune can be undone, so the room it freed stays free.
        var (garden, trunk, fork) = Branched();
        garden.Capacity = garden.Capacity with { Stems = 4 };
        var grove = Add(garden, trunk, NodeKind.Grove);
        garden.PassiveGrowth = true;
        garden.Prune(fork);
        // Hours of growth (as at a fast growth speed) within the window.
        garden.Grow(6 * 3600);
        Assert.Equal([grove], trunk.Branches);
        Assert.Empty(trunk.Fruits);
        Assert.Null(trunk.Forming);
        Assert.DoesNotContain(garden.Nodes, n => n.FormingBud);
        Assert.True(garden.CanUndoPrune);

        for (int i = 0; i < UndoTicks + 1; i++) garden.Step();
        garden.Grow(12 * 3600);
        Assert.True(trunk.Fruits.Count > 0 || trunk.Forming != null || trunk.Branches.Count > 1);
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
