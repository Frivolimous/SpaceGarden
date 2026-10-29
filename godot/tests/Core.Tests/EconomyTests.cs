namespace SpaceGarden.Core.Tests;

public class EconomyTests
{
    const float Tolerance = 1e-4f;
    static readonly Capacity Roomy = new(100, 100, 2);

    static Garden NewGarden(Capacity? capacity = null)
    {
        var garden = Garden.New(Layout.Radial, seed: 7);
        if (capacity != null) garden.Capacity = capacity;
        garden.Unlocks = Tuning.UnlockTrack.Length; // the track done: these test the economy at fixed caps
        return garden;
    }

    static PlantNode Add(Garden garden, PlantNode parent, NodeKind kind) =>
        (NodeSpecs.All[kind].IsFruit ? garden.GrowFruit(parent) : garden.SproutFrom(parent, kind))
            ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

    // core - stem - stem, which forks into two stems each carrying two labs: 24 a second of labs and 12 of stems
    // against the core's 24. Every link is taken, and the caps (4 stems, no generators) leave no room to grow.
    static (Garden Garden, PlantNode Fork, PlantNode Hungry) Overloaded()
    {
        var garden = NewGarden(new Capacity(4, 0, 1));
        var trunk = Add(garden, garden.Core, NodeKind.Stem);
        var fork = Add(garden, trunk, NodeKind.Stem);
        var left = Add(garden, fork, NodeKind.Stem);
        var right = Add(garden, fork, NodeKind.Stem);
        foreach (var side in new[] { left, right })
            for (int i = 0; i < 2; i++) Add(garden, side, NodeKind.Lab);
        foreach (var node in garden.Nodes) node.Power = node.Store;
        return (garden, fork, right);
    }

    static int MainNodes(Garden garden) => garden.Nodes.Count(n => !n.Spec.IsFruit);

    // ==== POWER ==== \\

    [Fact]
    public void RatesFollowTheOriginal()
    {
        // Per second when full: the original's per-tick numbers times 60.
        var garden = NewGarden(Roomy);
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        var generator = Add(garden, stem, NodeKind.Generator);
        var fork = Add(garden, stem, NodeKind.Stem);
        var lab = Add(garden, fork, NodeKind.Lab);
        var grove = Add(garden, fork, NodeKind.Grove);
        Assert.Equal(24, Garden.FullProductionPerSecond(garden.Core), Tolerance);
        Assert.Equal(12, Garden.FullProductionPerSecond(generator), Tolerance);
        Assert.Equal(3, Garden.FullUpkeepPerSecond(stem), Tolerance);
        Assert.Equal(6, Garden.FullUpkeepPerSecond(lab), Tolerance);
        Assert.Equal(6, Garden.FullUpkeepPerSecond(grove), Tolerance);
        Assert.Equal((300f, 100f, 200f), (garden.Core.Store, stem.Store, lab.Store));
        var (production, upkeep) = garden.FullPowerBalance();
        Assert.Equal(36, production, Tolerance);
        Assert.Equal(18, upkeep, Tolerance);
    }

    [Fact]
    public void EverythingFollowsItsFill()
    {
        // Sources make, and drains cost, their rate times their fill: the core too. A full source makes nothing.
        var garden = NewGarden(Roomy);
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        var generator = Add(garden, stem, NodeKind.Generator);
        foreach (var node in new[] { garden.Core, stem, generator })
        {
            node.Power = 0;
            Assert.Equal(0, Garden.ProductionPerSecond(node) + Garden.UpkeepPerSecond(node));
        }
        garden.Core.Power = garden.Core.Store / 2;
        generator.Power = generator.Store / 4;
        stem.Power = stem.Store / 2;
        Assert.Equal(12, Garden.ProductionPerSecond(garden.Core), Tolerance);
        Assert.Equal(3, Garden.ProductionPerSecond(generator), Tolerance);
        Assert.Equal(1.5f, Garden.UpkeepPerSecond(stem), Tolerance);
        garden.Core.Power = garden.Core.Store;
        Assert.Equal(0, Garden.ProductionPerSecond(garden.Core));
    }

    [Fact]
    public void LeavesHalveAStemsUpkeep()
    {
        var garden = NewGarden(Roomy);
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        Add(garden, stem, NodeKind.Leaf);
        Assert.Equal(1.5f, Garden.FullUpkeepPerSecond(stem), Tolerance);
        Add(garden, stem, NodeKind.Leaf);
        Assert.Equal(0.75f, Garden.FullUpkeepPerSecond(stem), Tolerance);
    }

    [Fact]
    public void GeneratorFruitAddsUpToAFifth()
    {
        var garden = NewGarden(Roomy);
        var generator = Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Generator);
        for (int i = 0; i < 3; i++) Add(garden, generator, NodeKind.Burr);
        generator.Power = generator.Store / 2;
        Assert.Equal(12 * 1.2f, Garden.FullProductionPerSecond(generator), Tolerance);
        Assert.Equal(12 * 1.2f / 2, Garden.ProductionPerSecond(generator), Tolerance);
    }

    [Fact]
    public void NewPartsStartEmpty()
    {
        var garden = NewGarden(Roomy);
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        Assert.Equal(0, stem.Power);
        Assert.Equal(garden.Core.Store, garden.Core.Power); // a new garden's core starts full
    }

    [Fact]
    public void PowerIsMadeAndSpentButNeverLostInTransfer()
    {
        // Over a step the garden gains exactly what it makes less what it costs, at the fills it started with: the
        // clumps sent move power without losing any.
        var garden = NewGarden(new Capacity(2, 0, 1));
        garden.WildGrowth = false;
        var a = Add(garden, garden.Core, NodeKind.Stem);
        var b = Add(garden, a, NodeKind.Stem);
        garden.Core.Power = 200;
        a.Power = 60;
        b.Power = 10;
        float dt = (float)Tuning.EconomyStepSeconds;
        for (int step = 0; step < 20; step++)
        {
            var (production, upkeep) = garden.PowerBalance();
            float expected = garden.TotalPower() + (production - upkeep) * dt;
            garden.Grow(Tuning.EconomyStepSeconds);
            Assert.Equal(expected, garden.TotalPower(), 1e-3f);
        }
    }

    [Fact]
    public void PowerGoesOutInClumpsToTheEmptiestNeighbour()
    {
        // core - trunk, the trunk forking into an empty-ish stem and a lab: the core sends the trunk a clump of 15,
        // and the trunk sends 10 to the stem, which counts as emptier for its weight, not to the lab.
        var garden = NewGarden(new Capacity(2, 0, 1));
        garden.WildGrowth = false;
        var trunk = Add(garden, garden.Core, NodeKind.Stem);
        var stem = Add(garden, trunk, NodeKind.Stem);
        var lab = Add(garden, trunk, NodeKind.Lab);
        trunk.Power = 50;
        stem.Power = 20; // 0.2, over its weight 0.18
        lab.Power = 38;  // 0.19
        var before = garden.Nodes.ToDictionary(n => n, n => n.Power);
        var production = garden.Nodes.ToDictionary(n => n, Garden.ProductionPerSecond);
        var upkeep = garden.Nodes.ToDictionary(n => n, Garden.UpkeepPerSecond);
        garden.Grow(Tuning.EconomyStepSeconds);
        float Moved(PlantNode n) =>
            n.Power - before[n] - (production[n] - upkeep[n]) * (float)Tuning.EconomyStepSeconds;
        Assert.Equal(-15, Moved(garden.Core), 1e-3f);
        Assert.Equal(15 - 10, Moved(trunk), 1e-3f);
        Assert.Equal(10, Moved(stem), 1e-3f);
        Assert.Equal(0, Moved(lab), 1e-3f);
    }

    [Fact]
    public void APartAtAQuarterOrLessSendsNothing()
    {
        var garden = NewGarden(new Capacity(2, 0, 1));
        garden.WildGrowth = false;
        var a = Add(garden, garden.Core, NodeKind.Stem);
        var b = Add(garden, a, NodeKind.Stem);
        garden.Core.Power = 0.24f * garden.Core.Store;
        a.Power = 24;
        garden.Grow(Tuning.EconomyStepSeconds);
        Assert.Equal(0, b.Power); // a, at 24%, kept its power to itself
        Assert.True(a.Power < 24); // and the core, at 24%, sent it none
    }

    [Fact]
    public void PowerFlowsOutFromTheSources()
    {
        var garden = NewGarden(new Capacity(3, 0, 1));
        garden.WildGrowth = false;
        var a = Add(garden, garden.Core, NodeKind.Stem);
        var b = Add(garden, a, NodeKind.Stem);
        var c = Add(garden, b, NodeKind.Stem);
        garden.Grow(3);
        Assert.True(garden.Core.Fill > a.Fill && a.Fill > b.Fill && b.Fill >= c.Fill,
            string.Join(" ", garden.Nodes.Select(n => n.Fill)));
        garden.Grow(60);
        Assert.All(garden.Nodes, n => Assert.True(n.Fill > 0.5f, $"{n.Spec.Kind} at {n.Fill}")); // full in a minute
    }

    [Fact]
    public void FillsKeepMovingOnceSettled()
    {
        // The clumps keep the fills rocking around their balance rather than settling on it.
        var (garden, _, hungry) = Overloaded();
        garden.Grow(600);
        var fills = new List<float>();
        for (int step = 0; step < 100; step++)
        {
            garden.Grow(Tuning.EconomyStepSeconds);
            fills.Add(hungry.Fill);
        }
        Assert.True(fills.Max() - fills.Min() > 0.05f, $"{fills.Min()} to {fills.Max()}");
    }

    [Fact]
    public void EachTransferFlashesItsLinkOnce()
    {
        // core - stem - stem, the far stem empty: power goes out along both links. Each clump sent flashes its link
        // once, within the step, as bright as the part it reached is full.
        var garden = NewGarden(new Capacity(2, 0, 1));
        garden.WildGrowth = false;
        var near = Add(garden, garden.Core, NodeKind.Stem);
        Add(garden, near, NodeKind.Stem);
        near.Power = 50;
        garden.Grow(Tuning.EconomyStepSeconds);
        int[] flashes = new int[garden.Links.Count];
        for (int tick = 0; tick < Tuning.EconomyStepSeconds * Tuning.TicksPerSecond + 1; tick++)
        {
            var before = garden.Links.Select(l => l.Flash).ToList();
            garden.Step();
            for (int i = 0; i < garden.Links.Count; i++)
            {
                var link = garden.Links[i];
                if (link.Flash <= before[i]) continue;
                flashes[i]++;
                Assert.True(link.FlowsToTarget);
                Assert.Equal(Tuning.FlashPeak * link.Target.Fill, link.Flash, Tolerance);
            }
        }
        Assert.Equal([1, 1], flashes);
    }

    [Fact]
    public void PartsShowTheirFill()
    {
        var garden = NewGarden(new Capacity(1, 0, 1));
        garden.WildGrowth = false;
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        var leaf = Add(garden, stem, NodeKind.Leaf);
        garden.SettleLooks();
        Assert.Equal(0, stem.FillLook);

        // The shown fill eases after the real one as power arrives; a fruit shows its part's.
        garden.Grow(5);
        for (int tick = 0; tick < 2 * Tuning.TicksPerSecond; tick++) garden.Step();
        Assert.Equal(stem.Fill, stem.FillLook, 0.05f);
        Assert.Equal(stem.FillLook, leaf.FillLook, 0.05f);

        // Past full shows past full (the view swells it only a little).
        stem.Power = 1.5f * stem.Store;
        garden.SettleLooks();
        Assert.Equal(1.5f, stem.FillLook, Tolerance);
    }

    // ==== OVERLOAD ==== \\

    // The average fill of each part over an hour, a few seconds at a time.
    static Dictionary<PlantNode, float> AverageFills(Garden garden)
    {
        var sums = garden.Nodes.Where(n => !n.Spec.IsFruit).ToDictionary(n => n, _ => 0f);
        const int samples = 720;
        for (int i = 0; i < samples; i++)
        {
            garden.Grow(5);
            foreach (var n in sums.Keys) sums[n] += n.Fill / samples;
        }
        return sums;
    }

    [Fact]
    public void AnOverloadSettlesLowButNeverDestroys()
    {
        var (garden, _, _) = Overloaded();
        var parts = garden.Nodes.ToList();
        Assert.True(GardenStats.Of(garden).Overloaded);
        garden.Grow(12 * 3600);
        Assert.All(parts, n => Assert.Contains(n, garden.Nodes));
        Assert.All(garden.Nodes, n => Assert.True(n.Power >= 0));
        // The core only sends while fuller than SendFill, so it never runs dry.
        Assert.True(garden.Core.Fill > Tuning.SendFill - garden.Core.Spec.PowerClump / garden.Core.Store);
        var fills = AverageFills(garden);
        Assert.True(fills.Values.Average() < 0.75f, $"{fills.Values.Average()}");
    }

    [Fact]
    public void FarTipsRunLowest()
    {
        var (garden, fork, _) = Overloaded();
        garden.Grow(600);
        var fills = AverageFills(garden);
        var labs = fills.Where(f => f.Key.Spec.Kind == NodeKind.Lab).Select(f => f.Value).ToList();
        Assert.True(fills[fork.Parent!] > labs.Max(), $"trunk {fills[fork.Parent!]}, labs {string.Join(" ", labs)}");
    }

    [Fact]
    public void PruningTheOverloadRaisesTheRest()
    {
        var (garden, _, hungry) = Overloaded();
        garden.Grow(600);
        var before = AverageFills(garden);
        var kept = garden.Nodes.Where(n => n.Spec.Kind == NodeKind.Lab && n.Parent != hungry).ToList();
        garden.Prune(hungry);
        garden.ForgetPrune();
        Assert.False(GardenStats.Of(garden).Overloaded); // 3 stems and 2 labs: 21 a second against the core's 24
        var after = AverageFills(garden);
        Assert.All(kept, lab => Assert.True(after[lab] > before[lab] + 0.1f, $"{before[lab]} -> {after[lab]}"));
    }

    [Fact]
    public void AnEmptyPartFillsByItself()
    {
        var garden = NewGarden(new Capacity(1, 0, 1));
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        garden.Core.Power = 0.3f * garden.Core.Store;
        garden.Grow(60);
        Assert.True(stem.Fill > 0.5f && garden.Core.Fill > 0.5f, $"core {garden.Core.Fill}, stem {stem.Fill}");
    }

    // ==== CAPACITY ==== \\

    [Fact]
    public void CoreStartsWithOneLink()
    {
        var garden = NewGarden();
        Assert.Equal(1, garden.Core.MaxLinks);
        Assert.NotNull(garden.SproutFrom(garden.Core, NodeKind.Stem));
        Assert.Null(garden.SproutFrom(garden.Core, NodeKind.Stem));
    }

    [Fact]
    public void RaisingCoreLinksMakesRoom()
    {
        var garden = NewGarden();
        Add(garden, garden.Core, NodeKind.Stem);
        garden.Capacity = garden.Capacity with { CoreLinks = 2 };
        Assert.NotNull(garden.SproutFrom(garden.Core, NodeKind.Stem));
        Assert.Null(garden.SproutFrom(garden.Core, NodeKind.Stem));
    }

    [Fact]
    public void StemsAndGeneratorsAreCapped()
    {
        var garden = NewGarden(); // start: 4 stems, 1 generator
        var tip = garden.Core;
        for (int i = 0; i < 4; i++) tip = Add(garden, tip, NodeKind.Stem);
        Assert.Null(garden.SproutFrom(tip, NodeKind.Stem));
        Add(garden, tip, NodeKind.Generator);
        Assert.Null(garden.SproutFrom(tip, NodeKind.Generator));
        Assert.NotNull(garden.SproutFrom(tip.Parent!, NodeKind.Lab)); // labs: only links and power limit them
    }

    [Fact]
    public void LinksAndFruitHaveLocalLimits()
    {
        var garden = NewGarden(Roomy);
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        Add(garden, stem, NodeKind.Stem);
        Add(garden, stem, NodeKind.Stem);
        Assert.Null(garden.SproutFrom(stem, NodeKind.Stem)); // 3 links, counting the parent
        Add(garden, stem, NodeKind.Leaf);
        Add(garden, stem, NodeKind.Leaf);
        Assert.Null(garden.SproutFrom(stem, NodeKind.Leaf));
        Assert.Null(garden.SproutFrom(stem, NodeKind.Burr)); // not a stem's fruit
    }

    [Fact]
    public void WildGrowthKeepsWithinCapacity()
    {
        var garden = Grown();
        garden.Grow(Tuning.CatchUpCapSeconds);
        Assert.True(MainNodes(garden) > 4);
        Assert.True(garden.Nodes.Count(n => n.Spec.Kind == NodeKind.Stem) <= 4);
        Assert.True(garden.Nodes.Count(n => n.Spec.Kind == NodeKind.Generator) <= 1);
        Assert.Equal(1, garden.Core.Links);
    }

    [Fact]
    public void WildGrowthDoesntWaitForPower()
    {
        // The plant is stupid: a low garden's grove still sends what blobs it can, and they grow buds that only add
        // to the load, past what the garden can keep full. Pruning is the player's job.
        var garden = NewGarden(new Capacity(30, 0, 1));
        var stem = Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Stem);
        Add(garden, stem, NodeKind.Grove);
        for (int i = 0; i < 4; i++) stem = Add(garden, stem, NodeKind.Stem);
        for (int i = 0; i < 2; i++) Add(garden, stem, NodeKind.Lab);
        garden.Grow(3 * 24 * 3600);
        Assert.True(MainNodes(garden) > 12);
        Assert.True(GardenStats.Of(garden).Overloaded);
    }

    // ==== CATCH-UP ==== \\

    // The core, a stem, and a grove the player planted on it.
    static Garden Grown()
    {
        var garden = NewGarden();
        garden.Sprout();
        Add(garden, garden.Nodes[1], NodeKind.Grove);
        return garden;
    }

    static void AssertSame(Garden expected, Garden actual)
    {
        Assert.Equal(expected.Nodes.Count, actual.Nodes.Count);
        for (int i = 0; i < expected.Nodes.Count; i++)
        {
            var (e, a) = (expected.Nodes[i], actual.Nodes[i]);
            Assert.Equal(e.Spec.Kind, a.Spec.Kind);
            Assert.Equal(e.Parent?.Id, a.Parent?.Id);
            Assert.Equal(e.Power, a.Power, Tolerance);
            Assert.Equal(e.Forming, a.Forming);
            Assert.Equal(e.Growth, a.Growth, Tolerance);
        }
    }

    [Fact]
    public void OneLongStepMatchesManySmallOnes()
    {
        const double day = 24 * 3600;
        var once = Grown();
        var bySecond = Grown();
        var byFrame = Grown();
        once.Grow(day);
        for (int i = 0; i < day; i++) bySecond.Grow(1);
        for (int i = 0; i < day * 64; i++) byFrame.Grow(1 / 64.0); // about a 1x frame at a time (exact in binary)
        AssertSame(once, bySecond);
        AssertSame(once, byFrame);
    }

    [Fact]
    public void CatchUpMatchesLivingThroughAnOverload()
    {
        var (once, _, _) = Overloaded();
        var (live, _, _) = Overloaded();
        once.CatchUp(10 * 3600);
        for (int i = 0; i < (int)(10 * 3600 / 7); i++) live.Grow(7); // uneven chunks
        live.Grow(10 * 3600 % 7);
        Assert.Equal(once.TotalPower(), live.TotalPower(), 1e-3f);
    }

    [Fact]
    public void CatchUpIsCappedAt72Hours()
    {
        var capped = Grown();
        var exact = Grown();
        capped.CatchUp(30 * 24 * 3600);
        exact.CatchUp(Tuning.CatchUpCapSeconds);
        AssertSame(exact, capped);
    }

    // ==== SAVES ==== \\

    [Fact]
    public void SavesKeepTheEconomy()
    {
        var (garden, _, _) = Overloaded();
        garden.Capacity = garden.Capacity with { CoreLinks = 2 };
        garden.Grow(5 * 3600 + 3);
        var loaded = Garden.FromSave(garden.ToSave(), seed: 7);
        Assert.Equal(garden.Capacity, loaded.Capacity);
        Assert.Equal(2, loaded.Core.MaxLinks);
        Assert.Equal(garden.EconomyClock, loaded.EconomyClock);
        AssertSame(garden, loaded);
    }
}
