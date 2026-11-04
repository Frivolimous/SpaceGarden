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

        // The chance is the same however many slots are free.
        var (two, stem2) = Make(wildLimit: 2);
        Assert.InRange(WildShare(two, stem2, passive: true), Tuning.PassiveWildChance * 0.9, Tuning.PassiveWildChance * 1.1);
        Assert.InRange(WildShare(two, stem2, passive: false), Tuning.PlantedWildChance * 0.9, Tuning.PlantedWildChance * 1.1);

        // A slot taken leaves one: the same chance.
        Add(two, stem2, NodeKind.Tendril);
        Assert.Equal(1, two.FreeWildSlots);
        Assert.InRange(WildShare(two, stem2, passive: true), Tuning.PassiveWildChance * 0.9, Tuning.PassiveWildChance * 1.1);

        // All taken: none.
        Add(two, stem2, NodeKind.Tendril);
        Assert.Equal(0, WildShare(two, stem2, passive: true));
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
        var tendril = Add(garden, stem, NodeKind.Amplifier); // an Amplifier takes no stem slot
        Assert.Null(garden.GrowBranch(stem, NodeKind.Tendril)); // a Tendril would: the stem cap is full
        Assert.Equal(0, garden.FreeWildSlots);
        Assert.Null(garden.GrowBranch(stem, NodeKind.Amplifier)); // the limit is reached
        garden.Prune(tendril);
        Assert.Equal(1, garden.FreeWildSlots);
        Assert.NotNull(garden.GrowBranch(stem, NodeKind.Amplifier));
    }

    // Nov 3: every kind can grow wild from the start (the core track only adds slots).
    [Fact]
    public void EveryKindRollsFromTheStart()
    {
        var (garden, stem) = Make(wildLimit: 20);
        foreach (var spec in NodeSpecs.Wild) Assert.True(garden.CanGrowWild(spec.Kind), $"{spec.Kind} is locked");
        var rolled = new HashSet<NodeKind>();
        for (int i = 0; i < 5_000; i++)
        {
            rolled.Add(garden.RollBud(stem, NodeKind.Stem, passive: true));
            rolled.Add(garden.RollBud(stem, NodeKind.Generator, passive: true));
        }
        foreach (var kind in new[] { NodeKind.BigStem, NodeKind.Pulsar, NodeKind.TwinStar, NodeKind.Ember })
            Assert.Contains(kind, rolled);

        // Uncapped, but caps stay possible in data.
        Assert.Null(garden.KindCap(NodeKind.Pulsar));
        garden.WildCaps[NodeKind.Pulsar] = 0;
        Assert.False(garden.CanGrowWild(NodeKind.Pulsar)); // a per-kind cap still holds
    }

    [Fact]
    public void AnEmbersPowerIsCappedByTheLifeItHasLeft()
    {
        // Halfway through its life it can hold half its store: no more is fed to it, what it holds past that goes on,
        // and as it makes power for its fill it makes at most half its rate. Its store and rate stay.
        var (garden, stem) = Make(wildLimit: 1);
        var ember = Add(garden, stem, NodeKind.Ember);
        ember.Life = Tuning.EmberLifeSeconds / 2;
        Assert.Equal(ember.Spec.PowerStore, ember.Store);
        Assert.Equal(ember.Store / 2, Garden.PowerCap(ember), 0.5f);
        Assert.Equal(NodeSpecs.All[NodeKind.Ember].PowerPerSecond, Garden.FullProductionPerSecond(ember), 1e-3f);
        ember.Power = Garden.PowerCap(ember);
        Assert.Equal(0, Garden.ProductionPerSecond(ember)); // at its cap, as a full generator
        ember.Power = ember.Store; // over its cap: it sends on
        stem.Power = 0;
        garden.Grow(Tuning.EconomyStepSeconds);
        Assert.True(ember.Power < ember.Store);
        // A stem fuller than it (for its cap) sends it nothing.
        ember.Power = Garden.PowerCap(ember);
        stem.Power = stem.Store * 0.9f;
        float before = ember.Power;
        garden.Grow(Tuning.EconomyStepSeconds);
        Assert.True(ember.Power <= before + 0.01f);
    }

    [Fact]
    public void AnEmberBurnsOutAndFreesItsSlot()
    {
        var (garden, stem) = Make(wildLimit: 1);
        var ember = Add(garden, stem, NodeKind.Ember);
        Assert.Equal(0, garden.FreeWildSlots);
        ember.Life = Tuning.EmberLifeSeconds - 1;
        Assert.True(Garden.PowerCap(ember) < 0.01f * ember.Store); // near its end it can hold almost nothing
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
        Assert.InRange(garden.Genes, 1, 2 * Tuning.BlobGenes); // doubled AmplifyChance of the time
    }

    [Fact]
    public void ABuffersBlobsRaiseNearbyProduction()
    {
        var (garden, stem) = Make(wildLimit: 1);
        var buffer = Add(garden, stem, NodeKind.Buffer);
        var generator = Add(garden, stem, NodeKind.Generator);
        float plain = Garden.FullProductionPerSecond(generator);
        for (int i = 0; i < 10 && generator.Buff <= 0; i++)
        {
            buffer.Blob = 1;
            garden.Grow(Tuning.EconomyStepSeconds);
        }
        Assert.True(generator.Buff > 0);
        Assert.Equal(plain * (1 + Tuning.BuffBonus), Garden.FullProductionPerSecond(generator), 1e-3f);
    }

    [Fact]
    public void ABuffMoteIsUsedUpByTheFirstPartThatNeedsIt()
    {
        var (garden, stem) = Make(wildLimit: 1);
        var generator = Add(garden, stem, NodeKind.Generator);
        // A stem doesn't use a buff: the mote passes on.
        Assert.False(garden.BuffMoteForTest(stem));
        // No buff: it's used up, and the generator buffed.
        Assert.True(garden.BuffMoteForTest(generator));
        Assert.Equal(Tuning.BuffSeconds, generator.Buff, 3);
        // Buffed for longer than BuffRefreshSeconds: passed on, the buff untouched.
        generator.Buff = 20;
        Assert.False(garden.BuffMoteForTest(generator));
        Assert.Equal(20, generator.Buff, 3);
        // Less than that left: used up, the buff back to full.
        generator.Buff = Tuning.BuffRefreshSeconds - 1;
        Assert.True(garden.BuffMoteForTest(generator));
        Assert.Equal(Tuning.BuffSeconds, generator.Buff, 3);
    }

    [Fact]
    public void APulsarSendsHalfItsPowerAtOnceWhenFull()
    {
        var (garden, stem) = Make(wildLimit: 1);
        garden.UnlockWild(NodeKind.Pulsar);
        var pulsar = Add(garden, stem, NodeKind.Pulsar);
        // Below full it sends nothing, whatever its fill.
        pulsar.Power = pulsar.Store * 0.9f;
        stem.Power = 0;
        garden.Grow(Tuning.EconomyStepSeconds);
        Assert.True(pulsar.Power >= pulsar.Store * 0.9f);
        // Full, it still sends nothing, and keeps making power past full.
        pulsar.Power = pulsar.Store;
        stem.Power = 0;
        garden.Grow(Tuning.EconomyStepSeconds);
        Assert.True(pulsar.Power > pulsar.Store);
        Assert.True(Garden.ProductionPerSecond(pulsar) > 0);
        // At PulsarBurstFill of its store, it sends PulsarBurst of its power to its emptiest neighbour in one step.
        float burstAt = pulsar.Store * Tuning.PulsarBurstFill;
        pulsar.Power = burstAt;
        stem.Power = 0;
        garden.Grow(Tuning.EconomyStepSeconds);
        Assert.Equal(burstAt - pulsar.Store * Tuning.PulsarBurst, pulsar.Power, 1f); // half its store (Nov 3)
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
    }

    // Nov 3: buffs only take on the parts that use them.
    [Fact]
    public void OnlyThePartsThatUseABuffTakeIt()
    {
        var takes = new[]
        {
            NodeKind.Generator, NodeKind.Lab, NodeKind.Grove, NodeKind.Core, NodeKind.Pulsar, NodeKind.TwinStar,
            NodeKind.Ember, NodeKind.LumberGrove, NodeKind.SeedPod, NodeKind.WildLab, NodeKind.Lichen,
        };
        var passes = new[]
        {
            NodeKind.Stem, NodeKind.Tendril, NodeKind.BigStem, NodeKind.Amplifier, NodeKind.Resistor, NodeKind.Buffer,
            NodeKind.CometBloom, NodeKind.Moonflower, NodeKind.NebulaBloom,
        };
        foreach (var kind in takes) Assert.True(NodeSpecs.All[kind].TakesBuffs, $"{kind} should take buffs");
        foreach (var kind in passes) Assert.False(NodeSpecs.All[kind].TakesBuffs, $"{kind} shouldn't take buffs");
    }

    // ==== Nov 3 rework ==== \\

    // A Wild Lab's blob carries 2 genes: on the core, every one it sends counts twice.
    [Fact]
    public void AWildLabsBlobsCarryTwoGenes()
    {
        var (garden, stem) = Make(wildLimit: 1);
        Add(garden, stem, NodeKind.WildLab);
        foreach (var n in garden.Nodes) n.Power = n.Store;
        double before = garden.Genes;
        garden.Grow(60);
        double genes = garden.Genes - before;
        Assert.True(genes > 0);
        Assert.Equal(0, genes % 2, 6); // whole pairs
    }

    // An Amplifier doubles a passing blob's value (once), AmplifyChance of the time (a quarter since Nov 4): one blob, worth 2.
    [Fact]
    public void AnAmplifierDoublesABlobsValue()
    {
        var (garden, stem) = Make(wildLimit: 1);
        var amplifier = Add(garden, stem, NodeKind.Amplifier);
        var lab = Add(garden, amplifier, NodeKind.Lab);
        foreach (var n in garden.Nodes) n.Power = n.Store;
        double before = garden.Genes;
        garden.Grow(600); // about 120 blobs, all reaching the core (core - stem - amplifier - lab: the only way)
        double perBlob = (garden.Genes - before) / (Garden.BlobsPerHour(lab) * 600 / 3600);
        double mean = 1 + Tuning.AmplifyChance;
        Assert.InRange(perBlob, mean - 0.2, mean + 0.2); // (1.25 on average)
    }

    // A Tendril sends its clump in two halves a step, picking afresh for the second.
    [Fact]
    public void ATendrilSendsTwiceAStep()
    {
        Assert.Equal(2, NodeSpecs.All[NodeKind.Tendril].TransferSplits);
        var (garden, stem) = Make(wildLimit: 1);
        var tendril = Add(garden, stem, NodeKind.Tendril);
        var a = Add(garden, tendril, NodeKind.Stem);
        var b = Add(garden, tendril, NodeKind.Stem);
        foreach (var n in garden.Nodes) n.Power = 0;
        tendril.Power = tendril.Store;
        garden.Grow(Tuning.EconomyStepSeconds);
        // Both halves found an empty neighbour, a different one each.
        Assert.Equal(2, new[] { stem, a, b }.Count(n => n.Power > 0));
    }

    // A Comet Bloom: active for its first 10 minutes (blobs worth double, buffs twice as long), then fading.
    [Fact]
    public void ACometBloomIsActiveThenFades()
    {
        var (garden, stem) = Make(wildLimit: 1);
        var comet = Add(garden, stem, NodeKind.CometBloom);
        garden.Grow(Tuning.EconomyStepSeconds);
        Assert.True(garden.CometActive);
        Assert.Equal(1, Garden.Brightness(comet));
        comet.Life = Tuning.CometActiveSeconds + Tuning.CometFadeSeconds / 2;
        garden.Grow(Tuning.EconomyStepSeconds);
        Assert.False(garden.CometActive);
        Assert.InRange(Garden.Brightness(comet), 0.45f, 0.55f);
    }

    // The Nebula Bloom is retired: it never grows wild.
    [Fact]
    public void TheNebulaBloomNoLongerGrows()
    {
        var (garden, stem) = Make(wildLimit: 20);
        Assert.False(garden.CanGrowWild(NodeKind.NebulaBloom));
        Assert.DoesNotContain(NodeSpecs.Wild, s => s.Kind == NodeKind.NebulaBloom);
        for (int i = 0; i < 3_000; i++) Assert.NotEqual(NodeKind.NebulaBloom, garden.RollBud(stem, NodeKind.Grove, passive: true));
    }

    // Nov 3: a Moonflower's bonus reaches only the wild flowers that use a boost (as a buff), not an Amplifier.
    [Fact]
    public void AMoonflowerLightsOnlyTheWildFlowersThatUseABoost()
    {
        var (garden, stem) = Make(wildLimit: 4);
        var branch = Add(garden, stem, NodeKind.Stem);
        Add(garden, branch, NodeKind.Moonflower);
        var amplifier = Add(garden, stem, NodeKind.Amplifier);
        var wildLab = Add(garden, branch, NodeKind.WildLab);
        garden.Grow(Tuning.EconomyStepSeconds);
        Assert.True(wildLab.Moonlit);
        Assert.False(amplifier.Moonlit);
    }
}
