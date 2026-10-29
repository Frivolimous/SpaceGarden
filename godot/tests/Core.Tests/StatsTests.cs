namespace SpaceGarden.Core.Tests;

// The hover readout's numbers (GardenStats) and the selected part's panel text (NodeSpecs.Info).
public class StatsTests
{
    static Garden NewGarden(Capacity capacity)
    {
        var garden = Garden.New(Layout.Radial, seed: 7);
        garden.Capacity = capacity;
        return garden;
    }

    static PlantNode Add(Garden garden, PlantNode parent, NodeKind kind) =>
        (NodeSpecs.All[kind].IsFruit ? garden.GrowFruit(parent) : garden.SproutFrom(parent, kind))
            ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

    // ==== NODE INFO ==== \\

    [Fact]
    public void EveryKindHasANameAndAOneLineDescription()
    {
        foreach (var kind in Enum.GetValues<NodeKind>())
        {
            Assert.True(NodeSpecs.Info.TryGetValue(kind, out var info), $"{kind} has no info");
            Assert.False(string.IsNullOrWhiteSpace(info.Name), $"{kind} has no name");
            Assert.False(string.IsNullOrWhiteSpace(info.Description), $"{kind} has no description");
            Assert.DoesNotContain('\n', info.Description);
            Assert.True(info.Description.Length <= 80, $"{kind}'s description is too long for the panel");
        }
    }

    // ==== GENE REACH ==== \\

    [Fact]
    public void ALabOnTheCoreAlwaysReachesIt()
    {
        var garden = NewGarden(new Capacity(4, 1, 1));
        var lab = Add(garden, garden.Core, NodeKind.Lab);
        Assert.Equal(1, GardenStats.GeneReach(garden, lab), 12);
    }

    // core - stem, forking into the lab and a dead-end stem. From the stem a blob goes to the core or the dead end,
    // half each, and every two links it's back at the stem with the same choice: 15 tries in 30 links.
    [Fact]
    public void ReachHalvesAtEachForkAndStopsAtTheHopLimit()
    {
        var garden = NewGarden(new Capacity(4, 1, 1));
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        var lab = Add(garden, stem, NodeKind.Lab);
        Add(garden, stem, NodeKind.Stem);
        Assert.Equal(Tuning.BlobMaxHops, 30);
        Assert.Equal(1 - Math.Pow(0.5, 15), GardenStats.GeneReach(garden, lab), 12);
    }

    // The odds agree with the blobs' real walk.
    [Fact]
    public void ReachMatchesTheWalk()
    {
        var garden = NewGarden(new Capacity(6, 1, 1));
        var trunk = Add(garden, garden.Core, NodeKind.Stem);
        var fork = Add(garden, trunk, NodeKind.Stem);
        var left = Add(garden, fork, NodeKind.Stem);
        var right = Add(garden, fork, NodeKind.Stem);
        var far = Add(garden, right, NodeKind.Stem);
        Add(garden, far, NodeKind.Stem);
        var lab = Add(garden, left, NodeKind.Lab);
        Add(garden, left, NodeKind.Lab);
        Add(garden, trunk, NodeKind.Lab);

        double reach = GardenStats.GeneReach(garden, lab);
        Assert.InRange(reach, 0.2, 0.95); // a real test: not a sure thing either way
        const int walks = 40_000;
        int found = 0;
        for (int i = 0; i < walks; i++)
            if (garden.WalkGene(lab)) found++;
        Assert.Equal(reach, found / (double)walks, 0.012);
    }

    // ==== GARDEN STATS ==== \\

    [Fact]
    public void StatsCountTheGarden()
    {
        var garden = NewGarden(new Capacity(5, 2, 1));
        var trunk = Add(garden, garden.Core, NodeKind.Stem);
        var fork = Add(garden, trunk, NodeKind.Stem);
        var left = Add(garden, fork, NodeKind.Stem);
        var right = Add(garden, fork, NodeKind.Stem);
        var lab = Add(garden, left, NodeKind.Lab);
        var grove = Add(garden, left, NodeKind.Grove);
        Add(garden, right, NodeKind.Generator);
        Add(garden, trunk, NodeKind.Leaf);
        Add(garden, lab, NodeKind.Research);
        right.Forming = NodeKind.Lab; // a wild bud on its way
        foreach (var n in garden.Nodes) n.Power = n.Store / 2;

        var stats = GardenStats.Of(garden);
        Assert.Equal(4, stats.Stems);
        Assert.Equal(1, stats.Generators);
        Assert.Equal(1, stats.Groves);
        Assert.Equal(1, stats.Labs);
        Assert.Equal(1, stats.CoreLinksUsed);
        Assert.Equal(garden.Capacity, stats.Capacity);
        Assert.Equal(2, stats.Fruit);
        Assert.Equal(4 * 2 + 3 * 3, stats.FruitRoom); // 4 stems with room for 2 leaves, 3 flowers for 3 fruit each
        Assert.Equal(1, stats.BudsForming[NodeKind.Lab]);
        Assert.Equal(0.5f, stats.PowerFill, 4);
        Assert.Equal(garden.PowerBalance(), (stats.Production, stats.Upkeep));
        Assert.Equal(GardenStats.GenesPerHourFrom(garden, lab), stats.GenesPerHour, 9);
        Assert.True(stats.GenesPerHour > 0);
    }

    [Fact]
    public void OverloadedWhenUpkeepAtFullPassesProduction()
    {
        // Whatever the fills: what the garden would cost full against what it would make full.
        var garden = NewGarden(new Capacity(4, 1, 1));
        var trunk = Add(garden, garden.Core, NodeKind.Stem);
        Assert.False(GardenStats.Of(garden).Overloaded);
        Add(garden, trunk, NodeKind.Lab);
        var branch = Add(garden, Add(garden, trunk, NodeKind.Stem), NodeKind.Stem);
        Add(garden, branch.Parent!, NodeKind.Lab); // 3 stems and 2 labs: 21 a second against the core's 24, fine
        Assert.False(GardenStats.Of(garden).Overloaded);
        Add(garden, branch, NodeKind.Lab);
        Assert.True(GardenStats.Of(garden).Overloaded); // a third lab: 27 a second
    }

    [Fact]
    public void TheNextUnlockIsTimedAtTheCurrentRate()
    {
        var garden = NewGarden(Capacity.Start);
        var next = garden.NextUnlock!;
        garden.Genes = next.Genes - 100;
        var stats = GardenStats.Of(garden) with { GenesPerHour = 600 };
        Assert.Equal(100, stats.GenesToNext, 9);
        Assert.Equal(600, stats.SecondsToNext!.Value, 9); // 100 genes at 10 a minute

        Assert.Null((stats with { GenesPerHour = 0 }).SecondsToNext); // no labs: never
        Assert.Null((stats with { NextUnlock = null }).SecondsToNext); // the track done
    }
}
