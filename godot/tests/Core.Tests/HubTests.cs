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

    // From the stem a gene blob goes to the core or the Hub, half each: half the lab's genes find the Hub, which sets
    // when its next point comes.
    [Fact]
    public void TheHubsGeneRateIsItsShareOfTheLabsBlobs()
    {
        var (garden, _, lab, _) = Make();
        var (core, hub) = GardenStats.GeneReaches(garden, lab);
        Assert.Equal(0.5, core, 12);
        Assert.Equal(0.5, hub, 12);
        Assert.Equal(Garden.BlobsPerHour(lab) * Tuning.BlobGenes * 0.5, GardenStats.HubGenesPerHour(garden), 9);
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
        Assert.Contains(garden.Tracking.Journal, e => e.Text == "First Hub grown");
        GrowFull(garden, 600);
        Assert.Equal(0, garden.Genes); // none reached the core's balance
        Assert.True(garden.HubGenes > 50);
        garden.CatchAtHub(10_000);
        // 3,000 for the first point, 3,400 for the second, 3,800 ...
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
        Assert.Equal(blobs * 1.2f, Garden.FullBlobsPerHour(lab), 3);
        Assert.Equal(upkeep + Tuning.HubUpkeepPerLevel, Garden.FullUpkeepPerSecond(hub), 3); // the Hub's upkeep rises

        // A milestone level costs more, and adds its own: level 3's faded gene blobs give the core a quarter (Nov 3).
        Assert.Equal(0, garden.FadedGenes);
        garden.HubLevels[NodeKind.Lab] = 2;
        garden.HubPoints = 1;
        Assert.False(garden.BuyHubLevel(NodeKind.Lab));
        garden.HubPoints = 2;
        Assert.True(garden.BuyHubLevel(NodeKind.Lab));
        Assert.Equal(0, garden.HubPoints);
        Assert.Equal(Tuning.HubFadedGenes3, garden.FadedGenes);
        Assert.Equal(blobs * 1.6f, Garden.FullBlobsPerHour(lab), 3);
        garden.HubLevels[NodeKind.Lab] = 5;
        Assert.Equal(Tuning.HubFadedGenes5, garden.FadedGenes);
    }

    [Fact]
    public void ThePathsCost92PointsAndTheHubsOwnAddsLinks()
    {
        Assert.Equal(92, Hubs.TotalPoints); // (96 with the retired Nebula Bloom's path)
        Assert.Equal(19, Hubs.Paths.Count);
        var (garden, _, _, hub) = Make();
        garden.HubPoints = 4;
        Assert.Equal(3, hub.MaxLinks);
        Assert.True(garden.BuyHubLevel(NodeKind.Hub));
        Assert.True(garden.BuyHubLevel(NodeKind.Hub));
        Assert.True(garden.BuyHubLevel(NodeKind.Hub));
        Assert.Equal(6, hub.MaxLinks);
        Assert.False(garden.BuyHubLevel(NodeKind.Hub)); // its top level
        // Its level 3 cuts what each level adds to its upkeep by a fifth.
        Assert.Equal(Tuning.HubUpkeep + 3 * Tuning.HubUpkeepPerLevel * Tuning.HubUpkeepCut, Garden.FullUpkeepPerSecond(hub), 3);
    }

    [Fact]
    public void AWildFlowersLevelsBoostItsFamilyToo()
    {
        var (garden, stem, _, _) = Make();
        garden.Capacity = garden.Capacity with { WildFlowers = 1 };
        Assert.NotNull(garden.GrowWild(NodeKind.Tendril, garden.Core));
        garden.HubPoints = 1;
        Assert.True(garden.BuyHubLevel(NodeKind.Tendril));
        Assert.Equal(1.1f, stem.Boost.Transfer, 3); // stems +10%
        Assert.Equal(1.3f, garden.Nodes.First(n => n.Spec.Kind == NodeKind.Tendril).Boost.Transfer, 3);
    }

    [Fact]
    public void GeneBlobsPassThroughACompleteHub()
    {
        var garden = Garden.New(Layout.Radial, 5);
        garden.PassiveGrowth = false;
        garden.Capacity = new Capacity(10, 4, 1, Hubs: 1);
        var hub = Add(garden, garden.Core, NodeKind.Hub);
        Add(garden, hub, NodeKind.Lab);
        foreach (var path in Hubs.Paths) garden.HubLevels[path.Kind] = path.MaxLevel;
        garden.HubLevels[NodeKind.Hub] = 2;
        garden.HubPoints = 2;
        Assert.True(garden.BuyHubLevel(NodeKind.Hub));
        Assert.True(garden.HubComplete);
        Assert.False(garden.CatchesGenes(hub)); // it takes no more in
        GrowFull(garden, 600);
        Assert.True(garden.Genes > 50);
        Assert.Equal(0, garden.HubGenes); // they went on to the core
    }

    [Fact]
    public void PruningTheHubLeavesItsLevelsDormantUntilAnotherIsPlanted()
    {
        var (garden, stem, lab, hub) = Make();
        garden.HubPoints = 1;
        var generator = Add(garden, garden.Core, NodeKind.Generator);
        float gen = Garden.FullProductionPerSecond(generator);
        Assert.True(garden.BuyHubLevel(NodeKind.Generator));
        Assert.Equal(gen * 1.2f, Garden.FullProductionPerSecond(generator), 3);

        garden.Prune(hub);
        Assert.Equal(gen, Garden.FullProductionPerSecond(generator), 3); // dormant
        Assert.Equal(1, garden.HubLevel(NodeKind.Generator)); // but kept
        garden.HubPoints = 1;
        Assert.False(garden.CanBuyHubLevel(NodeKind.Generator)); // no Hub to buy at

        garden.ForgetPrune();
        Add(garden, stem, NodeKind.Hub);
        Assert.Equal(gen * 1.2f, Garden.FullProductionPerSecond(generator), 3); // back
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
        Assert.Equal(1.2f, stem.Boost.Transfer, 3); // working again, with the Hub loaded
        Assert.True(loaded.HubPlanted);
    }

    // Respec (Nov 3): every path back to 0, the points refunded but HubRespecCost.
    [Fact]
    public void ARespecResetsEveryPathForAPointLess()
    {
        var (garden, _, _, _) = Make();
        garden.Tracking.Discovered[NodeKind.Lab] = 0;
        garden.Tracking.Discovered[NodeKind.Stem] = 0;
        garden.HubPoints = 10;
        Assert.True(garden.BuyHubLevel(NodeKind.Lab));
        Assert.True(garden.BuyHubLevel(NodeKind.Lab));
        Assert.True(garden.BuyHubLevel(NodeKind.Stem));
        int left = garden.HubPoints;
        Assert.True(garden.RespecHub());
        Assert.Equal(0, garden.HubLevelsBought);
        Assert.Equal(10 - Tuning.HubRespecCost, garden.HubPoints);
        Assert.True(left < garden.HubPoints);
        Assert.False(garden.RespecHub()); // nothing to take back
    }

    // Nov 3: the Buffer's path makes every buff 10% stronger a level and, at 3, 10 s longer; the Comet Bloom's keeps it
    // active 2 minutes longer a level and, at 3, halves its fading; the Moonflower's adds 5% a level to its bonus.
    [Fact]
    public void TheSpecialistsPathsActOnTheirEffects()
    {
        var (garden, _, _, hub) = Make();
        garden.Capacity = garden.Capacity with { WildFlowers = 2 };
        var branch = Add(garden, hub, NodeKind.Stem);
        var comet = Add(garden, branch, NodeKind.CometBloom);
        foreach (var kind in new[] { NodeKind.Buffer, NodeKind.CometBloom, NodeKind.Moonflower })
            garden.Tracking.Discovered[kind] = 0;
        garden.HubPoints = 12;
        double life = Garden.LifeOf(comet), active = Garden.ActiveOf(comet);
        for (int i = 0; i < 3; i++)
        {
            Assert.True(garden.BuyHubLevel(NodeKind.CometBloom));
            Assert.True(garden.BuyHubLevel(NodeKind.Buffer));
        }
        Assert.Equal(active + 3 * 120, Garden.ActiveOf(comet), 3);
        Assert.Equal(Garden.ActiveOf(comet) + (life - active) * 0.5, Garden.LifeOf(comet), 3);

        // A buff now: +10 points a level, 10 s longer (and twice as long while the Comet is active).
        garden.Grow(Tuning.EconomyStepSeconds);
        Assert.True(garden.CometActive);
        var generator = Add(garden, branch, NodeKind.Generator);
        garden.BuffForTest(generator);
        Assert.Equal(Tuning.BuffBonus + 0.3f, generator.BuffBonus, 4); // 150% + 30 points: 180%
        Assert.Equal((Tuning.BuffSeconds + Tuning.HubBuffSeconds) * Tuning.CometBuffTime, generator.Buff, 4);
    }

    // Nov 3: the stem's level 3 adds 150 to a stem's max power, flat.
    [Fact]
    public void TheStemsLevelThreeAddsMaxPower()
    {
        var (garden, stem, _, _) = Make();
        float store = stem.Store;
        garden.HubLevels[NodeKind.Stem] = 2;
        garden.HubPoints = 2;
        garden.Tracking.Discovered[NodeKind.Stem] = 0;
        Assert.True(garden.BuyHubLevel(NodeKind.Stem)); // level 3
        Assert.Equal(store + Tuning.HubStemStore, stem.Store, 3);
    }

    // Nov 3: the Wild Lab's level 3 adds 100 max power; the Lichen's, with all 4 leaves, makes 3 power a second and
    // sends at 3; the Seed Pod's makes the buds its blobs start twice as likely to grow wild.
    [Fact]
    public void TheWildLabLichenAndSeedPodMilestones()
    {
        var (garden, _, _, hub) = Make();
        garden.Capacity = garden.Capacity with { WildFlowers = 2 };
        var branch = Add(garden, hub, NodeKind.Stem);
        var wildLab = Add(garden, branch, NodeKind.WildLab);
        var lichen = Add(garden, branch, NodeKind.Lichen);
        float store = wildLab.Store;
        foreach (var kind in new[] { NodeKind.WildLab, NodeKind.Lichen, NodeKind.SeedPod }) garden.Tracking.Discovered[kind] = 0;
        garden.HubPoints = 12;
        for (int i = 0; i < 3; i++)
        {
            Assert.True(garden.BuyHubLevel(NodeKind.WildLab));
            Assert.True(garden.BuyHubLevel(NodeKind.Lichen));
        }
        Assert.Equal(store + Tuning.HubWildLabStore, wildLab.Store, 3);

        foreach (var n in garden.Nodes) n.Power = n.Store;
        Assert.Equal(0, Garden.FullProductionPerSecond(lichen)); // its leaves aren't out yet
        while (lichen.CanFruit) garden.GrowFruit(lichen);
        Assert.True(Garden.FullProductionPerSecond(lichen) >= Tuning.HubLichenGen);
        Assert.Equal(Tuning.HubLichenGen, Garden.TransferPerSecond(lichen), 3);
    }
}
