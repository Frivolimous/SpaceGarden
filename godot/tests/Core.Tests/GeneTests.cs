using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceGarden.Core.Tests;

// Step 5a's groundwork (DESIGN.md, Step 5): the gene balance, spent apart from the genes earned, and the garden's
// size, the most main nodes it has reached, both through a save.
public class GeneTests
{
    static Garden NewGarden(Capacity? capacity = null)
    {
        var garden = Garden.New(Layout.Radial, seed: 7);
        if (capacity != null) garden.Capacity = capacity;
        return garden;
    }

    static PlantNode Add(Garden garden, PlantNode parent, NodeKind kind) =>
        (NodeSpecs.All[kind].IsFruit ? garden.GrowFruit(parent) : garden.GrowBranch(parent, kind))
            ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

    // The widget's own save options (SaveFile.Options).
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // ==== GENE BALANCE ==== \\

    [Fact]
    public void GenesReachingTheCoreAddToTheBalanceAndToGenesEarned()
    {
        var garden = NewGarden();
        Assert.Equal(0, garden.GeneBalance);
        garden.Deliver(120);
        garden.Deliver(30);
        Assert.Equal(150, garden.Genes);
        Assert.Equal(150, garden.GeneBalance);
    }

    [Fact]
    public void SpendingTakesFromTheBalanceOnly()
    {
        var garden = NewGarden();
        garden.Deliver(150);
        Assert.True(garden.CanSpend(100));
        Assert.True(garden.Spend(100));
        Assert.Equal(50, garden.GeneBalance);
        Assert.Equal(150, garden.Genes); // genes earned never drop

        // More genes add to what's left.
        garden.Deliver(25);
        Assert.Equal(75, garden.GeneBalance);
        Assert.Equal(175, garden.Genes);
    }

    [Fact]
    public void SpendingMoreThanTheBalanceDoesNothing()
    {
        var garden = NewGarden();
        garden.Deliver(99);
        Assert.False(garden.CanSpend(100));
        Assert.False(garden.Spend(100));
        Assert.Equal(99, garden.GeneBalance);

        // The whole balance can be spent, to the last gene; a negative cost never adds to it.
        Assert.True(garden.Spend(99));
        Assert.Equal(0, garden.GeneBalance);
        Assert.False(garden.Spend(-10));
        Assert.Equal(0, garden.GeneBalance);
    }

    [Fact]
    public void TheUnlockTrackStillCountsGenesEarned()
    {
        // Until step 5b replaces it: spending the balance takes no unlock away and holds none back.
        var garden = NewGarden();
        var first = Tuning.UnlockTrack[0];
        garden.Deliver(first.Genes - 1);
        Assert.True(garden.Spend(first.Genes - 1));
        garden.Deliver(1);
        Assert.Equal(1, garden.Unlocks);
        Assert.Equal(1, garden.GeneBalance);
    }

    [Fact]
    public void LabsFillTheBalanceAsTheyDoGenesEarned()
    {
        var garden = NewGarden(new Capacity(4, 1, 1));
        garden.PassiveGrowth = false;
        Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Lab);
        foreach (var n in garden.Nodes) n.Power = n.Store;
        garden.Grow(3600);
        Assert.True(garden.Genes > 0);
        Assert.Equal(garden.Genes, garden.GeneBalance);
    }

    // ==== GARDEN SIZE ==== \\

    [Fact]
    public void TheGardenSizeCountsMainNodesNotFruit()
    {
        var garden = NewGarden(new Capacity(4, 1, 1));
        Assert.Equal(1, garden.Tracking.MostMainNodes); // the core
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        Add(garden, stem, NodeKind.Lab);
        Assert.Equal(3, garden.Tracking.MostMainNodes);
        Add(garden, stem, NodeKind.Leaf);
        Add(garden, stem, NodeKind.Leaf);
        Assert.Equal(3, garden.Tracking.MostMainNodes);
        Assert.Equal(5, garden.Tracking.MostParts);
    }

    [Fact]
    public void TheGardenSizeNeverDropsOnAPrune()
    {
        var garden = NewGarden(new Capacity(6, 1, 1));
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        var branch = Add(garden, stem, NodeKind.Stem);
        Add(garden, Add(garden, branch, NodeKind.Stem), NodeKind.Lab);
        Assert.Equal(5, garden.Tracking.MostMainNodes);

        garden.Prune(branch);
        Assert.Equal(2, garden.Nodes.Count);
        Assert.Equal(5, garden.Tracking.MostMainNodes);

        // An undo puts back no more than there was; regrowing counts again only past the old most.
        garden.UndoPrune();
        Assert.Equal(5, garden.Tracking.MostMainNodes);
        garden.Prune(branch);
        garden.ForgetPrune();
        Add(garden, stem, NodeKind.Lab);
        Assert.Equal(5, garden.Tracking.MostMainNodes);
        var again = Add(garden, stem, NodeKind.Stem);
        Add(garden, again, NodeKind.Stem);
        Add(garden, again, NodeKind.Grove);
        Assert.Equal(6, garden.Tracking.MostMainNodes);
    }

    // ==== SAVES ==== \\

    // Through the save's JSON (version 9) and back: the balance (less than genes earned once some are spent) and the
    // garden size (more than the parts now once some are pruned).
    [Fact]
    public void TheBalanceAndGardenSizeSurviveTheSave()
    {
        var garden = NewGarden(new Capacity(6, 1, 1));
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        var branch = Add(garden, stem, NodeKind.Stem);
        Add(garden, branch, NodeKind.Lab);
        garden.Prune(branch);
        garden.Deliver(500);
        garden.Spend(320);

        var json = JsonSerializer.Serialize(garden.ToSave(), Json);
        var loaded = Garden.FromSave(JsonSerializer.Deserialize<GardenSave>(json, Json)!, seed: 7);
        Assert.Equal(500, loaded.Genes);
        Assert.Equal(180, loaded.GeneBalance);
        Assert.Equal(4, loaded.Tracking.MostMainNodes);
        Assert.Equal(2, loaded.Nodes.Count); // loading the parts doesn't count them again

        // And it carries on: spending what was saved, and growing past the old most.
        Assert.True(loaded.Spend(180));
        Assert.Equal(0, loaded.GeneBalance);
        Assert.Equal(500, loaded.Genes);
    }

    [Fact]
    public void TheTotalsExportTheBalanceAndGardenSize()
    {
        var garden = NewGarden();
        garden.Deliver(40);
        garden.Spend(15);
        var csv = garden.Tracking.TotalsCsv(garden);
        Assert.Contains("genes_earned,40\n", csv);
        Assert.Contains("gene_balance,25\n", csv);
        Assert.Contains("most_main_nodes,1\n", csv);
        Assert.Contains("passive_buds,0\n", csv);
        Assert.DoesNotContain("wild", csv);
    }
}
