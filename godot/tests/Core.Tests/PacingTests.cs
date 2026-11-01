namespace SpaceGarden.Core.Tests;

// Genes from labs to the core, and the pacing of the upgrade tracks they buy, in gardens a smart player tends.
public class PacingTests
{
    const double Hour = 3600, Day = 24 * Hour;
    static readonly double BlobHours = 1 / NodeSpecs.All[NodeKind.Lab].BlobsPerHour;

    static Garden NewGarden(Capacity? capacity = null, int seed = 7)
    {
        var garden = Garden.New(Layout.Radial, seed);
        if (capacity != null) garden.Capacity = capacity;
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
        garden.PassiveGrowth = false;
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
        var garden = NewGarden(new Capacity(22, 0, 1));
        garden.PassiveGrowth = false;
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


    [Fact]
    public void OnlyLabsMakeGenes()
    {
        // A core, stems and a grove make none: the first upgrade waits for the first lab.
        var garden = NewGarden();
        garden.PassiveGrowth = false;
        Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Grove);
        garden.Grow(Day);
        Assert.DoesNotContain(garden.Nodes, n => n.Spec.Kind == NodeKind.Lab);
        Assert.Equal(0, garden.Genes);
        Assert.Equal(0, garden.GeneBalance);
    }

    // ==== TENDED GARDENS ==== \\

    // A garden a smart player has tended for a while (see SmartPlayer): it spends its genes as it checks in.
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
    public void ATendedGardenBuysAndKeepsWhatItBought()
    {
        var garden = Tended(7, 2 * 24 + 5);
        Assert.True(garden.StemsBought >= 5);
        Assert.True(garden.Capacity.Stems > Tuning.StartStemCap);
        Assert.True(garden.GeneBalance < garden.Genes); // spent
        var loaded = Garden.FromSave(garden.ToSave(), seed: 7);
        Assert.Equal(garden.Genes, loaded.Genes);
        Assert.Equal(garden.GeneBalance, loaded.GeneBalance);
        Assert.Equal((garden.StemsBought, garden.GeneratorsBought, garden.CoreBought),
            (loaded.StemsBought, loaded.GeneratorsBought, loaded.CoreBought));
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
    public void CatchUpGrowsAsLivingThroughItDoes()
    {
        var caught = Tended(7, 10);
        var live = Tended(7, 10);
        caught.CatchUp(Tuning.CatchUpCapSeconds);
        for (int i = 0; i < (int)(Tuning.CatchUpCapSeconds / 7); i++) live.Grow(7);
        live.Grow(Tuning.CatchUpCapSeconds % 7);
        Assert.Equal(live.Capacity, caught.Capacity);
        Assert.Equal(live.Genes, caught.Genes, 1e-3);
        Assert.Equal(live.GeneBalance, caught.GeneBalance, 1e-3);
        Assert.Equal(live.Tracking.MostMainNodes, caught.Tracking.MostMainNodes);
        Assert.Equal(live.Nodes.Select(n => (n.Spec.Kind, n.Parent?.Id)), caught.Nodes.Select(n => (n.Spec.Kind, n.Parent?.Id)));
    }

    [Fact]
    public void ABoughtCapGivesGrowthRoom()
    {
        // The player plants a grove and a lab, and after a day prunes the passive flowers that took every free link:
        // stems bought then have room to bud there, and passive growth keeps within the new caps (which kind fills each
        // freed link is up to its roll).
        var garden = Planted();
        var stem = garden.Nodes[1];
        var grove = Add(garden, stem, NodeKind.Grove);
        var lab = Add(garden, Add(garden, stem, NodeKind.Stem), NodeKind.Lab); // leaving the second stem a free link
        garden.Grow(Day);
        while (garden.Nodes.FirstOrDefault(n => n != grove && n != lab
            && n.Spec.Kind is NodeKind.Grove or NodeKind.Lab or NodeKind.Generator) is { } passive)
        {
            garden.Prune(passive);
            garden.ForgetPrune();
        }
        Assert.Equal(garden.Capacity.Stems, Count(garden, NodeKind.Stem)); // the start's stems all grown
        bool StemRoom() => garden.Nodes.Any(n => n.Parent != null && garden.CanSproutFrom(n, NodeKind.Stem));
        Assert.False(StemRoom());
        garden.Deliver(450); // three stems: 100, 150 and 200
        for (int i = 0; i < 3; i++) Assert.True(garden.Buy(Track.Stems));
        Assert.True(StemRoom());
        garden.Grow(Day);
        Assert.True(Count(garden, NodeKind.Stem) <= garden.Capacity.Stems);
        Assert.True(Count(garden, NodeKind.Generator) <= garden.Capacity.Generators);
        Assert.True(garden.Core.Links <= garden.Capacity.CoreLinks);
    }

    // ==== PACING ==== \\

    // The moments the pacing run times (DESIGN.md, Upgrade tracks): the first of each track, the core link, the Hub,
    // the last stem and generator, and the genes for all of that (stems 213,650 + generators 111,000 + the core to the
    // Hub 130,500 = 455,150) earned.
    // Where in the core track the nth upgrade of a kind is (n from 1).
    static int CoreAt(UpgradeKind kind, int n = 1) =>
        Tuning.CoreCosts.Select((c, i) => (c.Kind, i)).Where(c => c.Kind == kind).ElementAt(n - 1).i;

    static readonly (string Name, Func<Garden, bool> Reached)[] Milestones =
    [
        ("first stem", g => g.StemsBought >= 1),
        ("first generator", g => g.GeneratorsBought >= 1),
        ("first wild flower slot", g => g.CoreBought >= 1),
        ("core link", g => g.CoreBought > CoreAt(UpgradeKind.CoreLink)),
        ("10 stems", g => g.StemsBought >= 6),
        ("5 generators", g => g.GeneratorsBought >= 4),
        ("20 stems", g => g.StemsBought >= 16),
        ("Hub", g => g.CoreBought > CoreAt(UpgradeKind.Hub)),
        ("10 generators", g => g.GeneratorsBought >= 9),
        ("30 stems", g => g.StemsBought >= 26),
        ("15 generators", g => g.GeneratorsBought >= 14),
        ("40 stems", g => g.StemsBought >= 36),
        ("third seedling slot", g => g.CoreBought > CoreAt(UpgradeKind.Seedling, 2)),
        ("455,150 genes earned", g => g.Genes >= 455_150),
    ];

    static string Describe(Garden garden)
    {
        var (production, upkeep) = garden.PowerBalance();
        return $"{Count(garden, NodeKind.Stem)}/{garden.Capacity.Stems} stems, "
            + $"{Count(garden, NodeKind.Generator)}/{garden.Capacity.Generators} generators, "
            + $"{Count(garden, NodeKind.Lab)} labs, {Count(garden, NodeKind.Grove)} groves, {garden.Core.Links}/{garden.Capacity.CoreLinks} core links, "
            + $"core track {garden.CoreBought}, size {garden.Tracking.MostMainNodes}, "
            + $"{garden.Nodes.Count(n => !n.Spec.IsFruit)} main, {garden.Nodes.Count(n => n.Spec.IsFruit)} fruit, "
            + $"labs at {Fills(garden, NodeKind.Lab)}, groves at {Fills(garden, NodeKind.Grove)}, "
            + $"genes {garden.Genes:0} (balance {garden.GeneBalance:0}), +{production:0.0}/s -{upkeep:0.0}/s"
            + $", wild {garden.WildCount}/{garden.Capacity.WildFlowers}: "
            + string.Join(", ", garden.Nodes.Where(n => n.Spec.IsWild).GroupBy(n => n.Spec.Name).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}"));
    }

    // The average fill of a kind of part, as a percentage.
    static string Fills(Garden garden, NodeKind kind) =>
        garden.Nodes.Where(n => n.Spec.Kind == kind).Select(n => n.Fill).DefaultIfEmpty(0).Average().ToString("0%");

    // When each milestone was reached (hours from planting; infinity if not), and the genes earned by each day's end.
    sealed record Run(double[,] Reached, double[,] GenesByDay);

    // Some days from planting for a few seeds, a smart player tending and buying (see SmartPlayer): when each milestone
    // was reached, and the garden on some days.
    static Run Days(Action<string>? log, int days)
    {
        var seeds = new[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var shownDays = new[] { 1, 3, 7, 10, 14, 21, 30 }.Where(d => d <= days).Append(days).ToHashSet();
        var run = new Run(new double[seeds.Length, Milestones.Length], new double[seeds.Length, days]);
        for (int s = 0; s < seeds.Length; s++)
            for (int m = 0; m < Milestones.Length; m++) run.Reached[s, m] = double.PositiveInfinity;
        const double step = 600;
        // The seeds run side by side (each garden is its own), their lines kept in seed order.
        var lines = seeds.Select(_ => new List<string>()).ToArray();
        var chosen = new List<string>();
        Parallel.For(0, seeds.Length, s =>
        {
            var garden = Planted(seeds[s]);
            double t = 0;
            lines[s].Add($"seed {seeds[s]}:");
            void Check(double hours)
            {
                for (int m = 0; m < Milestones.Length; m++)
                    if (double.IsPositiveInfinity(run.Reached[s, m]) && Milestones[m].Reached(garden)) run.Reached[s, m] = hours;
            }
            for (int day = 1; day <= days; day++)
            {
                // A minute at a time through the opening, when the player checks in every minute.
                for (double dt; t < day * Day; t += dt)
                {
                    dt = t < Hour ? 60 : step;
                    if (SmartPlayer.CheckIn(t))
                    {
                        SmartPlayer.Tend(garden);
                        Check(t / Hour); // bought at the check-in
                    }
                    garden.Grow(dt);
                    Check((t + dt) / Hour); // genes earned as they arrive
                }
                run.GenesByDay[s, day - 1] = garden.Genes;
                if (shownDays.Contains(day)) lines[s].Add($"  day {day,2}: {Describe(garden)}");
            }
            // The evolutions the player chose (at random), when, and when each bloomed.
            lines[s].Add("  evolutions: " + (garden.Evolved.Count == 0 ? "none" : string.Join("; ", garden.Evolved.Select(e =>
                $"{e.Name} chosen {Hours(e.ChosenAt / Hour)}" + (e.Bloomed ? $", bloomed {Hours(e.BloomedAt / Hour)}" : ", growing")))));
            lock (chosen) chosen.AddRange(garden.Evolved.Select(e => e.Name));
        });
        foreach (var line in lines.SelectMany(l => l)) log?.Invoke(line);
        log?.Invoke("evolutions chosen, all seeds: " + string.Join(", ", chosen.GroupBy(n => n).OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}")));
        return run;
    }

    static double Median(double[,] values, int column)
    {
        var sorted = Enumerable.Range(0, values.GetLength(0)).Select(s => values[s, column]).Order().ToList();
        return sorted[sorted.Count / 2];
    }

    static string Hours(double hours) => double.IsPositiveInfinity(hours) ? "not yet"
        : hours < 48 ? $"{hours:0.#} h" : $"day {hours / 24 + 1:0.#}";

    // Prints when each milestone lands (the median of eight seeds, and their range) and the genes earned by each day's
    // end, for tuning the tracks' costs. A tuning aid run by hand (PACING=1): a day takes a few seconds; PACING_DAYS=N
    // runs N days (14 by default). Every garden must buy its first stem within the opening hour; the rest is reported
    // (as the skip message, so it shows), not checked: the costs wait on the real-week check.
    [Fact]
    public void TrackPacing()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("PACING") == "1", "a tuning aid, run by hand (PACING=1)");
        int days = int.TryParse(Environment.GetEnvironmentVariable("PACING_DAYS"), out int d) ? d : 14;
        var text = new System.Text.StringBuilder();
        void Log(string line)
        {
            TestContext.Current.TestOutputHelper?.WriteLine(line);
            text.AppendLine(line);
        }
        var run = Days(Log, days);
        for (int m = 0; m < Milestones.Length; m++)
        {
            var hours = Enumerable.Range(0, run.Reached.GetLength(0)).Select(s => run.Reached[s, m]).Order().ToList();
            Log($"{Milestones[m].Name,-22} median {Hours(Median(run.Reached, m)),-10} range {Hours(hours[0])} to {Hours(hours[^1])}");
        }
        for (int day = 1; day <= days; day++)
            Log($"genes earned by the end of day {day,2}: median {Median(run.GenesByDay, day - 1):#,0}");
        for (int s = 0; s < run.Reached.GetLength(0); s++)
            Assert.True(run.Reached[s, 0] <= 1, $"seed {s + 1} bought its first stem at {run.Reached[s, 0]:0.#} h\n{text}");
        Assert.Skip(text.ToString());
    }
}
