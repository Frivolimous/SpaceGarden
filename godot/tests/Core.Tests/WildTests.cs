namespace SpaceGarden.Core.Tests;

// Wild flowers (DESIGN.md, Wild flowers; step 5d): the roll, the limit, discovery and the save. Minimal, as step 5 asks.
public class WildTests
{
    static PlantNode Add(Garden garden, PlantNode parent, NodeKind kind) =>
        garden.GrowBranch(parent, kind) ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

    // core - stem, with room for more, and a wild flower limit.
    static (Garden Garden, PlantNode Stem) Make(int wildLimit, int seed = 5)
    {
        var garden = Garden.New(Layout.Radial, seed);
        garden.PassiveGrowth = false;
        garden.Capacity = new Capacity(10, 4, 1, WildFlowers: wildLimit);
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        return (garden, stem);
    }

    // The share of rolls on this stem that grow wild.
    static double WildShare(Garden garden, PlantNode stem, bool passive, int rolls = 20_000) =>
        Enumerable.Range(0, rolls).Count(_ => NodeSpecs.All[garden.RollBud(stem, NodeKind.Stem, passive)].IsWild) / (double)rolls;

    [Fact]
    public void TheRollObeysTheLimitAndTheChances()
    {
        var (none, stem0) = Make(wildLimit: 0);
        Assert.Equal(0, WildShare(none, stem0, passive: true));

        // The chance is per free slot: two free, twice the chance.
        var (two, stem2) = Make(wildLimit: 2);
        Assert.InRange(WildShare(two, stem2, passive: true), 2 * Tuning.PassiveWildChance * 0.9, 2 * Tuning.PassiveWildChance * 1.1);
        Assert.InRange(WildShare(two, stem2, passive: false), 2 * Tuning.PlantedWildChance * 0.9, 2 * Tuning.PlantedWildChance * 1.1);

        // A slot taken leaves one.
        Add(two, stem2, NodeKind.Tendril);
        Assert.Equal(1, two.FreeWildSlots);
        Assert.InRange(WildShare(two, stem2, passive: true), Tuning.PassiveWildChance * 0.85, Tuning.PassiveWildChance * 1.15);
    }

    [Fact]
    public void APlantedBudNeverGrowsAPassiveOnlyKind()
    {
        var (garden, stem) = Make(wildLimit: 20);
        for (int i = 0; i < 5_000; i++)
        {
            var kind = NodeSpecs.All[garden.RollBud(stem, NodeKind.Stem, passive: false)];
            Assert.False(kind.Wild?.PassiveOnly ?? false);
            if (kind.Wild is { } wild) Assert.Equal(NodeKind.Stem, wild.Base);
        }
    }

    [Fact]
    public void AWildFlowerTakesAWildSlotAndPruningFreesIt()
    {
        var (garden, stem) = Make(wildLimit: 1);
        garden.Capacity = garden.Capacity with { Stems = 1 }; // the stem cap is full
        var tendril = Add(garden, stem, NodeKind.Tendril); // a wild stem takes no stem slot
        Assert.Equal(0, garden.FreeWildSlots);
        Assert.Null(garden.GrowBranch(stem, NodeKind.Amplifier)); // the limit is reached
        garden.Prune(tendril);
        Assert.Equal(1, garden.FreeWildSlots);
        Assert.NotNull(garden.GrowBranch(stem, NodeKind.Amplifier));
    }

    [Fact]
    public void TheLockedKindsNeverRollUntilUnlocked()
    {
        var (garden, stem) = Make(wildLimit: 20);
        var locked = new[] { NodeKind.BigStem, NodeKind.Pulsar, NodeKind.TwinStar };
        foreach (var kind in locked) Assert.False(garden.CanGrowWild(kind));
        var rolled = new HashSet<NodeKind>();
        for (int i = 0; i < 5_000; i++)
        {
            rolled.Add(garden.RollBud(stem, NodeKind.Stem, passive: true));
            rolled.Add(garden.RollBud(stem, NodeKind.Generator, passive: true));
        }
        Assert.DoesNotContain(rolled, locked.Contains);
        Assert.Contains(NodeKind.Ember, rolled);

        // Its evolution unlocks a kind, with room for WildUnlockCap of it.
        garden.UnlockWild(NodeKind.Pulsar);
        Assert.Equal(Tuning.WildUnlockCap, garden.KindCap(NodeKind.Pulsar));
        var generators = Enumerable.Range(0, 5_000).Select(_ => garden.RollBud(stem, NodeKind.Generator, passive: true)).ToList();
        Assert.Contains(NodeKind.Pulsar, generators);
        for (int i = 0; i < Tuning.WildUnlockCap; i++) Add(garden, garden.Core.Branches[0], NodeKind.Pulsar);
        Assert.False(garden.CanGrowWild(NodeKind.Pulsar)); // capped
    }

    [Fact]
    public void AnEmberBurnsOutAndFreesItsSlot()
    {
        var (garden, stem) = Make(wildLimit: 1);
        var ember = Add(garden, stem, NodeKind.Ember);
        Assert.Equal(0, garden.FreeWildSlots);
        ember.Life = Tuning.EmberLifeSeconds - 1;
        Assert.True(Garden.Brightness(ember) < 0.3f); // dim near its end
        garden.Grow(2);
        Assert.DoesNotContain(ember, garden.Nodes);
        Assert.Equal(1, garden.FreeWildSlots);
        Assert.Equal(0, garden.Tracking.Pruned); // no prune: nothing to undo
        Assert.False(garden.CanUndoPrune);
        Assert.Contains(garden.Tracking.Journal, e => e is { Kind: JournalKind.Ended, Text: "An Ember burned out" });
        Assert.Equal(1, garden.Tracking.EndedCount[NodeKind.Ember]);
    }

    [Fact]
    public void AResistorBlocksBlobs()
    {
        var (garden, stem) = Make(wildLimit: 1);
        var resistor = Add(garden, stem, NodeKind.Resistor);
        var lab = Add(garden, resistor, NodeKind.Lab);
        Assert.Equal(0, GardenStats.GeneReach(garden, lab));
        Assert.All(Enumerable.Range(0, 200), _ => Assert.False(garden.WalkGene(lab)));
    }

    [Fact]
    public void AnAmplifierDoublesABlobOnce()
    {
        var garden = Garden.New(Layout.Radial, 3);
        garden.PassiveGrowth = false;
        garden.Capacity = new Capacity(10, 4, 1, WildFlowers: 1);
        // core - amplifier - lab: the lab's one blob passes the amplifier, and both find the core.
        var lab = Add(garden, Add(garden, garden.Core, NodeKind.Amplifier), NodeKind.Lab);
        lab.Blob = 1;
        garden.Grow(Tuning.EconomyStepSeconds);
        Assert.Equal(2 * Tuning.BlobGenes, garden.Genes);
    }

    [Fact]
    public void DiscoveryIsLoggedOnceAndSaved()
    {
        var (garden, stem) = Make(wildLimit: 2);
        garden.UnlockWild(NodeKind.BigStem);
        var tendril = Add(garden, stem, NodeKind.Tendril);
        Add(garden, tendril, NodeKind.Tendril);
        Assert.Contains(NodeKind.Tendril, garden.Tracking.Discovered.Keys);
        Assert.Single(garden.Tracking.Journal, e => e.Kind == JournalKind.Discovery);
        tendril.Life = 1234;

        var loaded = Garden.FromSave(garden.ToSave());
        Assert.Equal(garden.Tracking.Discovered, loaded.Tracking.Discovered);
        Assert.Equal(2, loaded.WildCount);
        Assert.Equal(1234, loaded.Nodes.First(n => n.Spec.Kind == NodeKind.Tendril).Life);
        Assert.Equal(Tuning.WildUnlockCap, loaded.KindCap(NodeKind.BigStem));
    }
}
