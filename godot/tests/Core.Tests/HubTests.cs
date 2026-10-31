using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceGarden.Core.Tests;

// The Hub (DESIGN.md, The Hub): its pool and points, buying a level and its effect, dormant once pruned and back once
// planted again, and the save. Minimal, as step 5 asks.
public class HubTests
{
    static PlantNode Add(Garden garden, PlantNode parent, NodeKind kind) =>
        garden.GrowBranch(parent, kind) ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

    // The widget's own save options (SaveFile.Options).
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // core - stem - lab, and a Hub on the stem: the Hub bought, nothing growing on its own.
    static (Garden Garden, PlantNode Stem, PlantNode Lab, PlantNode Hub) Make(int seed = 3)
    {
        var garden = Garden.New(Layout.Radial, seed);
        garden.PassiveGrowth = false;
        garden.Capacity = new Capacity(10, 4, 2, Hubs: 1);
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        var lab = Add(garden, stem, NodeKind.Lab);
        var hub = Add(garden, stem, NodeKind.Hub);
        return (garden, stem, lab, hub);
    }

    static void GrowFull(Garden garden, double seconds)
    {
        for (double t = 0; t < seconds; t += 1)
        {
            foreach (var n in garden.Nodes) n.Power = n.Store;
            garden.Grow(1);
        }
    }

    [Fact]
    public void TheHubIsPlantedOnceAndNeverGrowsWild()
    {
        var garden = Garden.New(Layout.Radial, 1);
        garden.PassiveGrowth = false;
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        Assert.False(garden.CanSproutFrom(stem, NodeKind.Hub)); // not bought yet
        garden.Capacity = garden.Capacity with { Hubs = 1, CoreLinks = 2, WildFlowers = 50 };
        Assert.True(garden.SproutFrom(stem, NodeKind.Hub));
        Assert.Equal(NodeKind.Hub, stem.Forming); // (with every wild flower slot free)
        var other = Add(garden, garden.Core, NodeKind.Stem);
        Assert.False(garden.CanSproutFrom(other, NodeKind.Hub)); // one at a time, a bud too
    }

    [Fact]
    public void GenesTheHubCatchesFillItsPoolAndTurnIntoPoints()
    {
        // core - hub - lab: every gene blob meets the Hub first.
        var garden = Garden.New(Layout.Radial, 5);
        garden.PassiveGrowth = false;
        garden.Capacity = new Capacity(10, 4, 1, Hubs: 1);
        var hub = Add(garden, garden.Core, NodeKind.Hub);
        Add(garden, hub, NodeKind.Lab);
        Assert.Contains(garden.Tracking.Journal, e => e.Text == "First Hub planted");
        GrowFull(garden, 3600);
        Assert.Equal(0, garden.Genes); // none reached the core's balance
        Assert.True(garden.HubGenes > 600);
        // 250 for the first point, 300 for the second, 350 ...
        double spent = 0;
        for (int i = 0; i < garden.HubPointsEarned; i++) spent += Tuning.HubFirstPoint + Tuning.HubPointStep * i;
        Assert.Equal(garden.HubGenes, spent + garden.HubPool, 6);
        Assert.True(garden.HubPool < garden.NextPointCost);
        Assert.True(garden.HubPointsEarned >= 2);
        Assert.Equal(garden.HubPointsEarned, garden.HubPoints);
    }

    [Fact]
    public void ALevelCostsPointsAndRaisesItsKindsMainEffect()
    {
        var (garden, _, lab, hub) = Make();
        float blobs = Garden.FullBlobsPerHour(lab), upkeep = Garden.FullUpkeepPerSecond(hub);
        Assert.False(garden.BuyHubLevel(NodeKind.Lab)); // no points
        garden.HubPoints = 3;
        Assert.False(garden.BuyHubLevel(NodeKind.Pulsar)); // not found in the garden: "???"
        Assert.True(garden.BuyHubLevel(NodeKind.Lab));
        Assert.Equal(2, garden.HubPoints);
        Assert.Equal(1, garden.HubLevel(NodeKind.Lab));
        Assert.Equal(blobs * 1.1f, Garden.FullBlobsPerHour(lab), 3);
        Assert.Equal(upkeep + Tuning.HubUpkeepPerLevel, Garden.FullUpkeepPerSecond(hub), 3); // the Hub's upkeep rises

        // A milestone level costs more, and adds its own: level 5's lab holds a research more.
        garden.HubLevels[NodeKind.Lab] = 4;
        garden.HubPoints = 1;
        Assert.False(garden.BuyHubLevel(NodeKind.Lab));
        garden.HubPoints = 2;
        int fruits = lab.MaxFruits;
        Assert.True(garden.BuyHubLevel(NodeKind.Lab));
        Assert.Equal(0, garden.HubPoints);
        Assert.Equal(fruits + 1, lab.MaxFruits);
        Assert.Equal(blobs * 1.5f, Garden.FullBlobsPerHour(lab), 3);
    }

    [Fact]
    public void PruningTheHubLeavesItsLevelsDormantUntilAnotherIsPlanted()
    {
        var (garden, stem, lab, hub) = Make();
        garden.HubPoints = 1;
        var generator = Add(garden, garden.Core, NodeKind.Generator);
        float gen = Garden.FullProductionPerSecond(generator);
        Assert.True(garden.BuyHubLevel(NodeKind.Generator));
        Assert.Equal(gen * 1.1f, Garden.FullProductionPerSecond(generator), 3);

        garden.Prune(hub);
        Assert.Equal(gen, Garden.FullProductionPerSecond(generator), 3); // dormant
        Assert.Equal(1, garden.HubLevel(NodeKind.Generator)); // but kept
        garden.HubPoints = 1;
        Assert.False(garden.CanBuyHubLevel(NodeKind.Generator)); // no Hub to buy at

        garden.ForgetPrune();
        Add(garden, stem, NodeKind.Hub);
        Assert.Equal(gen * 1.1f, Garden.FullProductionPerSecond(generator), 3); // back
        Assert.Equal(1f, lab.Boost.Blobs);
    }

    [Fact]
    public void TheHubSavesAndLoads()
    {
        var (garden, _, _, _) = Make();
        garden.HubPoints = 2;
        Assert.True(garden.BuyHubLevel(NodeKind.Stem));
        garden.HubPool = 120;
        garden.HubPointsEarned = 7;
        garden.HubGenes = 2_300;

        var json = JsonSerializer.Serialize(garden.ToSave(), Json);
        var loaded = Garden.FromSave(JsonSerializer.Deserialize<GardenSave>(json, Json)!, seed: 7);
        Assert.Equal((120.0, 1, 7, 2_300.0), (loaded.HubPool, loaded.HubPoints, loaded.HubPointsEarned, loaded.HubGenes));
        Assert.Equal(1, loaded.HubLevel(NodeKind.Stem));
        var stem = loaded.Nodes.First(n => n.Spec.Kind == NodeKind.Stem);
        Assert.Equal(1 + Tuning.HubBoost, stem.Boost.Transfer, 3); // working again, with the Hub loaded
        Assert.True(loaded.HubPlanted);
    }
}
