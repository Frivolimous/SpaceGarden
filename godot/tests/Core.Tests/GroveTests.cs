namespace SpaceGarden.Core.Tests;

public class GroveTests
{
    const double Hour = 3600, Day = 24 * Hour;
    static readonly double BlobHours = 1 / Tuning.GroveFruitPerHour;

    static Garden NewGarden(Capacity? capacity = null, int seed = 7)
    {
        var garden = Garden.New(Layout.Radial, seed);
        if (capacity != null) garden.Capacity = capacity;
        garden.Unlocks = Tuning.UnlockTrack.Length; // the track done: no raises
        return garden;
    }

    static PlantNode Add(Garden garden, PlantNode parent, NodeKind kind) =>
        garden.SproutFrom(parent, kind) ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

    static int Fruit(Garden garden) => garden.Nodes.Count(n => n.Spec.IsFruit);

    // core - stem - stem - grove, full of power, nothing else growing: room for 7 fruit (2 leaves a stem, 3 berries).
    static (Garden Garden, PlantNode Grove) WithAGrove(int seed = 7)
    {
        var garden = NewGarden(new Capacity(2, 0, 1), seed);
        garden.WildGrowth = false;
        var grove = Add(garden, Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Stem), NodeKind.Grove);
        foreach (var n in garden.Nodes) n.Power = n.Store;
        return (garden, grove);
    }

    // ==== RATES ==== \\

    [Fact]
    public void AGroveCostsHalfALab()
    {
        var (garden, grove) = WithAGrove();
        Assert.Equal(1.5f, Garden.UpkeepPerHour(grove));
        Assert.Equal(Tuning.LabUpkeepPerHour, 2 * Garden.UpkeepPerHour(grove));
        Assert.Equal((8f, 2.5f), garden.PowerBalance());
    }

    [Fact]
    public void BerriesRaiseAGrovesRate()
    {
        var (garden, grove) = WithAGrove();
        for (int i = 0; i < 3; i++) Add(garden, grove, NodeKind.Berry);
        Assert.Equal(Tuning.GroveFruitPerHour * 1.2f, Garden.FruitBlobsPerHour(grove), 1e-4f);
    }

    [Fact]
    public void GrovesAreLimitedOnlyByLinksAndPower()
    {
        // No cap: any free link takes one, whatever the stem and generator caps.
        var garden = NewGarden(new Capacity(1, 0, 1));
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        Add(garden, stem, NodeKind.Grove);
        Add(garden, stem, NodeKind.Grove);
        Assert.Null(garden.SproutFrom(stem, NodeKind.Grove)); // its 3 links are taken
    }

    // ==== FRUIT BLOBS ==== \\

    [Fact]
    public void AGroveSendsABlobThatGrowsAFruit()
    {
        var (garden, grove) = WithAGrove();
        garden.Grow(BlobHours * Hour); // one blob's worth
        var blob = Assert.Single(garden.Motes);
        Assert.False(blob.Homing);
        Assert.Equal(grove, blob.From);
        var route = blob.Route!;
        Assert.Equal(grove, route[0]);
        // The fruit it grows is there already (it counts from now), hidden until the blob reaches it.
        var fruit = route[^1];
        Assert.True(fruit.Spec.IsFruit);
        Assert.Equal(fruit.Parent!.Spec.Fruit, fruit.Spec.Kind);
        Assert.Equal(1, Fruit(garden));
        Assert.True(fruit.Age < 0);
        Assert.Equal(0, grove.FruitBlob, 1e-3f);
        // Each step of the walk is along a link.
        for (int i = 1; i < route.Count; i++)
            Assert.True(route[i].Parent == route[i - 1] || route[i - 1].Parent == route[i]);
    }

    [Fact]
    public void ABlobFollowsItsRouteAndTheFruitGrowsAsItArrives()
    {
        var (garden, _) = WithAGrove();
        garden.Grow(BlobHours * Hour);
        var blob = garden.Motes[0];
        var route = blob.Route!.ToList();
        var fruit = route[^1];
        var visited = new List<PlantNode> { blob.From };
        for (int tick = 0; tick < 30 * Tuning.TicksPerSecond && garden.Motes.Count > 0; tick++)
        {
            garden.Step();
            if (garden.Motes.Count > 0 && garden.Motes[0].From != visited[^1]) visited.Add(garden.Motes[0].From);
            // Hidden until the blob gets there.
            if (garden.Motes.Count > 0) Assert.True(fruit.Age <= 0.02f, $"fruit showing at {fruit.Age} with the blob on its way");
        }
        Assert.Empty(garden.Motes);
        Assert.Equal(route.SkipLast(1), visited);
        Assert.InRange(fruit.Age, -0.05f, 0.1f); // just starting to grow in
        Assert.Equal(1, fruit.Glow, 0.1f);
    }

    [Fact]
    public void GrovesFillTheRoomThenHoldTheirBlob()
    {
        var (garden, grove) = WithAGrove();
        garden.Grow(Day);
        // 12 blobs' worth in a day, room for 7: every part's fruit is grown, and the grove waits with a blob ready.
        Assert.Equal(7, Fruit(garden));
        Assert.All(garden.Nodes.Where(n => !n.Spec.IsFruit && n.Spec.Fruit != null), n => Assert.False(n.CanFruit));
        Assert.Equal(1, grove.FruitBlob, 1e-3f);

        // Room appears (a leaf pruned): the waiting blob leaves at the next step.
        var leaf = garden.Nodes.First(n => n.Spec.Kind == NodeKind.Leaf);
        garden.Prune(leaf);
        for (int i = 0; i < 100 && Fruit(garden) < 7; i++) garden.Grow(Tuning.EconomyStepSeconds);
        Assert.Equal(7, Fruit(garden));
    }

    [Fact]
    public void LeavesFromBlobsCutStemUpkeep()
    {
        var (garden, _) = WithAGrove();
        var (_, before) = garden.PowerBalance();
        garden.Grow(Day);
        var (_, after) = garden.PowerBalance();
        // Both stems carry two leaves: 0.125 each instead of 0.5.
        Assert.Equal(before - 2 * (0.5f - 0.125f), after, 1e-4f);
    }

    [Fact]
    public void AWiltingGroveMakesNoFruit()
    {
        var (garden, grove) = WithAGrove();
        grove.Power = 0;
        grove.Wilting = true;
        foreach (var n in garden.Nodes) if (n != grove) n.Power = 0;
        garden.Grow(Tuning.EconomyStepSeconds);
        Assert.True(grove.Wilting);
        Assert.Equal(0, grove.FruitBlob);
    }

    [Fact]
    public void CatchUpShowsNoBlobsButGrowsTheirFruit()
    {
        var (garden, _) = WithAGrove();
        garden.CatchUp(3 * BlobHours * Hour + 1);
        Assert.Empty(garden.Motes);
        Assert.InRange(Fruit(garden), 2, 3); // three blobs; a blob that wandered off can come back empty
        Assert.All(garden.Nodes, n => Assert.True(n.Age >= 0));
        Assert.True(garden.ShowBlobs);
    }

    [Fact]
    public void CatchUpGrowsTheSameFruitAsLivingThroughIt()
    {
        // The walk and its fruit are settled when a blob leaves, from the garden's own dice, not the physics, so a
        // catch-up and a live session (here in uneven chunks, with blobs shown) grow the same fruit in the same places.
        var caught = Planted(3);
        var live = Planted(3);
        caught.CatchUp(Tuning.CatchUpCapSeconds);
        for (int i = 0; i < Tuning.CatchUpCapSeconds / 7; i++) live.Grow(7);
        live.Grow(Tuning.CatchUpCapSeconds % 7);
        Assert.Contains(caught.Nodes, n => n.Spec.Kind == NodeKind.Grove);
        Assert.True(Fruit(caught) > 10);
        Assert.Equal(live.Nodes.Select(n => (n.Spec.Kind, n.Parent?.Id)), caught.Nodes.Select(n => (n.Spec.Kind, n.Parent?.Id)));
        Assert.Equal(live.Genes, caught.Genes, 1e-3);
    }

    [Fact]
    public void PruningTheRouteDropsTheBlob()
    {
        var (garden, grove) = WithAGrove();
        garden.Grow(BlobHours * Hour);
        Assert.Single(garden.Motes);
        garden.Prune(grove);
        Assert.Empty(garden.Motes);
    }

    [Fact]
    public void FruitBlobsSurviveSaveAndLoad()
    {
        var (garden, grove) = WithAGrove();
        garden.Grow(BlobHours * Hour / 2 + 3);
        Assert.InRange(grove.FruitBlob, 0.4f, 0.6f);
        var loaded = Garden.FromSave(garden.ToSave(), seed: 7);
        Assert.Equal(grove.FruitBlob, loaded.Nodes.Single(n => n.Spec.Kind == NodeKind.Grove).FruitBlob);
    }

    // ==== WILD GROWTH ==== \\

    static Garden Planted(int seed = 7)
    {
        var garden = Garden.New(Layout.Radial, seed);
        garden.Sprout();
        return garden;
    }

    [Fact]
    public void WithoutGrovesNoFruitGrows()
    {
        // Fruit is only grown by blobs now: until the first grove, a growing garden has none.
        var garden = Planted();
        int steps = 0;
        for (; steps < 3 * 24 * 6 && !garden.Nodes.Any(n => n.Spec.Kind == NodeKind.Grove); steps++)
        {
            garden.Grow(600);
            Assert.Equal(0, Fruit(garden));
        }
        Assert.True(garden.Nodes.Count(n => !n.Spec.IsFruit) > 4, "it grew meanwhile");
    }

    [Fact]
    public void WildGrowthKeepsGrovesFewAndUseful()
    {
        // Over a week a garden grows its groves, but only while there's fruit room for each (GroveFruitRoomEach).
        foreach (var seed in new[] { 1, 2, 3, 4 })
        {
            var garden = Planted(seed);
            garden.Grow(7 * Day);
            int groves = garden.Nodes.Count(n => n.Spec.Kind == NodeKind.Grove);
            Assert.InRange(groves, 1, 4);
            Assert.True(Fruit(garden) > 50, $"seed {seed}: {Fruit(garden)} fruit");
            Assert.DoesNotContain(garden.Nodes, n => n.Wilting && n.Spec.Kind == NodeKind.Grove);
        }
    }
}
