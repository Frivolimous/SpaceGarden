namespace SpaceGarden.Core.Tests;

public class GroveTests
{
    const double Hour = 3600, Day = 24 * Hour;
    static readonly double BlobHours = 1 / Tuning.GroveBlobsPerHour;

    static Garden NewGarden(Capacity? capacity = null, int seed = 7)
    {
        var garden = Garden.New(Layout.Radial, seed);
        if (capacity != null) garden.Capacity = capacity;
        garden.Unlocks = Tuning.UnlockTrack.Length; // the track done: no raises
        return garden;
    }

    static PlantNode Add(Garden garden, PlantNode parent, NodeKind kind) =>
        (NodeSpecs.All[kind].IsFruit ? garden.GrowFruit(parent) : garden.SproutFrom(parent, kind))
            ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

    static int Fruit(Garden garden) => garden.Nodes.Count(n => n.Spec.IsFruit);
    static int Main(Garden garden) => garden.Nodes.Count(n => !n.Spec.IsFruit);

    // core - stem - stem - grove, full of power, no wild buds: room for 7 fruit (2 leaves a stem, 3 berries).
    static (Garden Garden, PlantNode Grove) WithAGrove(int seed = 7)
    {
        var garden = NewGarden(new Capacity(2, 0, 1), seed);
        garden.WildGrowth = false;
        var grove = Add(garden, Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Stem), NodeKind.Grove);
        foreach (var n in garden.Nodes) n.Power = n.Store;
        return (garden, grove);
    }

    // Grows one blob at a time (the grove kept full), until what's asked for has happened.
    static void GrowUntil(Garden garden, Func<bool> done, int maxBlobs = 2000)
    {
        for (int i = 0; i < maxBlobs && !done(); i++)
        {
            foreach (var n in garden.Nodes) n.Power = n.Store;
            foreach (var n in garden.Nodes.Where(n => n.Spec.Kind == NodeKind.Grove)) n.Blob = 1;
            garden.Grow(Tuning.EconomyStepSeconds);
        }
        Assert.True(done());
    }

    // ==== RATES ==== \\

    [Fact]
    public void AGroveCostsAsMuchAsALab()
    {
        var (_, grove) = WithAGrove();
        Assert.Equal(6, Garden.FullUpkeepPerSecond(grove));
        Assert.Equal(Tuning.LabUpkeepPerSecond, Garden.FullUpkeepPerSecond(grove));
    }

    [Fact]
    public void AGrovesOutputFollowsItsPower()
    {
        var (_, grove) = WithAGrove();
        Assert.Equal(Tuning.GroveBlobsPerHour, Garden.BlobsPerHour(grove), 1e-4f);
        grove.Power = grove.Store / 4;
        Assert.Equal(Tuning.GroveBlobsPerHour / 4, Garden.BlobsPerHour(grove), 1e-4f);
        grove.Power = 0;
        Assert.Equal(0, Garden.BlobsPerHour(grove));
    }

    [Fact]
    public void BerriesRaiseAGrovesRate()
    {
        var (garden, grove) = WithAGrove();
        for (int i = 0; i < 3; i++) Add(garden, grove, NodeKind.Berry);
        Assert.Equal(Tuning.GroveBlobsPerHour * 1.2f, Garden.BlobsPerHour(grove), 1e-4f);
    }

    [Fact]
    public void GrovesAreLimitedOnlyByLinks()
    {
        // No cap: any free link takes one, whatever the stem and generator caps.
        var garden = NewGarden(new Capacity(1, 0, 1));
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        Add(garden, stem, NodeKind.Grove);
        Add(garden, stem, NodeKind.Grove);
        Assert.Null(garden.SproutFrom(stem, NodeKind.Grove)); // its 3 links are taken
    }

    [Fact]
    public void ManualBudsNeverGrowFruit()
    {
        var (garden, grove) = WithAGrove();
        Assert.False(garden.CanSproutFrom(grove, NodeKind.Stem)); // a grove's only link is its parent's
        Assert.Null(garden.SproutFrom(grove, NodeKind.Berry));
        Assert.Null(garden.SproutFrom(grove.Parent!, NodeKind.Leaf));
        Assert.Equal(0, Fruit(garden));
    }

    // ==== GROWTH BLOBS ==== \\

    [Fact]
    public void AGroveSendsBlobsOnARandomWalk()
    {
        var (garden, grove) = WithAGrove();
        garden.Grow(BlobHours * Hour * 1.5); // one blob's worth
        var blob = Assert.Single(garden.Motes);
        Assert.Equal(grove, blob.From);
        Assert.Equal(grove, blob.Route[0]);
        Assert.Equal(Tuning.BlobGrowth, blob.Growth, 1e-6f);
        // Each step of the walk is along a link, between main parts.
        for (int i = 1; i < blob.Route.Count; i++)
        {
            var (a, b) = (blob.Route[i - 1], blob.Route[i]);
            Assert.True(b.Parent == a || a.Parent == b);
            Assert.False(b.Spec.IsFruit);
        }
    }

    [Fact]
    public void APartWithNothingFormingAbsorbsAThirdOfTheBlobs()
    {
        // core - stem - stem - grove: a blob's walk passes the stems, each with leaf room, at most a few times
        // each before it fades. With nothing forming anywhere, each pass absorbs it 30% of the time.
        int absorbed = 0, blobs = 0;
        for (int seed = 0; seed < 300; seed++)
        {
            var (garden, grove) = WithAGrove(seed);
            var stem = grove.Parent!;
            grove.Blob = 1;
            garden.Grow(Tuning.EconomyStepSeconds);
            var route = garden.Motes[0].Route;
            if (route[1] != stem) continue;
            blobs++; // the first part it reaches is the stem
            if (route.Count == 2) absorbed++;
        }
        Assert.InRange(absorbed / (double)blobs, 0.22, 0.38);
    }

    [Fact]
    public void FormingFruitTakesEveryBlobAndCompletesAtOne()
    {
        var (garden, grove) = WithAGrove();
        var stem = grove.Parent!;
        GrowUntil(garden, () => stem.Forming != null);
        Assert.Equal(NodeKind.Leaf, stem.Forming); // WildGrowth off: never a bud
        Assert.True(stem.FormingFruit);
        Assert.Equal(Tuning.BlobGrowth, stem.Growth, 1e-5f);

        // From now on every blob that reaches it stays.
        for (int i = 0; i < 30 && stem.Forming != null; i++)
        {
            float before = stem.Growth;
            grove.Blob = 1;
            garden.Grow(Tuning.EconomyStepSeconds);
            if (garden.Motes[^1].Route.Contains(stem))
                Assert.True(stem.Forming == null || stem.Growth > before);
        }
        GrowUntil(garden, () => stem.Fruits.Count == 1);
        Assert.Equal(NodeKind.Leaf, stem.Fruits[0].Spec.Kind);
        // The next one starts afresh.
        Assert.True(stem.Forming == null || stem.Growth <= 2 * Tuning.BlobGrowth + 1e-5f);
    }

    [Fact]
    public void FreshAbsorbsRollBudsWhereABranchCanGrow()
    {
        // With wild buds on and room under the caps, fresh absorbs start buds as well as fruit; a bud reserves its
        // link and its cap slot while it forms.
        var garden = NewGarden(new Capacity(3, 0, 1));
        var grove = Add(garden, Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Stem), NodeKind.Grove);
        var stem = grove.Parent!;
        GrowUntil(garden, () => garden.Nodes.Any(n => n.FormingBud));
        var budding = garden.Nodes.Single(n => n.FormingBud);
        var kind = budding.Forming!.Value;
        Assert.Contains(kind, new[] { NodeKind.Stem, NodeKind.Lab, NodeKind.Grove }); // no generator room
        Assert.False(budding.CanBranch); // its one free link is held
        if (kind == NodeKind.Stem) Assert.False(garden.CanSproutFrom(stem, NodeKind.Stem) || garden.CanSproutFrom(budding, NodeKind.Stem));
        var children = budding.Branches.ToList();
        GrowUntil(garden, () => !budding.FormingBud);
        var grown = Assert.Single(budding.Branches.Except(children));
        Assert.Equal(kind, grown.Spec.Kind);
    }

    [Fact]
    public void AFullPartNeverAbsorbs()
    {
        // Every part's fruit grown and no room for a branch: blobs wander the whole walk and fade.
        var (garden, grove) = WithAGrove();
        GrowUntil(garden, () => Fruit(garden) == 7);
        garden.Motes.Clear();
        grove.Blob = 1;
        garden.Grow(Tuning.EconomyStepSeconds);
        var blob = Assert.Single(garden.Motes);
        Assert.Equal(Tuning.BlobMaxHops + 1, blob.Route.Count);
        Assert.DoesNotContain(garden.Nodes, n => n.Forming != null);
    }

    [Fact]
    public void ABlobFollowsItsRouteAndThePartItCompletesAppearsAsItArrives()
    {
        var (garden, grove) = WithAGrove();
        GrowUntil(garden, () => garden.Nodes.Any(n => n.Forming is { } && n.Growth >= Tuning.FruitGrowth - 0.15f));
        var forming = garden.Nodes.Single(n => n.Forming != null);
        int fruit = Fruit(garden);
        garden.Motes.Clear();
        GrowUntil(garden, () => Fruit(garden) > fruit);
        var mote = garden.Motes.Single(m => m.Completes);
        var grown = garden.Nodes.Last();
        Assert.Equal(forming, grown.Parent);
        Assert.Equal(forming, mote.Route[^1]);
        Assert.True(grown.Age < 0); // hidden until the blob gets there

        var route = mote.Route.ToList();
        var visited = new List<PlantNode> { mote.From };
        garden.Motes.RemoveAll(m => m != mote);
        for (int tick = 0; tick < 30 * Tuning.TicksPerSecond && garden.Motes.Count > 0; tick++)
        {
            garden.Step();
            if (garden.Motes.Count > 0 && garden.Motes[0].From != visited[^1]) visited.Add(garden.Motes[0].From);
            if (garden.Motes.Count > 0) Assert.True(grown.Age <= 0.02f, $"showing at {grown.Age} with the blob on its way");
        }
        Assert.Empty(garden.Motes);
        Assert.Equal(route.SkipLast(1), visited);
        Assert.InRange(grown.Age, -0.05f, 0.1f); // just starting to grow in
        Assert.Equal(0, forming.FormingLook); // and the bud it came from is gone
    }

    [Fact]
    public void LeavesFromBlobsCutStemUpkeep()
    {
        var (garden, _) = WithAGrove();
        var (_, before) = garden.FullPowerBalance();
        GrowUntil(garden, () => Fruit(garden) == 7);
        var (_, after) = garden.FullPowerBalance();
        // Both stems carry two leaves: 0.75 a second each instead of 3.
        Assert.Equal(before - 2 * (3 - 0.75f), after, 1e-4f);
    }

    [Fact]
    public void AnEmptyGroveMakesNoBlobs()
    {
        var (garden, grove) = WithAGrove();
        foreach (var n in garden.Nodes) n.Power = 0;
        garden.Grow(Tuning.EconomyStepSeconds);
        Assert.Equal(0, grove.Blob);
    }

    [Fact]
    public void CatchUpShowsNoBlobsButDoesTheirWork()
    {
        var (garden, _) = WithAGrove();
        garden.CatchUp(Day);
        Assert.Empty(garden.Motes);
        Assert.True(Fruit(garden) >= 3);
        Assert.All(garden.Nodes, n => Assert.True(n.Age >= 0));
        Assert.All(garden.Nodes, n => Assert.Equal(n.Growth, n.FormingLook));
        Assert.True(garden.ShowBlobs);
    }

    static Garden Planted(int seed = 7)
    {
        var garden = Garden.New(Layout.Radial, seed);
        garden.Sprout();
        return garden;
    }

    [Fact]
    public void CatchUpGrowsTheSameAsLivingThroughIt()
    {
        // The walk and its work are settled when a blob leaves, from the garden's own dice, not the physics, so a
        // catch-up and a live session (here in uneven chunks, with blobs shown) grow the same parts in the same places.
        var caught = Planted(3);
        var live = Planted(3);
        foreach (var g in new[] { caught, live })
        {
            Add(g, Add(g, g.Nodes[1], NodeKind.Stem), NodeKind.Grove);
            Add(g, g.Nodes[1], NodeKind.Lab);
        }
        caught.CatchUp(Tuning.CatchUpCapSeconds);
        for (int i = 0; i < (int)(Tuning.CatchUpCapSeconds / 7); i++) live.Grow(7);
        live.Grow(Tuning.CatchUpCapSeconds % 7);
        Assert.True(Fruit(caught) > 8);
        Assert.True(Main(caught) > 6);
        Assert.Equal(live.Nodes.Select(n => (n.Spec.Kind, n.Parent?.Id, n.Forming, n.Growth)),
            caught.Nodes.Select(n => (n.Spec.Kind, n.Parent?.Id, n.Forming, n.Growth)));
        Assert.Equal(live.Genes, caught.Genes, 1e-3);
    }

    [Fact]
    public void PruningTheRouteDropsTheBlob()
    {
        var (garden, grove) = WithAGrove();
        garden.Grow(BlobHours * Hour * 1.5);
        Assert.Single(garden.Motes);
        garden.Prune(grove);
        Assert.Empty(garden.Motes);
    }

    [Fact]
    public void BlobsAndFormingSurviveSaveAndLoad()
    {
        var (garden, grove) = WithAGrove();
        GrowUntil(garden, () => garden.Nodes.Any(n => n.Forming != null));
        garden.Grow(BlobHours * Hour / 2);
        var loaded = Garden.FromSave(garden.ToSave(), seed: 7);
        for (int i = 0; i < garden.Nodes.Count; i++)
        {
            Assert.Equal(garden.Nodes[i].Blob, loaded.Nodes[i].Blob);
            Assert.Equal(garden.Nodes[i].Forming, loaded.Nodes[i].Forming);
            Assert.Equal(garden.Nodes[i].Growth, loaded.Nodes[i].Growth);
        }
    }

    // ==== WILD GROWTH ==== \\

    [Fact]
    public void WithoutAGroveNothingGrows()
    {
        // No core trickle: a garden nobody plants a grove in stays as it was planted, whatever its power.
        var garden = Planted();
        Assert.Equal(0, garden.Grow(3 * Day));
        Assert.Equal(2, garden.Nodes.Count);
    }

    [Fact]
    public void WildGrowthIgnoresPower()
    {
        // The plant is stupid: with one grove planted and left alone it grows whatever it rolls, labs that drain it
        // and more groves included, however low that leaves it.
        foreach (var seed in new[] { 1, 2, 3, 4 })
        {
            var garden = NewGarden(new Capacity(40, 2, 1), seed);
            var stem = Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Stem);
            Add(garden, stem, NodeKind.Grove);
            garden.Grow(Day);
            Assert.True(garden.Nodes.Count(n => n.Spec.Kind == NodeKind.Grove) >= 1);
            Assert.True(Main(garden) > 6, $"seed {seed}: {Main(garden)} main parts");
            Assert.True(Fruit(garden) > 5, $"seed {seed}: {Fruit(garden)} fruit");
        }
    }
}
