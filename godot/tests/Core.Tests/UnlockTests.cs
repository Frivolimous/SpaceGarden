namespace SpaceGarden.Core.Tests;

public class UnlockTests
{
    const double Hour = 3600, Day = 24 * Hour;
    static readonly double BlobHours = 1 / Tuning.LabBlobsPerHour;

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
        (NodeSpecs.All[kind].IsFruit ? garden.GrowFruit(parent) : garden.SproutFrom(parent, kind))
            ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

    static int Count(Garden garden, NodeKind kind) => garden.Nodes.Count(n => n.Spec.Kind == kind);

    // core - stem - lab, full of power, nothing else growing: a lab's blob can only go to the stem, then the core.
    static (Garden Garden, PlantNode Lab) WithALab()
    {
        var garden = NewGarden(new Capacity(1, 0, 1));
        garden.WildGrowth = false;
        var lab = Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Lab);
        foreach (var n in garden.Nodes) n.Power = n.Store;
        return (garden, lab);
    }

    // ==== GENES ==== \\

    [Fact]
    public void ALabSendsGenesToTheCore()
    {
        var (garden, lab) = WithALab();
        garden.Grow(Hour);
        // Its blobs can only reach the core (lab - stem - core), at nearly full power: about six genes an hour.
        Assert.InRange(garden.Genes, 0.75 * Tuning.LabBlobsPerHour * Tuning.BlobGenes, Tuning.LabBlobsPerHour * Tuning.BlobGenes);
        Assert.InRange(lab.Blob, 0, 1);
        Assert.False(lab.Wilting);
    }

    [Fact]
    public void GeneBlobsWanderAndOnlyTheCoreTakesThem()
    {
        // core - stem - fork, the fork carrying the lab and a long side branch: blobs wander down it as often as up
        // to the core, and those that don't find the core in time fade with their genes.
        var garden = NewGarden(new Capacity(22, 0, 1), fixedCaps: true);
        garden.WildGrowth = false;
        var fork = Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Stem);
        var lab = Add(garden, fork, NodeKind.Lab);
        var tip = fork;
        for (int i = 0; i < 20; i++) tip = Add(garden, tip, NodeKind.Stem);
        foreach (var n in garden.Nodes) n.Power = n.Store;
        for (int i = 0; i < 200; i++)
        {
            foreach (var n in garden.Nodes) n.Power = n.Store;
            lab.Blob = 1;
            garden.Grow(Tuning.EconomyStepSeconds);
        }
        Assert.All(garden.Motes, m => Assert.Equal(lab, m.Route[0]));
        Assert.InRange(garden.Genes / Tuning.BlobGenes, 40, 200); // some found the core, some didn't
        Assert.DoesNotContain(garden.Nodes, n => n.Spec.IsFruit || n.Forming != null); // and they grew nothing
    }

    [Fact]
    public void GeneBlobsTravelUpToTheCore()
    {
        var (garden, lab) = WithALab();
        garden.Grow(BlobHours * Hour * 1.5);
        var blob = Assert.Single(garden.Motes);
        Assert.Equal([lab, lab.Parent!, garden.Core], blob.Route);

        var visited = new List<PlantNode> { blob.From };
        for (int tick = 0; tick < 5 * Tuning.TicksPerSecond && garden.Motes.Count > 0; tick++)
        {
            garden.Step();
            if (garden.Motes.Count > 0 && garden.Motes[0].From != visited[^1]) visited.Add(garden.Motes[0].From);
        }
        Assert.Empty(garden.Motes); // it arrived and was absorbed
        Assert.Equal([lab, lab.Parent!], visited);
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
        Assert.Equal(0, lab.Blob);
    }

    [Fact]
    public void ALowLabMakesFewerGenes()
    {
        var (garden, lab) = WithALab();
        Assert.Equal(Tuning.LabBlobsPerHour, Garden.BlobsPerHour(lab), 1e-4f);
        lab.Power = 0;
        Assert.Equal(Tuning.LabBlobsPerHour * Tuning.OutputFloor, Garden.BlobsPerHour(lab), 1e-4f);
    }

    [Fact]
    public void ResearchFruitRaisesALabsGenes()
    {
        var (garden, lab) = WithALab();
        for (int i = 0; i < 3; i++) Add(garden, lab, NodeKind.Research);
        Assert.Equal(Tuning.LabBlobsPerHour * 1.2f, Garden.BlobsPerHour(lab), 1e-4f);
    }

    [Fact]
    public void CatchUpShowsNoBlobsButDeliversTheirGenes()
    {
        var (caught, _) = WithALab();
        var (live, _) = WithALab();
        caught.CatchUp(10 * Hour);
        for (int i = 0; i < (int)(10 * Hour / 7); i++) live.Grow(7);
        live.Grow(10 * Hour % 7);
        Assert.Empty(caught.Motes);
        Assert.True(caught.Genes > 40);
        Assert.Equal(live.Genes, caught.Genes, 1e-3);
        Assert.True(caught.ShowBlobs);
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
        // A core, stems and a grove make none: the first unlock waits for the first lab.
        var garden = NewGarden();
        garden.WildGrowth = false;
        Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Grove);
        garden.Grow(Day);
        Assert.DoesNotContain(garden.Nodes, n => n.Spec.Kind == NodeKind.Lab);
        Assert.Equal(0, garden.Genes);
        Assert.Equal(0, garden.Unlocks);
    }

    // A garden a smart player has tended for a while (see SmartPlayer).
    static Garden Tended(int seed, double hours)
    {
        var garden = Planted(seed);
        for (double t = 0; t < hours; t += SmartPlayer.CheckHours)
        {
            if (SmartPlayer.Awake(t)) SmartPlayer.Tend(garden);
            garden.Grow(Math.Min(SmartPlayer.CheckHours, hours - t) * Hour);
        }
        return garden;
    }

    [Fact]
    public void UnlocksSurviveSaveAndLoad()
    {
        var garden = Tended(7, 2 * 24 + 5);
        Assert.True(garden.Unlocks >= 3);
        var loaded = Garden.FromSave(garden.ToSave(), seed: 7);
        Assert.Equal(garden.Genes, loaded.Genes);
        Assert.Equal(garden.Unlocks, loaded.Unlocks);
        Assert.Equal(garden.Capacity, loaded.Capacity);
        Assert.Equal(garden.Core.MaxLinks, loaded.Core.MaxLinks);
        for (int i = 0; i < garden.Nodes.Count; i++)
        {
            Assert.Equal(garden.Nodes[i].Blob, loaded.Nodes[i].Blob);
            Assert.Equal(garden.Nodes[i].Forming, loaded.Nodes[i].Forming);
            Assert.Equal(garden.Nodes[i].Growth, loaded.Nodes[i].Growth);
        }
    }

    [Fact]
    public void CatchUpUnlocksAsLivingThroughItDoes()
    {
        var caught = Tended(7, 10);
        var live = Tended(7, 10);
        caught.CatchUp(Tuning.CatchUpCapSeconds);
        for (int i = 0; i < (int)(Tuning.CatchUpCapSeconds / 7); i++) live.Grow(7);
        live.Grow(Tuning.CatchUpCapSeconds % 7);
        Assert.True(caught.Unlocks >= 3);
        Assert.Equal(live.Unlocks, caught.Unlocks);
        Assert.Equal(live.Capacity, caught.Capacity);
        Assert.Equal(live.Genes, caught.Genes, 1e-3);
        Assert.Equal(live.Nodes.Select(n => (n.Spec.Kind, n.Parent?.Id)), caught.Nodes.Select(n => (n.Spec.Kind, n.Parent?.Id)));
    }

    [Fact]
    public void RaisedCapsGiveWildGrowthRoom()
    {
        // The player plants a grove and a lab, then leaves it: the unlocks the lab earns fill with wild stems.
        var garden = Planted();
        var stem = garden.Nodes[1];
        Add(garden, stem, NodeKind.Grove);
        Add(garden, Add(garden, stem, NodeKind.Stem), NodeKind.Lab); // leaving the second stem a free link
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

    // ==== PACING ==== \

    // When each unlock should fire, in hours from planting (see Tuning.UnlockTrack; Day N is the start of day N):
    // ~4 h, ~6 h, day 1 evening, days 2-7, days 8-14, then every other day to day 30.
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
            + $"{Count(garden, NodeKind.Lab)} labs, {Count(garden, NodeKind.Grove)} groves, {garden.Core.Links}/{garden.Capacity.CoreLinks} core links, "
            + $"{garden.Nodes.Count(n => !n.Spec.IsFruit)} main, {garden.Nodes.Count(n => n.Spec.IsFruit)} fruit, "
            + $"{garden.Nodes.Count(n => n.Wilting)} wilting ({string.Join(" ", garden.Nodes.Where(n => n.Wilting).GroupBy(n => n.Spec.Kind).Select(g => $"{g.Count()} {g.Key}"))}), genes {garden.Genes:0}, +{production:0.0}/h -{upkeep:0.0}/h";
    }

    sealed record Run(double[,] Fired, double[,] GenesAt, int[] WeekOne, int[] Month);

    // A month from planting for a few seeds, a smart player tending it (see SmartPlayer): when each unlock fired, the
    // genes at each target time, and the garden on some days.
    static Run Month(Action<string>? log)
    {
        var seeds = new[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var shownDays = new[] { 1, 3, 7, 10, 14, 21, 30 };
        var run = new Run(new double[seeds.Length, Tuning.UnlockTrack.Length], new double[seeds.Length, TargetHours.Length],
            new int[seeds.Length], new int[seeds.Length]);
        for (int s = 0; s < seeds.Length; s++)
            for (int u = 0; u < Tuning.UnlockTrack.Length; u++) run.Fired[s, u] = double.PositiveInfinity;
        const double step = 600;
        // The seeds run side by side (each garden is its own), their lines kept in seed order.
        var lines = seeds.Select(_ => new List<string>()).ToArray();
        Parallel.For(0, seeds.Length, s =>
        {
            var garden = Planted(seeds[s]);
            double t = 0;
            lines[s].Add($"seed {seeds[s]}:");
            for (int day = 1; day <= 30; day++)
            {
                for (; t < day * Day; t += step)
                {
                    double hours = t / Hour;
                    if (Math.Abs(hours % SmartPlayer.CheckHours) < 1e-6 && SmartPlayer.Awake(hours)) SmartPlayer.Tend(garden);
                    int before = garden.Unlocks;
                    garden.Grow(step);
                    for (int u = before; u < garden.Unlocks; u++) run.Fired[s, u] = (t + step) / Hour;
                    for (int h = 0; h < TargetHours.Length; h++)
                        if (Math.Abs(TargetHours[h] * Hour - (t + step)) < 1) run.GenesAt[s, h] = garden.Genes;
                }
                if (shownDays.Contains(day)) lines[s].Add($"  day {day,2}: {Describe(garden)}");
                if (day == 7) run.WeekOne[s] = garden.Unlocks;
            }
            run.Month[s] = garden.Unlocks;
        });
        foreach (var line in lines.SelectMany(l => l)) log?.Invoke(line);
        return run;
    }

    static double Median(double[,] values, int column)
    {
        var sorted = Enumerable.Range(0, values.GetLength(0)).Select(s => values[s, column]).Order().ToList();
        return sorted[sorted.Count / 2];
    }

    // Prints the curve (for tuning), and checks every unlock's median lands within a loose band of its target and
    // every garden keeps up (see the end).
    [Fact]
    public void MonthPacing()
    {
        Assert.Equal(Tuning.UnlockTrack.Length, TargetHours.Length);
        var text = new System.Text.StringBuilder();
        void Log(string line)
        {
            TestContext.Current.TestOutputHelper?.WriteLine(line);
            text.AppendLine(line);
        }
        var run = Month(Log);
        var medians = new List<double>();
        for (int u = 0; u < Tuning.UnlockTrack.Length; u++)
        {
            var hours = Enumerable.Range(0, run.Fired.GetLength(0)).Select(s => run.Fired[s, u]).Order().ToList();
            double median = Median(run.Fired, u);
            Log($"unlock {u + 1,2} at {Tuning.UnlockTrack[u].Genes,6:0} genes (target {TargetHours[u]:0.#} h, "
                + $"day {TargetHours[u] / 24 + 1:0.#}): median {median:0.#} h, range {hours[0]:0.#}-{hours[^1]:0.#} h; "
                + $"genes at target: median {Median(run.GenesAt, u):0}");
            medians.Add(median);
        }
        for (int u = 0; u < Tuning.UnlockTrack.Length; u++)
            Assert.InRange(medians[u], TargetHours[u] * 0.75, TargetHours[u] * 1.25 + 0.2);
        // Every garden has had week one's nine steps (DESIGN.md's 31 stems, 8 generators, 2 core links) by the end of
        // day 7, and is at most two steps short of the end of the track by the end of day 30 (at real-speed blob rates
        // gardens spread out: the slowest seed runs about a fifth behind the median).
        Assert.All(run.WeekOne, u => Assert.True(u >= 9, $"{u} unlocks by day 7"));
        Assert.All(run.Month, u => Assert.True(u >= Tuning.UnlockTrack.Length - 2, $"{u} unlocks by day 30"));
        // PACING=1 shows the curve even when it passes (as the skip message), for tuning.
        if (Environment.GetEnvironmentVariable("PACING") == "1") Assert.Skip(text.ToString());
    }

    // For retuning the thresholds after a change to the economy: sets each one to the median genes at its target
    // time, runs again, and repeats until they settle, then reports them and the last run's gardens (as the skip
    // message, so it shows). Run it by hand: dotnet test -- --filter-method "*RetuneThresholds*" with RETUNE=1 set.
    [Fact]
    public void RetuneThresholds()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("RETUNE") == "1", "a tuning aid, run by hand (RETUNE=1)");
        var report = new System.Text.StringBuilder();
        var original = Tuning.UnlockTrack.ToArray();
        try
        {
            for (int pass = 0; pass < 6; pass++)
            {
                var run = Month(pass == 5 ? line => report.AppendLine(line) : null);
                double previous = 0;
                for (int u = 0; u < Tuning.UnlockTrack.Length; u++)
                {
                    double median = Median(run.GenesAt, u);
                    // Rounded to three figures, so the table reads easily.
                    double scale = median < 100 ? 1 : Math.Pow(10, Math.Floor(Math.Log10(median)) - 2);
                    double genes = Math.Max(previous + 1, Math.Round(median / scale) * scale);
                    Tuning.UnlockTrack[u] = Tuning.UnlockTrack[u] with { Genes = genes };
                    previous = genes;
                }
                report.AppendLine($"pass {pass + 1}: " + string.Join(", ", Tuning.UnlockTrack.Select(s => s.Genes)));
            }
        }
        finally
        {
            for (int u = 0; u < original.Length; u++) Tuning.UnlockTrack[u] = original[u];
        }
        Assert.Skip(report.ToString());
    }
}
