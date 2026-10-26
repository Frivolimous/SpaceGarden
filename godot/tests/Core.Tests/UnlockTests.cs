namespace SpaceGarden.Core.Tests;

public class UnlockTests
{
    const double Hour = 3600, Day = 24 * Hour;

    static Garden NewGarden(Capacity? capacity = null, int seed = 7, bool fixedCaps = false)
    {
        var garden = Garden.New(Layout.Radial, seed);
        if (capacity != null) garden.Capacity = capacity;
        if (fixedCaps) garden.Unlocks = Tuning.UnlockTrack.Length; // the track done: no raises
        return garden;
    }

    // A new garden as the widget plants it: the core and a first stem.
    static Garden Planted(int seed = 7)
    {
        var garden = NewGarden(seed: seed);
        garden.Sprout();
        return garden;
    }

    static PlantNode Add(Garden garden, PlantNode parent, NodeKind kind) =>
        garden.SproutFrom(parent, kind) ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

    static int Count(Garden garden, NodeKind kind) => garden.Nodes.Count(n => n.Spec.Kind == kind);

    // core - stem - stem - lab, full of power, nothing else growing.
    static (Garden Garden, PlantNode Lab) WithALab()
    {
        var garden = NewGarden(new Capacity(2, 0, 1));
        garden.WildGrowth = false;
        var lab = Add(garden, Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Stem), NodeKind.Lab);
        foreach (var n in garden.Nodes) n.Power = n.Store;
        return (garden, lab);
    }

    // ==== GENES ==== \\

    [Fact]
    public void ALabSendsGenesToTheCore()
    {
        var (garden, lab) = WithALab();
        garden.Grow(Hour);
        // Its six an hour, every one of them a blob that reached the core.
        Assert.Equal(Tuning.LabGenesPerHour, garden.Genes, 1e-3);
        Assert.InRange(lab.Genes, 0, Tuning.GeneBlobSize);
        Assert.False(lab.Wilting);
    }

    [Fact]
    public void GeneBlobsTravelUpToTheCore()
    {
        var (garden, lab) = WithALab();
        garden.Grow(Hour / Tuning.LabGenesPerHour); // one blob's worth
        var blob = Assert.Single(garden.Motes);
        Assert.True(blob.Homing);
        Assert.Equal(lab, blob.From);

        var visited = new List<PlantNode> { blob.From };
        for (int tick = 0; tick < 5 * Tuning.TicksPerSecond && garden.Motes.Count > 0; tick++)
        {
            garden.Step();
            if (garden.Motes.Count > 0 && garden.Motes[0].From != visited[^1]) visited.Add(garden.Motes[0].From);
        }
        Assert.Empty(garden.Motes); // it arrived and was absorbed
        Assert.Equal([lab, lab.Parent!, lab.Parent!.Parent!], visited);
        Assert.True(garden.Core.Glow > 0);
    }

    [Fact]
    public void AWiltingLabMakesNoGenes()
    {
        var (garden, lab) = WithALab();
        lab.Power = 0;
        lab.Wilting = true;
        foreach (var n in garden.Nodes) if (n != lab) n.Power = 0;
        garden.Grow(Tuning.EconomyStepSeconds);
        Assert.True(lab.Wilting);
        Assert.Equal(0, lab.Genes);
    }

    [Fact]
    public void ResearchFruitRaisesALabsGenes()
    {
        var (garden, lab) = WithALab();
        for (int i = 0; i < 3; i++) Add(garden, lab, NodeKind.Research);
        Assert.Equal(Tuning.LabGenesPerHour * 1.2f, Garden.GenesPerHour(lab), 1e-4f);
    }

    [Fact]
    public void CatchUpShowsNoBlobsButDeliversTheirGenes()
    {
        var (garden, _) = WithALab();
        garden.CatchUp(10 * Hour);
        Assert.Empty(garden.Motes);
        Assert.Equal(10 * Tuning.LabGenesPerHour, garden.Genes, 1e-2);
        Assert.True(garden.ShowBlobs);
    }

    // ==== UNLOCKS ==== \\

    [Fact]
    public void GenesRaiseTheCapsAlongTheTrack()
    {
        var garden = NewGarden();
        Assert.Equal(Capacity.Start, garden.Capacity);
        var first = Tuning.UnlockTrack[0];
        garden.Deliver(first.Genes - 0.01);
        Assert.Equal(0, garden.Unlocks);
        garden.Deliver(0.01);
        Assert.Equal(1, garden.Unlocks);
        Assert.Equal(Capacity.Start.Raise(first), garden.Capacity);

        // A big delivery passes several thresholds at once, each applied in turn.
        garden.Deliver(Tuning.UnlockTrack[4].Genes);
        Assert.Equal(5, garden.Unlocks);
        Assert.Equal(new Capacity(19, 4, 2), garden.Capacity); // day 3: +1 core link, +5 stems, +1 generator
        Assert.Equal(2, garden.Core.MaxLinks);
    }

    [Fact]
    public void TheTrackReachesTheDocsWeekOneThenAMonth()
    {
        var garden = NewGarden();
        garden.Deliver(Tuning.UnlockTrack[8].Genes);
        Assert.Equal(9, garden.Unlocks);
        Assert.Equal(new Capacity(31, 8, 2), garden.Capacity); // DESIGN.md: 31 stems, 8 generators, 2 core links
        garden.Deliver(1e9);
        Assert.Null(garden.NextUnlock);
        Assert.Equal(Tuning.UnlockTrack.Length, garden.Unlocks);
        Assert.Equal(new Capacity(72, 17, 3), garden.Capacity);
    }

    [Fact]
    public void ThresholdsOnlyRise()
    {
        for (int i = 1; i < Tuning.UnlockTrack.Length; i++)
            Assert.True(Tuning.UnlockTrack[i].Genes > Tuning.UnlockTrack[i - 1].Genes);
    }

    [Fact]
    public void OnlyLabsMakeGenes()
    {
        // A core and stems alone make none: the first unlock waits for the first lab.
        var garden = NewGarden();
        garden.WildGrowth = false;
        Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Stem);
        garden.Grow(Day);
        Assert.Equal(0, garden.Genes);
        Assert.Equal(0, garden.Unlocks);
    }

    [Fact]
    public void UnlocksSurviveSaveAndLoad()
    {
        var garden = Planted();
        garden.Grow(2 * Day + 17);
        Assert.True(garden.Unlocks >= 3);
        var loaded = Garden.FromSave(garden.ToSave(), seed: 7);
        Assert.Equal(garden.Genes, loaded.Genes);
        Assert.Equal(garden.Unlocks, loaded.Unlocks);
        Assert.Equal(garden.Capacity, loaded.Capacity);
        Assert.Equal(garden.Core.MaxLinks, loaded.Core.MaxLinks);
        for (int i = 0; i < garden.Nodes.Count; i++) Assert.Equal(garden.Nodes[i].Genes, loaded.Nodes[i].Genes);

        // And carry on from there as the original does (wild growth off: the loaded garden's dice are rolled afresh).
        garden.WildGrowth = loaded.WildGrowth = false;
        garden.Grow(Day);
        loaded.Grow(Day);
        Assert.Equal(garden.Unlocks, loaded.Unlocks);
        Assert.Equal(garden.Genes, loaded.Genes, 1e-3);
    }

    [Fact]
    public void CatchUpUnlocksAsLivingThroughItDoes()
    {
        var caught = Planted();
        var live = Planted();
        caught.CatchUp(Tuning.CatchUpCapSeconds);
        for (int i = 0; i < Tuning.CatchUpCapSeconds / 7; i++) live.Grow(7);
        live.Grow(Tuning.CatchUpCapSeconds % 7);
        Assert.True(caught.Unlocks >= 4);
        Assert.Equal(live.Unlocks, caught.Unlocks);
        Assert.Equal(live.Capacity, caught.Capacity);
        Assert.Equal(live.Genes, caught.Genes, 1e-3);
        Assert.Equal(live.Nodes.Count, caught.Nodes.Count);
    }

    [Fact]
    public void RaisedCapsGiveWildGrowthRoom()
    {
        var garden = Planted();
        garden.Grow(Day);
        int stemsBefore = Count(garden, NodeKind.Stem);
        int unlocks = garden.Unlocks;
        garden.Grow(3 * Day);
        Assert.True(garden.Unlocks > unlocks);
        Assert.True(Count(garden, NodeKind.Stem) > stemsBefore);
        Assert.True(Count(garden, NodeKind.Stem) <= garden.Capacity.Stems);
        Assert.True(Count(garden, NodeKind.Generator) <= garden.Capacity.Generators);
        Assert.True(garden.Core.Links <= garden.Capacity.CoreLinks);
    }

    // ==== WILD GROWTH ==== \\

    [Fact]
    public void WildGrowthNeverGrowsIntoADeficit()
    {
        // Plenty of room, no generator to grow: wild growth stops adding upkeep while it'd eat the reserve.
        var garden = NewGarden(new Capacity(100, 0, 1), fixedCaps: true);
        garden.Sprout();
        for (int day = 0; day < 6; day++)
        {
            garden.Grow(Day);
            var (production, upkeep) = garden.PowerBalance();
            Assert.True(upkeep <= production - Tuning.WildSpareReserve + 1e-3f, $"day {day + 1}: +{production} -{upkeep}");
        }
        Assert.Equal(0, garden.Nodes.Count(n => n.Wilting));
    }

    [Fact]
    public void WildGrowthOnlyAddsWhatTheGardenCanAfford()
    {
        // 8 an hour against a lab's 3 and 4 stems' 2: 3 spare, so a stem fits but another lab doesn't (labs leave 30% of
        // the production unspent). Forty sprouts later it has never eaten into the reserve.
        var garden = NewGarden(new Capacity(100, 0, 1), fixedCaps: true);
        var tip = garden.Core;
        for (int i = 0; i < 4; i++) tip = Add(garden, tip, NodeKind.Stem);
        Add(garden, tip.Parent!, NodeKind.Lab);
        var spare = garden.WildSpare();
        Assert.Equal(8 - 3 - 2 - Tuning.WildSpareReserve, spare.Any, 1e-4f);
        Assert.True(spare.Affords(NodeKind.Stem));
        Assert.False(spare.Affords(NodeKind.Lab));
        foreach (var n in garden.Nodes) n.Power = n.Store;
        for (int i = 0; i < 40; i++)
        {
            garden.GrowthClock = Tuning.SproutEverySeconds;
            foreach (var n in garden.Nodes) n.Power = n.Store;
            garden.Grow(Tuning.EconomyStepSeconds);
            var (production, upkeep) = garden.PowerBalance();
            Assert.True(production - upkeep >= Tuning.WildSpareReserve - 1e-3f, $"+{production} -{upkeep}");
        }
        Assert.True(garden.Nodes.Count > 10);
    }

    [Fact]
    public void AStemByTheCoreFillsItsFreeLinkOnceStemsAreCapped()
    {
        // core - stem (two free links) with the stem cap reached: it used to be left bare forever.
        var garden = NewGarden(new Capacity(1, 1, 1), fixedCaps: true);
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        garden.Grow(3 * Day);
        Assert.Equal(3, stem.Links);
        Assert.All(stem.Branches, b => Assert.NotEqual(NodeKind.Stem, b.Spec.Kind));
    }

    // ==== PACING ==== \

    // When each unlock should fire, in hours from planting (see Tuning.UnlockTrack; Day N is the start of day N):
    // the first lab's first blob, ~6 h, day 1 evening, days 2-7, days 8-14, then every other day to day 30.
    static readonly double[] TargetHours =
    [
        4, 6, 10,
        .. Enumerable.Range(2, 13).Select(day => (day - 1) * 24.0),
        .. Enumerable.Range(8, 8).Select(i => (2 * i - 1) * 24.0),
    ];

    static string Describe(Garden garden)
    {
        var (production, upkeep) = garden.PowerBalance();
        return $"{Count(garden, NodeKind.Stem)}/{garden.Capacity.Stems} stems, "
            + $"{Count(garden, NodeKind.Generator)}/{garden.Capacity.Generators} generators, "
            + $"{Count(garden, NodeKind.Lab)} labs, {garden.Core.Links}/{garden.Capacity.CoreLinks} core links, "
            + $"{garden.Nodes.Count(n => !n.Spec.IsFruit)} main, {garden.Nodes.Count(n => n.Spec.IsFruit)} fruit, "
            + $"{garden.Nodes.Count(n => n.Wilting)} wilting, genes {garden.Genes:0}, +{production:0.0}/h -{upkeep:0.0}/h";
    }

    // A month of wild growth from planting for a few seeds: when each unlock fired, and the garden on some days.
    // Prints the curve (for tuning), and checks every unlock's median lands within a loose band of its target and
    // every garden keeps up (see the end).
    [Fact]
    public void MonthPacing()
    {
        Assert.Equal(Tuning.UnlockTrack.Length, TargetHours.Length);
        var log = TestContext.Current.TestOutputHelper;
        var seeds = new[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var shownDays = new[] { 1, 3, 7, 10, 14, 21, 30 };
        var fired = new double[seeds.Length, Tuning.UnlockTrack.Length];
        var genesAt = new double[seeds.Length, TargetHours.Length];
        var weekOne = new int[seeds.Length];
        var month = new int[seeds.Length];
        for (int s = 0; s < seeds.Length; s++)
            for (int u = 0; u < Tuning.UnlockTrack.Length; u++) fired[s, u] = double.PositiveInfinity;
        for (int s = 0; s < seeds.Length; s++)
        {
            var garden = Planted(seeds[s]);
            double t = 0;
            log?.WriteLine($"seed {seeds[s]}:");
            for (int day = 1; day <= 30; day++)
            {
                for (; t < day * Day; t += 600)
                {
                    int before = garden.Unlocks;
                    garden.Grow(600);
                    for (int u = before; u < garden.Unlocks; u++) fired[s, u] = (t + 600) / Hour;
                    for (int h = 0; h < TargetHours.Length; h++)
                        if (Math.Abs(TargetHours[h] * Hour - (t + 600)) < 1) genesAt[s, h] = garden.Genes;
                }
                if (shownDays.Contains(day)) log?.WriteLine($"  day {day,2}: {Describe(garden)}");
                if (day == 7) weekOne[s] = garden.Unlocks;
            }
            month[s] = garden.Unlocks;
        }

        var medians = new List<double>();
        for (int u = 0; u < Tuning.UnlockTrack.Length; u++)
        {
            var hours = Enumerable.Range(0, seeds.Length).Select(s => fired[s, u]).Order().ToList();
            var genes = Enumerable.Range(0, seeds.Length).Select(s => genesAt[s, u]).Order().ToList();
            double median = hours[hours.Count / 2];
            log?.WriteLine($"unlock {u + 1,2} at {Tuning.UnlockTrack[u].Genes,6:0} genes (target {TargetHours[u]:0.#} h, "
                + $"day {TargetHours[u] / 24 + 1:0.#}): median {median:0.#} h, range {hours[0]:0.#}-{hours[^1]:0.#} h; "
                + $"genes at target: median {genes[genes.Count / 2]:0}");
            medians.Add(median);
        }
        for (int u = 0; u < Tuning.UnlockTrack.Length; u++)
            Assert.InRange(medians[u], TargetHours[u] * 0.75, TargetHours[u] * 1.25 + 0.2);
        // Every garden has had week one's nine steps (DESIGN.md's 31 stems, 8 generators, 2 core links) by the end of
        // day 7, and is at most one step short of the end of the track by the end of day 30.
        Assert.All(weekOne, u => Assert.True(u >= 9, $"{u} unlocks by day 7"));
        Assert.All(month, u => Assert.True(u >= Tuning.UnlockTrack.Length - 1, $"{u} unlocks by day 30"));
    }
}
