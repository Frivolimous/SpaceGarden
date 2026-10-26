namespace SpaceGarden.Core.Tests;

public class EconomyTests
{
    const float Tolerance = 1e-4f;
    static readonly Capacity Roomy = new(100, 100, 2);

    static Garden NewGarden(Capacity? capacity = null)
    {
        var garden = Garden.New(Layout.Radial, seed: 7);
        if (capacity != null) garden.Capacity = capacity;
        return garden;
    }

    static PlantNode Add(Garden garden, PlantNode parent, NodeKind kind) =>
        garden.SproutFrom(parent, kind) ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

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
        Assert.Equal((8f, 0f), garden.PowerBalance());
    }

    [Fact]
    public void RatesFollowTheDraftTable()
    {
        var garden = NewGarden(Roomy);
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        var generator = Add(garden, stem, NodeKind.Generator);
        var lab = Add(garden, stem, NodeKind.Lab);

        Assert.Equal(4, Garden.ProductionPerHour(generator));
        Assert.Equal(3, Garden.UpkeepPerHour(lab));
        Assert.Equal(0.5f, Garden.UpkeepPerHour(stem));
        Assert.Equal((12f, 3.5f), garden.PowerBalance());
    }

    [Fact]
    public void LeavesHalveAStemsUpkeep()
    {
        var garden = NewGarden(Roomy);
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        Add(garden, stem, NodeKind.Leaf);
        Assert.Equal(0.25f, Garden.UpkeepPerHour(stem));
        Add(garden, stem, NodeKind.Leaf);
        Assert.Equal(0.125f, Garden.UpkeepPerHour(stem));
    }

    [Fact]
    public void GeneratorFruitAddsUpToAFifth()
    {
        var garden = NewGarden(Roomy);
        var generator = Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Generator);
        for (int i = 0; i < 3; i++) Add(garden, generator, NodeKind.Burr);
        Assert.Equal(4 * 1.2f, Garden.ProductionPerHour(generator), Tolerance);
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
        Assert.Equal(before + (8 - 0.5f) / 6, garden.TotalPower(), Tolerance);
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
                garden.Step();
                count += garden.Links.Count(l => l.Flash == 1);
            }
            return count;
        }
        int rushing = Flashes();

        garden.Grow(6 * 3600);
        Assert.All(garden.Links, l => Assert.True(l.FlowPerHour < 1.1f));
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

    [Fact]
    public void OverloadWiltsButNeverDestroys()
    {
        var (garden, _, _) = Overloaded();
        var parts = garden.Nodes.ToList();
        garden.Grow(12 * 3600);
        Assert.All(parts, n => Assert.Contains(n, garden.Nodes)); // (fruit may have grown meanwhile)
        Assert.True(Wilting(garden) > 0);
        Assert.All(garden.Nodes, n => Assert.True(n.Power >= 0));
        Assert.False(garden.Core.Wilting);
        // Wilting parts stop draining, so what's left costs less than the core makes, near enough.
        var (production, upkeep) = garden.PowerBalance();
        Assert.True(upkeep < 14.5f);
        Assert.True(production == 8);
    }

    [Fact]
    public void FarTipsWiltFirst()
    {
        var (garden, fork, _) = Overloaded();
        garden.Grow(12 * 3600);
        Assert.False(fork.Parent!.Wilting); // the trunk, next to the core, keeps going
        Assert.Contains(garden.Nodes, n => n.Spec.Kind == NodeKind.Lab && n.Wilting);
    }

    [Fact]
    public void PruningTheOverloadRecovers()
    {
        var (garden, _, hungry) = Overloaded();
        garden.Grow(12 * 3600);
        Assert.True(Wilting(garden) > 0);

        garden.Prune(hungry); // 6.5 an hour of upkeep left against 8
        garden.Grow(12 * 3600);
        Assert.Equal(0, Wilting(garden));
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
        var garden = NewGarden();
        garden.Sprout();
        garden.Grow(Tuning.CatchUpCapSeconds);
        Assert.True(garden.Nodes.Count > 5);
        Assert.True(garden.Nodes.Count(n => n.Spec.Kind == NodeKind.Stem) <= 4);
        Assert.True(garden.Nodes.Count(n => n.Spec.Kind == NodeKind.Generator) <= 1);
        Assert.Equal(1, garden.Core.Links);
    }

    [Fact]
    public void WildGrowthWaitsForPower()
    {
        var garden = NewGarden();
        garden.Sprout();
        foreach (var n in garden.Nodes) n.Power = 0;
        // Twenty minutes is a sprout's worth of time, but nothing has power to spare yet.
        Assert.Equal(0, garden.Grow(Tuning.SproutEverySeconds));
        Assert.Equal(2, garden.Nodes.Count);
        Assert.True(garden.Grow(4 * 3600) > 0);
        Assert.True(garden.Nodes.Count > 2);
    }

    [Fact]
    public void ANewGardenSettlesWithinItsMeans()
    {
        // Three days of wild growth from the start: it fills its caps, and whatever labs it grows, the garden keeps
        // going (the core, trunk and generator never wilt). Prints the trajectory, for tuning.
        var garden = NewGarden();
        garden.Sprout();
        for (int hour = 6; hour <= 72; hour += 6)
        {
            garden.Grow(6 * 3600);
            var (production, upkeep) = garden.PowerBalance();
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"{hour,3} h: {MainNodes(garden)} main, {garden.Nodes.Count - MainNodes(garden)} fruit, "
                + $"{garden.Nodes.Count(n => n.Spec.Kind == NodeKind.Lab)} labs, {Wilting(garden)} wilting, "
                + $"power {garden.TotalPower():0.00}, +{production:0.0}/h -{upkeep:0.0}/h");
        }
        Assert.False(garden.Core.Wilting);
        Assert.False(garden.Nodes[1].Wilting);
        Assert.All(garden.Nodes.Where(n => n.Spec.Kind == NodeKind.Generator), n => Assert.False(n.Wilting));
    }

    // ==== CATCH-UP ==== \\

    static Garden Grown()
    {
        var garden = NewGarden();
        garden.Sprout();
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
        }
        Assert.Equal(expected.GrowthClock, actual.GrowthClock, 1e-6);
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
        for (int i = 0; i < 10 * 3600 / 7; i++) live.Grow(7); // uneven chunks
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
