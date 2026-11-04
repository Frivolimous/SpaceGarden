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
        // The Hub's progress counted in points earned (Jeremy, Nov 4: 46 of its 92 by day 14), not levels (a wildflower's
        // path waits for its kind to be found).
        ("Hub grown", g => g.HubPlanted),
        ("first Hub point", g => g.HubPointsEarned >= 1),
        ("10 Hub points", g => g.HubPointsEarned >= 10),
        ("23 Hub points (25%)", g => g.HubPointsEarned >= 23),
        ("46 Hub points (50%)", g => g.HubPointsEarned >= 46),
        ("69 Hub points (75%)", g => g.HubPointsEarned >= 69),
        ("92 Hub points (all)", g => g.HubPointsEarned >= Hubs.TotalPoints),
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
            + string.Join(", ", garden.Nodes.Where(n => n.Spec.IsWild).GroupBy(n => n.Spec.Name).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}"))
            + (garden.HubPlanted || garden.HubPointsEarned > 0
                ? $"; Hub: {garden.HubGenes:0} genes caught, {garden.HubPointsEarned} points earned ({garden.HubPoints} unspent), "
                    + $"{garden.HubLevelsBought} levels: " + string.Join(", ", garden.HubLevels.Where(l => l.Value > 0)
                        .OrderByDescending(l => l.Value).ThenBy(l => l.Key.ToString()).Select(l => $"{NodeSpecs.All[l.Key].Name} {l.Value}"))
                : "");
    }

    // The average fill of a kind of part, as a percentage.
    static string Fills(Garden garden, NodeKind kind) =>
        garden.Nodes.Where(n => n.Spec.Kind == kind).Select(n => n.Fill).DefaultIfEmpty(0).Average().ToString("0%");

    // When each milestone was reached (hours from planting; infinity if not), the genes earned by each day's end, the
    // longest wait between upgrades each day (awake hours) and the upgrades bought at each day's first check-in, after
    // the night.
    sealed record Run(double[,] Reached, double[,] GenesByDay, double[,] LongestWait, double[,] AfterSleep,
        double[,] HubGenesByDay, double[,] HubLevelsByDay)
    {
        // At each day's end (cumulative): stems and generators bought, Expansions bought, Hub points unspent; and the
        // Hub's drain and the garden's power gen, at full power.
        public required double[,] StemsBought, GeneratorsBought, Expansions, HubUnspent, HubDrain, PowerGen;
        // Hours in each day the garden was overloaded (it would cost more full than it makes full), or had fewer stems
        // or generators (grown or forming) than its caps allow.
        public required double[,] Overloaded, UnderStems, UnderGenerators;
    }

    // A stretch of time something held, for a garden's own line: how many stretches, the hours in all, the longest, the
    // first's start.
    sealed class Stretches
    {
        double? since;
        public int Count;
        public double Total, Longest, First = double.PositiveInfinity;
        public void At(bool on, double hours)
        {
            if (on && since == null) { since = hours; Count++; First = Math.Min(First, hours); }
            if (!on && since is { } start) End(hours - start);
        }
        public void Close(double hours) { if (since is { } start) End(hours - start); }
        void End(double length) { since = null; Total += length; Longest = Math.Max(Longest, length); }
        public override string ToString() => Count == 0 ? "never"
            : $"{Count} times, {Total:0.#} h in all, longest {Longest:0.#} h, first at {Hours(First)}";
    }

    // Stems or generators, grown or forming (a wild stand-in too), against the cap.
    static int Grown(Garden garden, NodeKind kind) =>
        garden.Nodes.Count(n => Garden.IsBudOf(n.Spec.Kind, kind))
        + garden.Nodes.Count(n => n.FormingBud && Garden.IsBudOf(n.Forming!.Value, kind));

    // Awake hours from planting to this many hours in (the player sleeps the last 8 of each day: see SmartPlayer.Awake).
    static double AwakeHours(double hours) => Math.Floor(hours / 24) * 16 + Math.Min(hours % 24, 16);

    // The longest awake wait each day: between two upgrades bought, the later on that day, or from the last upgrade to
    // the day's end if that's longer (a day with none bought waits all day).
    static double[] LongestWaits(List<double> boughtAt, int days)
    {
        var waits = new double[days];
        for (int d = 1; d <= days; d++)
        {
            double start = (d - 1) * 24, end = d * 24, longest = 0;
            for (int i = 1; i < boughtAt.Count; i++)
                if (boughtAt[i] >= start && boughtAt[i] < end)
                    longest = Math.Max(longest, AwakeHours(boughtAt[i]) - AwakeHours(boughtAt[i - 1]));
            double last = boughtAt.Where(b => b < end).DefaultIfEmpty(0).Max();
            waits[d - 1] = Math.Max(longest, AwakeHours(end) - AwakeHours(last));
        }
        return waits;
    }

    // Some days from planting for a few seeds, a smart player tending and buying (see SmartPlayer): when each milestone
    // was reached, and the garden on some days.
    static Run Days(Action<string>? log, int days)
    {
        var seeds = new[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var shownDays = new[] { 1, 3, 7, 10, 14, 21, 30 }.Where(d => d <= days).Append(days).ToHashSet();
        double[,] PerDay() => new double[seeds.Length, days];
        var run = new Run(new double[seeds.Length, Milestones.Length], PerDay(), PerDay(), PerDay(), PerDay(), PerDay())
        {
            StemsBought = PerDay(), GeneratorsBought = PerDay(), Expansions = PerDay(), HubUnspent = PerDay(),
            HubDrain = PerDay(), PowerGen = PerDay(), Overloaded = PerDay(), UnderStems = PerDay(), UnderGenerators = PerDay(),
        };
        for (int s = 0; s < seeds.Length; s++)
            for (int m = 0; m < Milestones.Length; m++) run.Reached[s, m] = double.PositiveInfinity;
        const double step = 600;
        // The seeds run side by side (each garden is its own), their lines kept in seed order.
        var lines = seeds.Select(_ => new List<string>()).ToArray();
        var chosen = new List<string>();
        var kept = new List<string>();
        var hubLevels = new List<string>();
        Parallel.For(0, seeds.Length, s =>
        {
            var garden = Planted(seeds[s]);
            var boughtAt = new List<double>();
            var overload = new Stretches();
            var underStems = new Stretches();
            var underGenerators = new Stretches();
            var producedAt = new Dictionary<int, (double Core, double Hub)>();
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
                        int before = garden.Bought;
                        SmartPlayer.Tend(garden);
                        if (garden.Bought > before) boughtAt.Add(t / Hour);
                        if (day > 1 && Math.Abs(t - (day - 1) * Day) < 1) run.AfterSleep[s, day - 1] = garden.Bought - before;
                        Check(t / Hour); // bought at the check-in
                    }
                    garden.Grow(dt);
                    Check((t + dt) / Hour); // genes earned as they arrive
                    // How the garden stands after this stretch, as if for all of it.
                    var (made, used) = garden.FullPowerBalance();
                    bool over = made < used;
                    bool shortStems = Grown(garden, NodeKind.Stem) < garden.StemCap;
                    bool shortGenerators = Grown(garden, NodeKind.Generator) < garden.Capacity.Generators;
                    overload.At(over, (t + dt) / Hour);
                    underStems.At(shortStems, (t + dt) / Hour);
                    underGenerators.At(shortGenerators, (t + dt) / Hour);
                    if (over) run.Overloaded[s, day - 1] += dt / Hour;
                    if (shortStems) run.UnderStems[s, day - 1] += dt / Hour;
                    if (shortGenerators) run.UnderGenerators[s, day - 1] += dt / Hour;
                }
                run.GenesByDay[s, day - 1] = garden.Genes;
                run.HubGenesByDay[s, day - 1] = garden.HubGenes;
                run.HubLevelsByDay[s, day - 1] = garden.HubPointsEarned; // (points, Nov 4: progress is counted in them)
                run.StemsBought[s, day - 1] = garden.StemsBought;
                run.GeneratorsBought[s, day - 1] = garden.GeneratorsBought;
                run.Expansions[s, day - 1] = garden.Bought;
                run.HubUnspent[s, day - 1] = garden.HubPoints;
                run.HubDrain[s, day - 1] = garden.Hub is { } hub ? Garden.FullUpkeepPerSecond(hub) : 0;
                run.PowerGen[s, day - 1] = garden.FullPowerBalance().Production;
                producedAt[day] = (garden.Genes, garden.HubGenes);
                if (shownDays.Contains(day)) lines[s].Add($"  day {day,2}: {Describe(garden)}");
            }
            var waits = LongestWaits(boughtAt, days);
            for (int d = 0; d < days; d++) run.LongestWait[s, d] = waits[d];
            // The evolutions the player chose (at random, or by its ranking: see SmartPlayer.Style), when, and when each bloomed.
            lines[s].Add("  evolutions: " + (garden.Evolved.Count == 0 ? "none" : string.Join("; ", garden.Evolved.Select(e =>
                $"{e.Name} chosen {Hours(e.ChosenAt / Hour)}" + (e.Bloomed ? $", bloomed {Hours(e.BloomedAt / Hour)}" : ", growing")))));
            lock (chosen) chosen.AddRange(garden.Evolved.Select(e => e.Name));
            // The Hub: when it came, the rebuild, the labs moved to keep the split, and the levels bought, in order.
            var care = SmartPlayer.CareOf(garden);
            lines[s].Add("  Hub: " + (care.Rebuilt
                ? $"rebuilt at {Hours(care.RebuiltAt)} ({care.RebuildPruned} parts pruned), {care.LabsMoved} labs moved since, "
                    + $"{garden.HubPointsEarned} points earned ({garden.HubPoints} unspent), "
                    + $"{Projected(garden, run.HubGenesByDay, s, days)}"
                : "not out"));
            lines[s].Add("  Hub levels: " + (care.Bought.Count == 0 ? "none" : string.Join(", ", care.Bought.Select(b =>
                $"{NodeSpecs.All[b.Kind].Name} {b.Level}" + (Hubs.Path(b.Kind).MilestoneAt(b.Level) != null ? $" ({Hours(b.Hours)})" : "")))));
            lock (hubLevels) hubLevels.AddRange(care.Bought.Select(b => NodeSpecs.All[b.Kind].Name));
            // Genes made (to the core, and to the Hub) by the ends of days 3, 7 and 14.
            lines[s].Add("  genes made: " + string.Join("; ", new[] { 3, 7, 14 }.Where(producedAt.ContainsKey).Select(d =>
                $"day {d} {producedAt[d].Core + producedAt[d].Hub:#,0} ({producedAt[d].Core:#,0} core, {producedAt[d].Hub:#,0} Hub)")));
            // The wildflowers kept at the end.
            var wild = garden.Nodes.Where(n => n.Spec.IsWild).GroupBy(n => n.Spec.Name).OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}").ToList();
            lines[s].Add("  wildflowers kept: " + (wild.Count == 0 ? "none" : string.Join(", ", wild)));
            lock (kept) kept.AddRange(garden.Nodes.Where(n => n.Spec.IsWild).Select(n => n.Spec.Name));
            // Overloaded, and short of the caps.
            foreach (var stretch in new[] { overload, underStems, underGenerators }) stretch.Close(days * 24);
            lines[s].Add($"  overloaded: {overload}");
            lines[s].Add($"  fewer stems than the cap: {underStems}");
            lines[s].Add($"  fewer generators than the cap: {underGenerators}");
        });
        foreach (var line in lines.SelectMany(l => l)) log?.Invoke(line);
        string Tally(List<string> names) => names.Count == 0 ? "none" : string.Join(", ", names.GroupBy(n => n)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}"));
        log?.Invoke("evolutions chosen, all seeds: " + Tally(chosen));
        log?.Invoke("Hub levels bought, all seeds: " + Tally(hubLevels));
        log?.Invoke("wildflowers kept at the end, all seeds: " + Tally(kept));
        return run;
    }

    // When the Hub would have every level, at its last day's rate of genes caught: the points still to earn, each dearer.
    static string Projected(Garden garden, double[,] hubGenesByDay, int s, int days)
    {
        int spent = Hubs.Paths.Sum(p => Enumerable.Range(1, garden.HubLevel(p.Kind)).Sum(p.Cost));
        int toEarn = Hubs.TotalPoints - spent - garden.HubPoints;
        if (toEarn <= 0) return "every level affordable";
        double genes = -garden.HubPool;
        for (int i = 0; i < toEarn; i++) genes += Tuning.HubPointCost(garden.HubPointsEarned + i);
        double perDay = days > 1 ? hubGenesByDay[s, days - 1] - hubGenesByDay[s, days - 2] : hubGenesByDay[s, 0];
        return perDay <= 0 ? "never complete at its rate" : $"complete in about {genes / perDay:0} more days at its last day's rate";
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
        // Trying other Hub point costs (see Tuning.HubPointCost): HUB_FIRST, HUB_STEP, HUB_GROWTH.
        if (double.TryParse(Environment.GetEnvironmentVariable("HUB_FIRST"), out double first)) Tuning.HubFirstPoint = first;
        if (double.TryParse(Environment.GetEnvironmentVariable("HUB_STEP"), out double step)) Tuning.HubPointStep = step;
        if (double.TryParse(Environment.GetEnvironmentVariable("HUB_GROWTH"), out double growth)) Tuning.HubPointGrowth = growth;
        if (double.TryParse(Environment.GetEnvironmentVariable("HUB_POWER"), out double power)) Tuning.HubPointPower = power;
        // The player (PLAYER: bad, medium or best; see SmartPlayer.Style).
        SmartPlayer.Style = Enum.TryParse<SmartPlayer.PlayStyle>(Environment.GetEnvironmentVariable("PLAYER"), true, out var style)
            ? style : SmartPlayer.PlayStyle.Medium;
        var text = new System.Text.StringBuilder();
        void Log(string line)
        {
            TestContext.Current.TestOutputHelper?.WriteLine(line);
            text.AppendLine(line);
        }
        Log($"Player: {SmartPlayer.Style}");
        Log($"Hub points: first {Tuning.HubFirstPoint:#,0} genes, each {Tuning.HubPointGrowth}x the last, times its number to the {Tuning.HubPointPower}, and {Tuning.HubPointStep:#,0} more "
            + $"(point 10 {Tuning.HubPointCost(9):#,0}, 46 {Tuning.HubPointCost(45):#,0}, 92 {Tuning.HubPointCost(91):#,0}; all 92 "
            + $"{Enumerable.Range(0, Hubs.TotalPoints).Sum(Tuning.HubPointCost):#,0})");
        var run = Days(Log, days);
        for (int m = 0; m < Milestones.Length; m++)
        {
            var hours = Enumerable.Range(0, run.Reached.GetLength(0)).Select(s => run.Reached[s, m]).Order().ToList();
            Log($"{Milestones[m].Name,-24} median {Hours(Median(run.Reached, m)),-10} range {Hours(hours[0])} to {Hours(hours[^1])}");
        }
        // The Hub's mean (Jeremy, Nov 1: after day 5, so at least 120 h), over the gardens that reached it.
        int hub = Array.FindIndex(Milestones, m => m.Name == "Hub");
        var hubs = Enumerable.Range(0, run.Reached.GetLength(0)).Select(s => run.Reached[s, hub]).ToList();
        var reached = hubs.Where(h => !double.IsPositiveInfinity(h)).ToList();
        Log($"Hub mean               {(reached.Count > 0 ? Hours(reached.Average()) : "not yet")} over {reached.Count} of {hubs.Count} gardens "
            + $"(target: after day 5, {(reached.Count > 0 && reached.Average() > 120 ? "met" : "not met")})");
        for (int day = 1; day <= days; day++)
        {
            var waits = Enumerable.Range(0, run.LongestWait.GetLength(0)).Select(s => run.LongestWait[s, day - 1]).ToList();
            Log($"day {day,2}: genes earned by its end median {Median(run.GenesByDay, day - 1):#,0}; longest awake wait between "
                + $"upgrades median {Median(run.LongestWait, day - 1):0.#} h, worst {waits.Max():0.#} h"
                + (day > 1 ? $"; bought after the night median {Median(run.AfterSleep, day - 1):0}, range "
                    + $"{Enumerable.Range(0, run.AfterSleep.GetLength(0)).Min(s => run.AfterSleep[s, day - 1]):0}-"
                    + $"{Enumerable.Range(0, run.AfterSleep.GetLength(0)).Max(s => run.AfterSleep[s, day - 1]):0}" : "")
                + (Median(run.HubGenesByDay, day - 1) > 0 || Median(run.HubLevelsByDay, day - 1) > 0
                    ? $"; the Hub's genes caught median {Median(run.HubGenesByDay, day - 1):#,0}, points earned median {Median(run.HubLevelsByDay, day - 1):0}"
                    : ""));
            // The day's own: stems and generators bought (all gardens together), Expansions, the Hub's share of the
            // genes, its unspent points, its drain against the power gen, and the hours overloaded and short of caps.
            double OnDay(double[,] v, int seed) => v[seed, day - 1] - (day > 1 ? v[seed, day - 2] : 0);
            var seedsAll = Enumerable.Range(0, run.Reached.GetLength(0)).ToList();
            double stems = seedsAll.Sum(x => OnDay(run.StemsBought, x)), generators = seedsAll.Sum(x => OnDay(run.GeneratorsBought, x));
            var shares = seedsAll.Select(x => (Hub: OnDay(run.HubGenesByDay, x), Core: OnDay(run.GenesByDay, x)))
                .Where(x => x.Hub > 0).Select(x => x.Hub / (x.Hub + x.Core)).Order().ToList();
            double MedianOf(Func<int, double> f) => seedsAll.Select(f).Order().ElementAt(seedsAll.Count / 2);
            Log($"        stems:generators bought {stems:0}:{generators:0}"
                + (generators > 0 ? $" ({stems / generators:0.#}:1)" : "")
                + $"; Expansions bought median {MedianOf(x => OnDay(run.Expansions, x)):0}"
                + (shares.Count > 0 ? $"; Hub's share of genes median {shares[shares.Count / 2]:0%}" : "")
                + (MedianOf(x => run.HubDrain[x, day - 1]) > 0
                    ? $"; Hub points unspent median {MedianOf(x => run.HubUnspent[x, day - 1]):0}, Hub drain median "
                        + $"{MedianOf(x => run.HubDrain[x, day - 1]):0.#}/s of {MedianOf(x => run.PowerGen[x, day - 1]):0}/s gen" : "")
                + $"; hours overloaded median {MedianOf(x => run.Overloaded[x, day - 1]):0.#} (worst {seedsAll.Max(x => run.Overloaded[x, day - 1]):0.#}), "
                + $"short of stems {MedianOf(x => run.UnderStems[x, day - 1]):0.#}, of generators {MedianOf(x => run.UnderGenerators[x, day - 1]):0.#}");
        }
        for (int s = 0; s < run.Reached.GetLength(0); s++)
            Assert.True(run.Reached[s, 0] <= 1, $"seed {s + 1} bought its first stem at {run.Reached[s, 0]:0.#} h\n{text}");
        Assert.Skip(text.ToString());
    }
}
