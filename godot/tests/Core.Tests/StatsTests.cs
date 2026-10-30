namespace SpaceGarden.Core.Tests;

// The garden card's numbers (GardenStats), the part card's (NodeStats), and each
// NodeSpec's Name and Description.
public class StatsTests
{
    static Garden NewGarden(Capacity capacity)
    {
        var garden = Garden.New(Layout.Radial, seed: 7);
        garden.Capacity = capacity;
        return garden;
    }

    static PlantNode Add(Garden garden, PlantNode parent, NodeKind kind) =>
        (NodeSpecs.All[kind].IsFruit ? garden.GrowFruit(parent) : garden.GrowBranch(parent, kind))
            ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

    // ==== NODE INFO ==== \\

    [Fact]
    public void EveryKindHasANameAndAOneLineDescription()
    {
        foreach (var kind in Enum.GetValues<NodeKind>())
        {
            Assert.True(NodeSpecs.All.TryGetValue(kind, out var info), $"{kind} has no spec");
            Assert.False(string.IsNullOrWhiteSpace(info.Name), $"{kind} has no name");
            Assert.False(string.IsNullOrWhiteSpace(info.Description), $"{kind} has no description");
            Assert.DoesNotContain('\n', info.Description);
            Assert.True(info.Description.Length <= 80, $"{kind}'s description is too long for one line");
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
        right.Forming = NodeKind.Lab; // a passive bud on its way
        foreach (var n in garden.Nodes) n.Power = n.Store / 2;

        var stats = GardenStats.Of(garden);
        Assert.Equal(4, stats.Stems);
        Assert.Equal(1, stats.Generators);
        Assert.Equal(1, stats.Groves);
        Assert.Equal(1, stats.Labs);
        Assert.Equal(1, stats.CoreLinksUsed);
        Assert.Equal(garden.Capacity, stats.Capacity);
        Assert.Equal(2, stats.Fruit);
        Assert.Equal(4 * 2 + 2 * 3 + 2, stats.FruitRoom); // 4 stems with room for 2 leaves, a lab and grove for 3, a generator for 2
        Assert.Equal(1, stats.BudsForming[NodeKind.Lab]);
        Assert.Equal(0.5f, stats.PowerFill, 4);
        Assert.Equal(garden.Nodes.Sum(Garden.ShownProductionPerSecond), stats.Production, 4);
        Assert.Equal(garden.PowerBalance().Upkeep, stats.Upkeep, 4);
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
    public void TheCheapestNextUpgradeIsTimedAtTheCurrentRate()
    {
        var garden = NewGarden(Capacity.Start);
        garden.Deliver(400);
        garden.Buy(Track.Stems); // 300 left; the next stem costs 150, generators and the core 1,500
        garden.Buy(Track.Stems); // 150 left; the next stem 200
        garden.Spend(50);
        var stats = GardenStats.Of(garden) with { GenesPerHour = 600 };
        Assert.Equal(new Upgrade(Track.Stems, UpgradeKind.Stem, 200), stats.Cheapest);
        Assert.Equal(100, stats.GeneBalance, 9);
        Assert.Equal(400, stats.Genes, 9);
        Assert.Equal(100, stats.GenesToNext, 9);
        Assert.Equal(600, stats.SecondsToNext!.Value, 9); // 100 genes at 10 a minute

        Assert.Null((stats with { GenesPerHour = 0 }).SecondsToNext); // no labs: never
        Assert.Equal(0, (stats with { GeneBalance = 250 }).SecondsToNext); // it can be bought now
        Assert.Null((stats with { Cheapest = null }).SecondsToNext); // every upgrade bought
    }

    // ==== PART CARD ==== \\

    [Fact]
    public void TheCoresCardShowsWhatItMakesAndSends()
    {
        var garden = NewGarden(new Capacity(4, 1, 2));
        Add(garden, garden.Core, NodeKind.Stem);
        garden.Core.Power = 300;
        var core = NodeStats.Of(garden.Core);
        Assert.Equal((300f, 800f), (core.Power, core.Store));
        Assert.Equal(24 * 300f / 800, core.Makes, 4); // in proportion to its fill
        Assert.Equal(28.8f, core.Sends, 4);
        Assert.Equal(0, core.Upkeep);
        garden.Core.Power = 800;
        Assert.Equal(24, NodeStats.Of(garden.Core).Makes, 4); // full, it makes nothing: the card shows its full rate
        garden.Core.Power = 900; // past full too, and the garden's Gen counts it the same
        Assert.Equal(24, NodeStats.Of(garden.Core).Makes, 4);
        Assert.Equal(24, GardenStats.Of(garden).Production, 4);
        Assert.Equal(0, garden.PowerBalance().Production, 4); // (though it really makes nothing)
        Assert.Equal((1, 2), (core.Links, core.MaxLinks)); // against the garden's core links
        Assert.Null(core.Forming);
    }

    [Fact]
    public void AStemsUpkeepCountsItsLeaves()
    {
        var garden = NewGarden(new Capacity(4, 1, 1));
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        Add(garden, stem, NodeKind.Stem);
        stem.Power = stem.Store; // full: upkeep follows the fill
        Assert.Equal(3, NodeStats.Of(stem).Upkeep, 4);
        stem.Power = stem.Store / 2;
        Assert.Equal(1.5f, NodeStats.Of(stem).Upkeep, 4);
        stem.Power = stem.Store;
        Add(garden, stem, NodeKind.Leaf);
        var card = NodeStats.Of(stem);
        Assert.Equal(2.25f, card.Upkeep, 4);
        Assert.Equal(0, card.Makes);
        Assert.Equal((2, 3), (card.Links, card.MaxLinks)); // its parent and its branch
    }

    [Fact]
    public void FlowersShowWhatTheyMakeNotWhatArrives()
    {
        var garden = NewGarden(new Capacity(4, 1, 1));
        var stem = Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Stem);
        var lab = Add(garden, stem, NodeKind.Lab);
        var grove = Add(garden, stem, NodeKind.Grove);
        foreach (var n in garden.Nodes) n.Power = n.Store; // full

        var labCard = NodeStats.Of(lab);
        Assert.Equal(720, labCard.GenesPerHour, 3); // a gene a blob, every 5 s, before any is lost on the way
        Assert.Equal(0, labCard.GrowthBlobsPerHour);
        Assert.Equal(6, labCard.Upkeep, 4);
        Assert.True(GardenStats.GenesPerHourFrom(garden, lab) < labCard.GenesPerHour); // what reaches the core
        lab.Power = lab.Store / 2;
        Assert.Equal(360, NodeStats.Of(lab).GenesPerHour, 3); // half full, half as many

        Assert.Equal(1200, NodeStats.Of(grove).GrowthBlobsPerHour, 3);
        Add(garden, grove, NodeKind.Berry);
        Assert.Equal(1320, NodeStats.Of(grove).GrowthBlobsPerHour, 3); // a berry adds a tenth
        Assert.Equal(0, NodeStats.Of(grove).GenesPerHour);
        Assert.Equal(1, NodeStats.Of(grove).Links);
    }

    [Fact]
    public void FormingShowsHowFarAlongItIs()
    {
        var garden = NewGarden(new Capacity(4, 1, 1));
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        stem.Forming = NodeKind.Lab;
        stem.Growth = Tuning.BudGrowth * 0.6f;
        var card = NodeStats.Of(stem);
        Assert.Equal(NodeKind.Lab, card.Forming);
        Assert.Equal(0.6f, card.FormingProgress, 4);

        stem.Forming = NodeKind.Leaf;
        stem.Growth = Tuning.FruitGrowth * 0.25f;
        Assert.Equal(0.25f, NodeStats.Of(stem).FormingProgress, 4);
    }

    // Pinning went in step 3d; a save from before it, with parts pinned, still loads.
    [Fact]
    public void AnOldSavesPinsAreIgnored()
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };
        var node = System.Text.Json.JsonSerializer.Deserialize<NodeSave>(
            """{"Id":3,"Kind":"Stem","ParentId":0,"X":1,"Y":2,"Pinned":true,"Power":50}""", options)!;
        Assert.Equal((3, NodeKind.Stem, 50f), (node.Id, node.Kind, node.Power));
    }
}
