namespace SpaceGarden.Core.Tests;

public class UnlockTests
{
    const double Hour = 3600, Day = 24 * Hour;
    static readonly double BlobHours = 1 / NodeSpecs.All[NodeKind.Lab].BlobsPerHour;

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
        (NodeSpecs.All[kind].IsFruit ? garden.GrowFruit(parent) : garden.GrowBranch(parent, kind))
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
        Assert.InRange(garden.Genes, 0.75 * NodeSpecs.All[NodeKind.Lab].BlobsPerHour * Tuning.BlobGenes, NodeSpecs.All[NodeKind.Lab].BlobsPerHour * Tuning.BlobGenes);
        Assert.InRange(lab.Blob, 0, 1);
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
        Assert.True(garden.Core.Glow > 0); // the core flashes the lab's purple as it absorbs it
        Assert.Equal(lab.Spec.Rgb, garden.Core.GlowRgb);
        Assert.Equal(0, lab.Parent!.Glow); // the stem it passed doesn't
    }

    [Fact]
    public void AnEmptyLabMakesNoGenes()
    {
        var (garden, lab) = WithALab();
        foreach (var n in garden.Nodes) n.Power = 0;
        garden.Grow(Tuning.EconomyStepSeconds);
        Assert.Equal(0, lab.Blob);
    }

    [Fact]
    public void ALowLabMakesFewerGenes()
    {
        var (_, lab) = WithALab();
        Assert.Equal(NodeSpecs.All[NodeKind.Lab].BlobsPerHour, Garden.BlobsPerHour(lab), 1e-4f);
        lab.Power = lab.Store / 4;
        Assert.Equal(NodeSpecs.All[NodeKind.Lab].BlobsPerHour / 4, Garden.BlobsPerHour(lab), 1e-4f);
    }

    [Fact]
    public void ResearchFruitRaisesALabsGenes()
    {
        var (garden, lab) = WithALab();
        for (int i = 0; i < 3; i++) Add(garden, lab, NodeKind.Research);
        Assert.Equal(NodeSpecs.All[NodeKind.Lab].BlobsPerHour * 1.3f, Garden.BlobsPerHour(lab), 0.01f);
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
        Assert.Equal(new Capacity(16, 5, 2, Pots: 2, Trellises: 1, Wells: 1), garden.Capacity); // five steps in: 16 stems, 5 generators, 2 core links, 2 pots, a trellis and a well
        Assert.Equal(2, garden.Core.MaxLinks);
    }

    [Fact]
    public void TheTrackReachesTheDocsWeekOne()
    {
        var garden = NewGarden();
        garden.Deliver(1e9);
        Assert.Null(garden.NextUnlock);
        Assert.Equal(9, garden.Unlocks);
        Assert.Equal(new Capacity(30, 10, 2, Pots: 2, Trellises: 1, Wells: 1), garden.Capacity); // DESIGN.md: 30 stems, 10 generators, 2 core links; 2 pots, a trellis, a well
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
        // The player plants a grove and a lab, and after a day prunes the wild flowers that took every free link: the
        // next unlock, landing then, fills with wild stems.
        var garden = Planted();
        var stem = garden.Nodes[1];
        var grove = Add(garden, stem, NodeKind.Grove);
        var lab = Add(garden, Add(garden, stem, NodeKind.Stem), NodeKind.Lab); // leaving the second stem a free link
        garden.Grow(Day);
        while (garden.Nodes.FirstOrDefault(n => n != grove && n != lab
            && n.Spec.Kind is NodeKind.Grove or NodeKind.Lab or NodeKind.Generator) is { } wild)
        {
            garden.Prune(wild);
            garden.ForgetPrune();
        }
        int stemsBefore = Count(garden, NodeKind.Stem);
        int unlocks = garden.Unlocks;
        garden.Deliver(garden.NextUnlock!.Genes - garden.Genes);
        garden.Grow(Hour);
        Assert.True(garden.Unlocks > unlocks);
        Assert.True(Count(garden, NodeKind.Stem) > stemsBefore);
        Assert.True(Count(garden, NodeKind.Stem) <= garden.Capacity.Stems);
        Assert.True(Count(garden, NodeKind.Generator) <= garden.Capacity.Generators);
        Assert.True(garden.Core.Links <= garden.Capacity.CoreLinks);
    }

    // ==== PACING ==== \

    // When each unlock should fire, in hours from planting (see Tuning.UnlockTrack; Day N is the start of day N): the
    // first four where Jeremy's draft thresholds land (kept as they are: early and easy), the rest spread over week one
    // (the balance pass, Oct 29, retuned 5-9 to these at the real gene delivery, about 40-60%).
    static readonly double[] TargetHours = [1.0 / 3, 1.5, 5, 10, 24, 48, 72, 120, 168];

    static string Describe(Garden garden)
    {
        var (production, upkeep) = garden.PowerBalance();
        return $"{Count(garden, NodeKind.Stem)}/{garden.Capacity.Stems} stems, "
            + $"{Count(garden, NodeKind.Generator)}/{garden.Capacity.Generators} generators, "
            + $"{Count(garden, NodeKind.Lab)} labs, {Count(garden, NodeKind.Grove)} groves, {garden.Core.Links}/{garden.Capacity.CoreLinks} core links, "
            + $"{garden.Nodes.Count(n => !n.Spec.IsFruit)} main, {garden.Nodes.Count(n => n.Spec.IsFruit)} fruit, "
            + $"labs at {Fills(garden, NodeKind.Lab)}, groves at {Fills(garden, NodeKind.Grove)}, genes {garden.Genes:0}, "
            + $"+{production:0.0}/s -{upkeep:0.0}/s";
    }

    // The average fill of a kind of part, as a percentage.
    static string Fills(Garden garden, NodeKind kind) =>
        garden.Nodes.Where(n => n.Spec.Kind == kind).Select(n => n.Fill).DefaultIfEmpty(0).Average().ToString("0%");

    sealed record Run(double[,] Fired, double[,] GenesAt, int[] WeekOne, int[] Month);

    // A month from planting for a few seeds, a smart player tending it (see SmartPlayer): when each unlock fired, the
    // genes at each target time, and the garden on some days.
    static Run Month(Action<string>? log, int days = 30)
    {
        var seeds = new[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var shownDays = new[] { 1, 3, 7, 10, 14, 21, 30 }.Where(d => d <= days).Append(days).ToHashSet();
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
            for (int day = 1; day <= days; day++)
            {
                // A minute at a time through the opening, when the player checks in every minute.
                for (double dt; t < day * Day; t += dt)
                {
                    dt = t < Hour ? 60 : step;
                    double hours = t / Hour;
                    if (SmartPlayer.CheckIn(t))
                    {
                        // A short run shows every two-hourly check-in, as the player found the garden.
                        if (days <= 2 && t >= Hour) lines[s].Add($"  {hours,4:0} h: {Describe(garden)}");
                        SmartPlayer.Tend(garden);
                    }
                    int before = garden.Unlocks;
                    garden.Grow(dt);
                    for (int u = before; u < garden.Unlocks; u++) run.Fired[s, u] = (t + dt) / Hour;
                    for (int h = 0; h < TargetHours.Length; h++)
                        if (Math.Abs(TargetHours[h] * Hour - (t + dt)) < 1) run.GenesAt[s, h] = garden.Genes;
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
    // every garden keeps up (see the end). A tuning aid run by hand (PACING=1): since power moved to the original's
    // clumps (economy steps of 0.3125 s) a month takes minutes, and the thresholds wait on a retune. PACING_DAYS=N
    // runs only the first N days (a day takes seconds), checking the unlocks due by then.
    [Fact]
    public void MonthPacing()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("PACING") == "1", "a tuning aid, run by hand (PACING=1)");
        Assert.Equal(Tuning.UnlockTrack.Length, TargetHours.Length);
        int days = int.TryParse(Environment.GetEnvironmentVariable("PACING_DAYS"), out int d) ? d : 30;
        var text = new System.Text.StringBuilder();
        void Log(string line)
        {
            TestContext.Current.TestOutputHelper?.WriteLine(line);
            text.AppendLine(line);
        }
        var run = Month(Log, days);
        var medians = new List<double>();
        int due = TargetHours.Count(h => h < days * 24);
        for (int u = 0; u < due; u++)
        {
            var hours = Enumerable.Range(0, run.Fired.GetLength(0)).Select(s => run.Fired[s, u]).Order().ToList();
            double median = Median(run.Fired, u);
            Log($"unlock {u + 1,2} at {Tuning.UnlockTrack[u].Genes,6:0} genes (target {TargetHours[u]:0.#} h, "
                + $"day {TargetHours[u] / 24 + 1:0.#}): median {median:0.#} h, range {hours[0]:0.#}-{hours[^1]:0.#} h; "
                + $"genes at target: median {Median(run.GenesAt, u):0}");
            medians.Add(median);
        }
        var late = Enumerable.Range(0, due)
            .Where(u => medians[u] < TargetHours[u] * 0.75 || medians[u] > TargetHours[u] * 1.25 + 0.2).ToList();
        Assert.True(late.Count == 0, $"unlocks {string.Join(", ", late.Select(u => u + 1))} off target\n{text}");
        if (days < 30)
        {
            Assert.Skip(text.ToString());
            return;
        }
        // Every garden has had week one's nine steps (DESIGN.md's 30 stems, 10 generators, 2 core links) by the end of
        // day 7, and is at most two steps short of the end of the track by the end of day 30 (at real-speed blob rates
        // gardens spread out: the slowest seed runs about a fifth behind the median).
        Assert.All(run.WeekOne, u => Assert.True(u >= 9, $"{u} unlocks by day 7"));
        Assert.All(run.Month, u => Assert.True(u >= Tuning.UnlockTrack.Length - 2, $"{u} unlocks by day 30"));
        // Shows the curve even when it passes (as the skip message), for tuning.
        Assert.Skip(text.ToString());
    }

    // For retuning the thresholds after a change to the economy: sets each one to the median genes at its target
    // time, runs again, and repeats until they settle, then reports them and the last run's gardens (as the skip
    // message, so it shows). Run it by hand: dotnet test -- --filter-method "*RetuneThresholds*" with RETUNE=1 set;
    // PACING_DAYS=N retunes only the unlocks due in the first N days, leaving the rest.
    [Fact]
    public void RetuneThresholds()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("RETUNE") == "1", "a tuning aid, run by hand (RETUNE=1)");
        int days = int.TryParse(Environment.GetEnvironmentVariable("PACING_DAYS"), out int d) ? d : 30;
        int due = TargetHours.Count(h => h < days * 24);
        var report = new System.Text.StringBuilder();
        var original = Tuning.UnlockTrack.ToArray();
        try
        {
            for (int pass = 0; pass < 6; pass++)
            {
                var run = Month(pass == 5 ? line => report.AppendLine(line) : null, days);
                double previous = 0;
                for (int u = 0; u < due; u++)
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
