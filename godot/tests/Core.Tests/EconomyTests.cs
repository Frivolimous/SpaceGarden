namespace SpaceGarden.Core.Tests;

public class EconomyTests
{
    const float Tolerance = 1e-4f;
    const float P = Tuning.PowerScale; // the draft table's units, rescaled with the blobs
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

    // core - stem - stem, which forks into two stems each carrying two labs: 12 an hour of labs and 2.5 of stems
    // against the core's 8. Every link is taken, and the caps (4 stems, no generators) leave no room to grow.
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

    static int Wilting(Garden garden) => garden.Nodes.Count(n => n.Wilting);
    static int MainNodes(Garden garden) => garden.Nodes.Count(n => !n.Spec.IsFruit);

    // ==== POWER BALANCE ==== \\

    [Fact]
    public void CoreAloneMakesEightAnHour()
    {
        var garden = NewGarden();
        Assert.Equal((8 * P, 0f), garden.PowerBalance());
    }

    [Fact]
    public void RatesFollowTheDraftTable()
    {
        var garden = NewGarden(Roomy);
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        var generator = Add(garden, stem, NodeKind.Generator);
        var lab = Add(garden, stem, NodeKind.Lab);
        foreach (var node in garden.Nodes) node.Power = node.Store;

        Assert.Equal(4 * P, Garden.ProductionPerHour(generator));
        Assert.Equal(3 * P, Garden.UpkeepPerHour(lab));
        Assert.Equal(0.5f * P, Garden.UpkeepPerHour(stem));
        Assert.Equal((12 * P, 3.5f * P), garden.PowerBalance());
    }

    [Fact]
    public void LeavesHalveAStemsUpkeep()
    {
        var garden = NewGarden(Roomy);
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        Add(garden, stem, NodeKind.Leaf);
        Assert.Equal(0.25f * P, Garden.UpkeepPerHour(stem));
        Add(garden, stem, NodeKind.Leaf);
        Assert.Equal(0.125f * P, Garden.UpkeepPerHour(stem));
    }

    [Fact]
    public void GeneratorFruitAddsUpToAFifth()
    {
        var garden = NewGarden(Roomy);
        var generator = Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Generator);
        for (int i = 0; i < 3; i++) Add(garden, generator, NodeKind.Burr);
        generator.Power = generator.Store;
        Assert.Equal(4 * P * 1.2f, Garden.ProductionPerHour(generator), Tolerance);
    }

    [Fact]
    public void AGeneratorMakesLessPowerWhenLow()
    {
        // From OutputFloor of its rate when empty to all of it when full.
        var garden = NewGarden(Roomy);
        var generator = Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Generator);
        generator.Power = 0;
        Assert.Equal(4 * P * Tuning.OutputFloor, Garden.ProductionPerHour(generator), Tolerance);
        generator.Power = generator.Store / 2;
        Assert.Equal(4 * P * (1 + Tuning.OutputFloor) / 2, Garden.ProductionPerHour(generator), Tolerance);
        garden.Core.Power = 0;
        Assert.Equal(8 * P, Garden.ProductionPerHour(garden.Core)); // the core always makes its full rate
    }

    [Fact]
    public void PowerIsConservedWhileNothingIsFull()
    {
        // A drained garden with a surplus: every hour it gains exactly production minus upkeep (nothing overflows).
        var garden = NewGarden(new Capacity(1, 0, 1));
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        garden.Core.Power = 0;
        stem.Power = 0.2f;
        float before = garden.TotalPower();
        garden.Grow(600);
        Assert.False(stem.Wilting);
        Assert.Equal(before + (8 - 0.5f) * P / 6, garden.TotalPower(), 0.01f); // (float rounding, at these sizes)
    }

    [Fact]
    public void PowerFlowsOutFromTheSources()
    {
        var garden = NewGarden(new Capacity(3, 0, 1));
        garden.WildGrowth = false;
        var a = Add(garden, garden.Core, NodeKind.Stem);
        var b = Add(garden, a, NodeKind.Stem);
        var c = Add(garden, b, NodeKind.Stem);
        foreach (var n in garden.Nodes) n.Power = 0;
        garden.Grow(60);
        Assert.True(garden.Core.Fill > a.Fill && a.Fill > b.Fill && b.Fill >= c.Fill);
        garden.Grow(6 * 3600);
        Assert.All(garden.Nodes, n => Assert.True(n.Fill > 0.9f)); // a gentle slope carries the upkeep out
    }

    [Fact]
    public void LinksFlashAsOftenAsPowerRunsAlongThem()
    {
        // core - stem - stem, the last drained: power rushes out along both links and they flash busily. Once the
        // plant has filled up only the stems' upkeep still flows, a trickle, and they flash only now and then.
        var garden = NewGarden(new Capacity(2, 0, 1));
        garden.WildGrowth = false;
        var near = Add(garden, garden.Core, NodeKind.Stem);
        var far = Add(garden, near, NodeKind.Stem);
        far.Power = 0;
        garden.Grow(Tuning.EconomyStepSeconds);
        Assert.All(garden.Links, l => Assert.True(l.FlowPerHour > 0));

        // Flashes on any link over 10 real seconds (the flow rate holds between economy steps).
        int Flashes()
        {
            int count = 0;
            for (int tick = 0; tick < 10 * Tuning.TicksPerSecond; tick++)
            {
                var before = garden.Links.Select(l => l.Flash).ToList();
                garden.Step();
                for (int i = 0; i < garden.Links.Count; i++)
                {
                    var link = garden.Links[i];
                    if (link.Flash <= before[i]) continue;
                    count++;
                    // As bright as the part it reached is full.
                    var reached = link.FlowsToTarget ? link.Target : link.Origin;
                    Assert.Equal(Tuning.FlashPeak * reached.Fill, link.Flash, Tolerance);
                }
            }
            return count;
        }
        int rushing = Flashes();

        garden.Grow(6 * 3600);
        Assert.All(garden.Links, l => Assert.True(l.FlowPerHour < 1.1f * P));
        int trickling = Flashes();
        Assert.True(rushing > 3 * trickling, $"{rushing} flashes while filling, {trickling} once full");
        Assert.True(rushing <= 2 * 10 * Tuning.MaxFlashesPerSecond * 1.1f);
    }

    [Fact]
    public void PartsShowTheirFillAndBounceWhenPowerArrives()
    {
        var garden = NewGarden(new Capacity(1, 0, 1));
        garden.WildGrowth = false;
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        stem.Power = 0;
        garden.SettleLooks();
        Assert.Equal(0, stem.FillLook);

        // Power rushes in: the link flashes, the stem bounces past its level, dips a little below, and settles.
        garden.Grow(Tuning.EconomyStepSeconds);
        Assert.True(garden.Links[0].FlowsToTarget);
        float peak = 0, dip = 0;
        for (int tick = 0; tick < 2 * Tuning.TicksPerSecond; tick++)
        {
            garden.Step();
            peak = Math.Max(peak, stem.Bounce);
            dip = Math.Min(dip, stem.Bounce);
        }
        Assert.InRange(peak, 0.05f, Tuning.BounceMax);
        Assert.InRange(dip, -peak / 2, -0.001f);
        Assert.Equal(0, garden.Core.Bounce); // the core sent it

        // The shown fill catches up with the real one.
        Assert.Equal(stem.Fill, stem.FillLook, 0.01f);
    }

    // ==== WILTING ==== \\

    // Grows for hours, a minute at a time, and returns every part that wilted meanwhile. (An overload cycles: the
    // hungry labs wilt, stop draining, refill to RecoverFill, recover and drain again, so one moment can miss it.)
    static HashSet<PlantNode> WiltedDuring(Garden garden, int hours)
    {
        var wilted = new HashSet<PlantNode>();
        for (int minute = 0; minute < hours * 60; minute++)
        {
            garden.Grow(60);
            wilted.UnionWith(garden.Nodes.Where(n => n.Wilting));
        }
        return wilted;
    }

    [Fact]
    public void OverloadWiltsButNeverDestroys()
    {
        var (garden, _, _) = Overloaded();
        var parts = garden.Nodes.ToList();
        Assert.NotEmpty(WiltedDuring(garden, 12));
        Assert.All(parts, n => Assert.Contains(n, garden.Nodes));
        Assert.All(garden.Nodes, n => Assert.True(n.Power >= 0));
        Assert.False(garden.Core.Wilting);
        var (production, _) = garden.PowerBalance();
        Assert.True(production == 8 * P);
    }

    [Fact]
    public void FarTipsWiltFirst()
    {
        var (garden, fork, _) = Overloaded();
        var wilted = WiltedDuring(garden, 12);
        Assert.DoesNotContain(fork.Parent!, wilted); // the trunk, next to the core, keeps going
        Assert.Contains(wilted, n => n.Spec.Kind == NodeKind.Lab);
    }

    [Fact]
    public void PruningTheOverloadRecovers()
    {
        var (garden, _, hungry) = Overloaded();
        Assert.NotEmpty(WiltedDuring(garden, 12));

        garden.Prune(hungry); // 6.5 an hour of upkeep left against 8
        garden.Grow(12 * 3600);
        Assert.Empty(WiltedDuring(garden, 6));
    }

    [Fact]
    public void WiltingNodesRecoverByThemselves()
    {
        // A part at zero power, wilted, with the core beside it: it refills and recovers with no help.
        var garden = NewGarden(new Capacity(1, 0, 1));
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        stem.Power = 0;
        stem.Wilting = true;
        garden.Core.Power = 0;
        garden.Grow(3600);
        Assert.False(stem.Wilting);
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
        // The plant is stupid: a drained garden's grove still sends what blobs it can, and they grow buds that only
        // add to the drain. Pruning is the player's job.
        var garden = NewGarden(new Capacity(30, 0, 1));
        var stem = Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Stem);
        Add(garden, stem, NodeKind.Grove);
        for (int i = 0; i < 4; i++) stem = Add(garden, stem, NodeKind.Stem);
        for (int i = 0; i < 2; i++) Add(garden, stem, NodeKind.Lab);
        garden.Grow(3 * 24 * 3600);
        var (production, upkeep) = garden.PowerBalance();
        Assert.True(MainNodes(garden) > 12);
        Assert.True(Wilting(garden) > 0 || upkeep > production - 1, $"+{production} -{upkeep}");
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
            Assert.Equal(e.Wilting, a.Wilting);
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
        Assert.Equal(Wilting(once), Wilting(live));
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
