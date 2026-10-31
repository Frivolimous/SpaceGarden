using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceGarden.Core.Tests;

// Seedlings and evolutions (DESIGN.md, Seedlings and evolutions; step 5c): the fill and the choice, the target, the
// bloom, an unlock, repeats and the save. Minimal, as step 5 asks.
public class SeedlingTests
{
    static PlantNode Add(Garden garden, PlantNode parent, NodeKind kind) =>
        garden.GrowBranch(parent, kind) ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

    // The widget's own save options (SaveFile.Options).
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // A seedling's store before its choice: full, it offers its evolutions.
    static float Choice => NodeSpecs.All[NodeKind.Seedling].PowerStore;

    // core - stem - seedling, the core with room for a second branch, nothing growing on its own.
    static (Garden Garden, PlantNode Stem, PlantNode Seedling) Make(int seed = 3)
    {
        var garden = Garden.New(Layout.Radial, seed);
        garden.PassiveGrowth = false;
        garden.Capacity = new Capacity(10, 4, 2);
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        var seedling = Add(garden, stem, NodeKind.Seedling);
        return (garden, stem, seedling);
    }

    // Keeps every part but the seedlings full while time passes, so they're fed as fast as their neighbours can send.
    static void GrowFull(Garden garden, double seconds)
    {
        for (double t = 0; t < seconds; t += 1)
        {
            foreach (var n in garden.Nodes)
                if (!n.IsSeedling) n.Power = n.Store;
            garden.Grow(1);
        }
    }

    [Fact]
    public void ASeedlingTakesASlotFillsToTheChoiceAndWaits()
    {
        var (garden, stem, seedling) = Make();
        Assert.Equal(1, garden.SeedlingsUsed);
        Assert.False(garden.CanSproutFrom(stem, NodeKind.Seedling)); // its one slot is taken
        Assert.Equal(0, Garden.FullUpkeepPerSecond(seedling)); // it costs nothing
        Assert.Equal(0, Garden.TransferPerSecond(seedling)); // and sends nothing on

        GrowFull(garden, 2);
        Assert.True(seedling.Power > 0);
        Assert.False(seedling.Choosing);
        GrowFull(garden, 120);
        Assert.True(seedling.Power >= Choice - 1e-3f);
        Assert.True(seedling.Choosing);
        Assert.Equal(Tuning.ChoiceCount, seedling.Offers!.Distinct().Count());
        var offers = seedling.Offers.ToList();
        GrowFull(garden, 60);
        Assert.Equal(offers, seedling.Offers); // rolled once
        Assert.True(seedling.Power <= Choice + Garden.Clump(stem)); // full, it takes no more
    }

    [Fact]
    public void ASeedlingFillsPastItsNeighbours()
    {
        var (garden, stem, seedling) = Make();
        for (int t = 0; t < 200; t++)
        {
            foreach (var n in garden.Nodes)
                if (!n.IsSeedling) n.Power = n.Store * 0.8f; // a garden running at 80%
            garden.Grow(1);
        }
        Assert.True(seedling.Choosing);
    }

    [Fact]
    public void ChoosingRaisesTheStoreToTheTarget()
    {
        var (garden, _, seedling) = Make();
        garden.FillSeedling(seedling, Choice);
        var kind = seedling.Offers![0];
        Assert.False(garden.Choose(seedling, Enum.GetValues<EvolutionKind>().First(k => !seedling.Offers.Contains(k))));
        Assert.True(garden.Choose(seedling, kind));
        Assert.Equal(Tuning.FirstBloom, seedling.Evolution!.Target);
        Assert.Equal((float)Tuning.FirstBloom, seedling.Store);
        Assert.Equal(Choice, seedling.Power); // so it's a fifth full
        Assert.Empty(garden.Core.Fruits); // the marker grows at the bloom
        Assert.Equal(1, garden.SeedlingsUsed); // still its slot
        // Each later evolution's target five times the last.
        Assert.Equal(Tuning.FirstBloom * Tuning.BloomGrowth, garden.NextTarget);
        garden.EvolveNow(EvolutionKind.WildBloom);
        Assert.Equal(Tuning.FirstBloom * Tuning.BloomGrowth * Tuning.BloomGrowth, garden.NextTarget);
    }

    [Fact]
    public void ABloomBecomesItsWildFlowerGrowsItsMarkerAndRaisesTheLimits()
    {
        var (garden, stem, seedling) = Make();
        garden.FillSeedling(seedling, Choice);
        seedling.Offers = [EvolutionKind.BuildYourCore];
        Assert.True(garden.Choose(seedling, EvolutionKind.BuildYourCore));
        var before = garden.Capacity;
        float coreGen = Garden.FullProductionPerSecond(garden.Core);

        garden.FillSeedling(seedling, (float)Tuning.FirstBloom);
        Assert.DoesNotContain(seedling, garden.Nodes);
        var ember = Assert.Single(stem.Branches);
        Assert.Equal(NodeKind.Ember, ember.Spec.Kind);
        Assert.Equal(ember.Store, ember.Power); // full, from what the seedling held
        Assert.Contains(ember, garden.JustBloomed); // for the view's burst
        var marker = Assert.Single(garden.Core.Fruits);
        Assert.Equal(NodeKind.MarkCore, marker.Spec.Kind);
        Assert.Equal(before.WildFlowers + 1, garden.Capacity.WildFlowers);
        Assert.Equal(before.Generators + 1, garden.Capacity.Generators); // an Ember takes a generator slot: it costs nothing
        Assert.Equal(1, garden.WildCount);
        Assert.Equal(0, garden.FreeWildSlots);
        Assert.Equal(coreGen * 1.5f, Garden.FullProductionPerSecond(garden.Core), 3);
        Assert.Equal(1, garden.SeedlingsUsed); // the slot stays used
        Assert.Contains(garden.Tracking.Journal, e => e.Kind == JournalKind.Bloomed);

        // Pruning the flower takes only the flower.
        garden.Prune(ember);
        Assert.Equal(coreGen * 1.5f, Garden.FullProductionPerSecond(garden.Core), 3);
        Assert.Single(garden.Core.Fruits);
        Assert.Equal(1, garden.SeedlingsUsed);
    }

    [Fact]
    public void AnUnlockEvolutionUnlocksItsKindAndBoostsItsBase()
    {
        var (garden, stem, _) = Make();
        var generator = Add(garden, stem, NodeKind.Generator);
        float gen = Garden.FullProductionPerSecond(generator);
        Assert.False(garden.CanGrowWild(NodeKind.Pulsar));
        garden.EvolveNow(EvolutionKind.PulsarPower);
        Assert.True(garden.CanGrowWild(NodeKind.Pulsar));
        Assert.Contains(NodeKind.Pulsar, garden.WildUnlocked);
        Assert.True(Garden.FullProductionPerSecond(generator) > gen);
    }

    [Fact]
    public void RepeatsWaitForFiveEvolutionsAndSmartParticlesIsOneOff()
    {
        var garden = Garden.New(Layout.Radial, seed: 3);
        garden.EvolveNow(EvolutionKind.SmartParticles);
        garden.EvolveNow(EvolutionKind.MorePower);
        Assert.DoesNotContain(EvolutionKind.MorePower, garden.Offerable());
        garden.EvolveNow(EvolutionKind.WildBloom);
        garden.EvolveNow(EvolutionKind.LongStemmed);
        garden.EvolveNow(EvolutionKind.TwinStars);
        Assert.Equal(5, garden.Evolved.Count);
        Assert.Contains(EvolutionKind.MorePower, garden.Offerable());
        Assert.DoesNotContain(EvolutionKind.SmartParticles, garden.Offerable());
        var repeat = garden.EvolveNow(EvolutionKind.MorePower)!;
        Assert.Equal(2, repeat.Level);
        Assert.Equal("More Power II", repeat.Name);
    }

    [Fact]
    public void APrunedSeedlingLeavesItsEvolutionForTheNext()
    {
        var (garden, stem, seedling) = Make();
        garden.FillSeedling(seedling, Choice);
        var kind = seedling.Offers![0];
        garden.Choose(seedling, kind);
        garden.FillSeedling(seedling, 600);
        garden.Prune(seedling);
        garden.ForgetPrune();
        Assert.Single(garden.Waiting);
        Assert.Equal(0, garden.SeedlingsUsed);
        var next = Add(garden, stem, NodeKind.Seedling);
        Assert.Equal(kind, next.Evolution!.Kind);
        Assert.Equal(0, next.Power); // a new part starts empty
        Assert.Equal((float)Tuning.FirstBloom, next.Store);
        Assert.Equal(1, garden.SeedlingsUsed);
    }

    [Fact]
    public void SeedlingsAndEvolutionsSurviveASave()
    {
        var (garden, stem, seedling) = Make();
        garden.EvolveNow(EvolutionKind.BigStem);
        garden.Capacity = garden.Capacity with { Seedlings = 3 };
        garden.FillSeedling(seedling, Choice);
        var offers = seedling.Offers!.ToList();
        var other = Add(garden, garden.Core, NodeKind.Seedling); // (the Big Stem took the stem's last link)
        garden.FillSeedling(other, Choice);
        garden.Choose(other, other.Offers![1]);
        garden.FillSeedling(other, 700);

        var json = JsonSerializer.Serialize(garden.ToSave(), Json);
        var loaded = Garden.FromSave(JsonSerializer.Deserialize<GardenSave>(json, Json)!, seed: 7);
        Assert.Equal(garden.Evolved.Count, loaded.Evolved.Count);
        var seedlings = loaded.Nodes.Where(n => n.IsSeedling).ToList();
        Assert.Equal(offers, seedlings[0].Offers);
        Assert.True(seedlings[0].Choosing);
        Assert.Equal(other.Evolution!.Kind, seedlings[1].Evolution!.Kind);
        Assert.Equal(700, seedlings[1].Power);
        Assert.Equal(other.Store, seedlings[1].Store);
        Assert.Same(loaded.Evolved[1], seedlings[1].Evolution);
        Assert.Equal(garden.SeedlingsUsed, loaded.SeedlingsUsed);
        Assert.Single(loaded.Core.Fruits); // Big Stem's marker (the other hasn't bloomed)
        // Big Stem's boost is worked out again: a stem holds a third leaf.
        Assert.Equal(3, loaded.Nodes.First(n => n.Spec.Kind == NodeKind.Stem).MaxFruits);
        Assert.Contains(NodeKind.BigStem, loaded.WildUnlocked);
    }
}
