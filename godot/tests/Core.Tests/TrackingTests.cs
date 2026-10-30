using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceGarden.Core.Tests;

// Step 4's tracking (Tracking.cs): the lifetime totals, the tiered history and its roll-up, the journal's moments, the
// away note, and all of it through a save.
public class TrackingTests
{
    const double Minute = History.Minute, Hour = History.Hour, Day = History.Day;

    static Garden NewGarden(Capacity? capacity = null)
    {
        var garden = Garden.New(Layout.Radial, seed: 7);
        if (capacity != null) garden.Capacity = capacity;
        return garden;
    }

    static PlantNode Add(Garden garden, PlantNode parent, NodeKind kind) =>
        (NodeSpecs.All[kind].IsFruit ? garden.GrowFruit(parent) : garden.GrowBranch(parent, kind))
            ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

    // The widget's own save options (SaveFile.Options).
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // core - stem - lab and a grove on another stem, all full: genes and passive growth both under way.
    static Garden Growing()
    {
        var garden = NewGarden(new Capacity(6, 1, 2));
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        Add(garden, stem, NodeKind.Lab);
        Add(garden, Add(garden, garden.Core, NodeKind.Stem), NodeKind.Grove);
        foreach (var n in garden.Nodes) n.Power = n.Store;
        return garden;
    }

    // ==== TOTALS ==== \\

    [Fact]
    public void TotalsCountWhatGrewWasPrunedAndHowOldTheGardenIs()
    {
        var garden = NewGarden(new Capacity(4, 1, 1));
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        var lab = Add(garden, stem, NodeKind.Lab);
        Add(garden, lab, NodeKind.Research);
        var t = garden.Tracking;
        Assert.Equal(3, t.PartsGrown);
        Assert.Equal(1, t.Grown[NodeKind.Research]);
        Assert.False(t.Grown.ContainsKey(NodeKind.Core)); // the core was planted, not grown

        garden.Grow(90);
        Assert.Equal(90, t.Age, 6);

        garden.Prune(lab);
        Assert.Equal(2, t.Pruned); // the lab and its research
        garden.UndoPrune();
        Assert.Equal(0, t.Pruned); // put back: never cut after all
        Assert.Equal(3, t.PartsGrown); // and not grown again
    }

    [Fact]
    public void PassiveBudsAreCountedAsGrowthBlobsStartThem()
    {
        var garden = Growing();
        garden.Grow(12 * Hour);
        Assert.True(garden.Tracking.PassiveBuds > 0);
        Assert.Single(garden.Tracking.Journal, e => e.Text == "First passive bud started");
        // Every passive bud started is a branch grown or still forming.
        int forming = garden.Nodes.Count(n => n.FormingBud);
        int branches = garden.Tracking.Grown.Where(g => !NodeSpecs.All[g.Key].IsFruit).Sum(g => g.Value) - 4; // the 4 placed
        Assert.Equal(garden.Tracking.PassiveBuds, branches + forming);
    }

    [Fact]
    public void ALoadedGardenIsNotCountedAsGrowingAgain()
    {
        var garden = Growing();
        var loaded = Garden.FromSave(garden.ToSave(), seed: 7);
        Assert.Equal(garden.Tracking.PartsGrown, loaded.Tracking.PartsGrown);
        Assert.Equal(garden.Tracking.Journal.Count, loaded.Tracking.Journal.Count);
    }

    // ==== HISTORY ==== \\

    [Fact]
    public void EachMinuteBecomesASampleOfTheFourMetrics()
    {
        var garden = Growing();
        float stored = garden.Nodes.Where(n => !n.Spec.IsFruit).Sum(n => n.Power);
        garden.Grow(59);
        Assert.Empty(garden.Tracking.History.Minutes); // not yet a whole minute
        garden.Grow(1);
        var sample = Assert.Single(garden.Tracking.History.Minutes);
        Assert.Equal((0, Minute), (sample.Start, sample.Seconds));
        Assert.InRange(sample.Power, stored * 0.8f, stored * 1.2f);
        Assert.True(sample.Gen > 0 && sample.Drain > 0);
        Assert.Equal(garden.Genes, sample.Genes, 6);
    }

    // A month and a half of minutes: the last day stays by the minute, the month before by the hour, the rest by the
    // day; nothing is lost on the way and the tiers join up end to end.
    [Fact]
    public void OldSamplesRollUpIntoHoursThenDays()
    {
        var history = new History();
        double age = 0, genes = 0;
        for (int i = 0; i < 45 * 24 * 60 + 30; i++)
        {
            history.AddGenes(1);
            genes++;
            age += Minute;
            history.Record(age, Minute, stored: i % 60, production: 5, upkeep: 3);
        }
        Assert.InRange(history.Minutes.Count, 24 * 60, 25 * 60);
        Assert.InRange(history.Minutes[0].Start, age - Day - Hour, age - Day);
        Assert.InRange(history.Hours.Count, 29 * 24, 30 * 24); // the month before the last day
        Assert.Equal(15, history.Days.Count);

        var all = history.All.ToList();
        for (int i = 1; i < all.Count; i++) Assert.Equal(all[i - 1].Start + all[i - 1].Seconds, all[i].Start, 6);
        Assert.Equal(age, all.Sum(s => s.Seconds), 6);
        Assert.Equal(genes, all.Sum(s => s.Genes), 6);
        Assert.All(history.Hours, h => Assert.Equal(Hour, h.Seconds, 6));
        Assert.All(history.Days, d => Assert.Equal(Day, d.Seconds, 6));
        Assert.All(history.Days, d => Assert.Equal((5f, 3f), (d.Gen, d.Drain)));
        Assert.Equal(29.5f, history.Days[0].Power, 3); // 0..59 averaged
    }

    // A catch-up runs the same steps, so it fills the history as if the garden had been watched.
    [Fact]
    public void ACatchUpFillsTheHistory()
    {
        var garden = Growing();
        garden.CatchUp(3 * Hour);
        Assert.Equal(180, garden.Tracking.History.Minutes.Count);
        Assert.Equal(3 * Hour, garden.Tracking.History.All.Sum(s => s.Seconds), 6);
        Assert.Equal(garden.Genes, garden.Tracking.History.All.Sum(s => s.Genes), 6);
    }

    // ==== SAVES ==== \\

    // Through the save's JSON (version 9) and back: the totals, every tier, the minute half gathered and the journal,
    // and the garden carries on as the original does.
    [Fact]
    public void TrackingSurvivesTheSave()
    {
        var garden = Growing();
        garden.Grow(Day + 2 * Hour + 30); // some hours rolled up, and half a minute gathering
        var json = JsonSerializer.Serialize(garden.ToSave(), Json);
        var loaded = Garden.FromSave(JsonSerializer.Deserialize<GardenSave>(json, Json)!, seed: 7);

        var (a, b) = (garden.Tracking, loaded.Tracking);
        Assert.Equal(a.Age, b.Age);
        Assert.Equal(a.Grown, b.Grown);
        Assert.Equal((a.Pruned, a.PassiveBuds, a.MostParts), (b.Pruned, b.PassiveBuds, b.MostParts));
        Assert.Equal(a.History.Minutes, b.History.Minutes);
        Assert.Equal(a.History.Hours, b.History.Hours);
        Assert.Equal(a.Journal, b.Journal);
        Assert.Equal(a.Firsts.Order(), b.Firsts.Order());
        Assert.NotEmpty(b.History.Hours);

        // The half minute carries on: the next sample is whole.
        loaded.Grow(30);
        Assert.Equal(Minute, b.History.Minutes[^1].Seconds, 6);
        Assert.Equal(a.History.Minutes.Count + 1, b.History.Minutes.Count);
    }

    // ==== JOURNAL ==== \\

    [Fact]
    public void TheJournalLogsAPlantingAndEachKindsFirst()
    {
        var garden = NewGarden(new Capacity(6, 1, 1));
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        Add(garden, stem, NodeKind.Grove);
        var second = Add(garden, stem, NodeKind.Stem);
        Add(garden, second, NodeKind.Leaf);
        var texts = garden.Tracking.Journal.Select(e => e.Text).ToList();
        Assert.Equal(["A new garden was planted", "First stem emerged", "First grove emerged", "First leaf grew"], texts);
        Assert.Equal(JournalKind.Planted, garden.Tracking.Journal[0].Kind);
    }

    [Fact]
    public void EachUnlockIsLoggedSayingWhatItOpened()
    {
        var garden = NewGarden();
        garden.Grow(Minute);
        garden.Deliver(15_000); // four steps of the track at once
        var unlocks = garden.Tracking.Journal.Where(e => e.Kind == JournalKind.Unlock).ToList();
        Assert.Equal(4, unlocks.Count);
        Assert.All(unlocks, u => Assert.Equal(Minute, u.Age, 6));
        Assert.Equal("Unlocked room for 3 more stems", unlocks[0].Text);
        Assert.Equal("Trellis unlocked, with room for 4 more stems and 1 more generator", unlocks[2].Text);
        Assert.Equal("Second core link and second Gravity Pot unlocked, with room for 1 more stem and 1 more generator",
            unlocks[3].Text);
    }

    [Fact]
    public void FirstsAndMilestonesAreLoggedOnce()
    {
        var garden = NewGarden(Capacity.Unlimited);
        garden.Place(ShapingKind.Pot, default);
        garden.Remove(garden.Shaping[0]);
        garden.Place(ShapingKind.Pot, default);
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        var lab = Add(garden, stem, NodeKind.Lab);
        garden.Prune(lab);
        garden.UndoPrune();
        garden.Prune(lab);
        while (garden.Nodes.Count < 60) garden.Sprout();
        var texts = garden.Tracking.Journal.Select(e => e.Text).ToList();
        Assert.Single(texts, t => t == "First Gravity Pot placed");
        Assert.DoesNotContain("First branch pruned", texts); // the player's routine doings aren't moments
        Assert.Single(texts, t => t == "The garden reached 25 parts");
        Assert.Single(texts, t => t == "The garden reached 50 parts");
        Assert.DoesNotContain("The garden reached 100 parts", texts);
    }

    // ==== AWAY NOTE ==== \\

    [Fact]
    public void TheAwayNoteSaysWhatGrewWhatReachedTheCoreAndWhatUnlocked()
    {
        var garden = Growing();
        garden.Genes = 190; // just short of the first unlock
        var mark = AwayNote.MarkNow(garden);
        garden.CatchUp(6 * Hour);
        var note = AwayNote.Since(garden, mark);
        Assert.Equal(6 * Hour, note.Seconds, 6);
        Assert.Contains(note.Lines, l => l.Contains("new parts grew"));
        Assert.Contains(note.Lines, l => l.EndsWith("genes reached the core."));
        Assert.Contains("Unlocked room for 3 more stems.", note.Lines);
        Assert.DoesNotContain(note.Lines, l => l.StartsWith("Power ran low"));
    }

    [Fact]
    public void TheAwayNoteTellsOfAPowerDip()
    {
        // Full, but badly overloaded: a chain of stems, each with a lab, costing far more than the core makes.
        var garden = NewGarden(Capacity.Unlimited);
        garden.PassiveGrowth = false;
        var at = garden.Core;
        while (garden.FullPowerBalance() is var (made, cost) && cost < made * 4)
        {
            at = Add(garden, at, NodeKind.Stem);
            Add(garden, at, NodeKind.Lab);
        }
        foreach (var n in garden.Nodes) n.Power = n.Store;
        var mark = AwayNote.MarkNow(garden);
        garden.CatchUp(Hour);
        var note = AwayNote.Since(garden, mark);
        Assert.Contains(note.Lines, l => l.StartsWith("Power ran low"));
    }

    [Fact]
    public void NothingChangedMeansNoNote()
    {
        var garden = NewGarden();
        foreach (var n in garden.Nodes) n.Power = n.Store;
        var mark = AwayNote.MarkNow(garden);
        garden.CatchUp(10 * Minute);
        Assert.False(AwayNote.Since(garden, mark).Any);
    }

    // ==== EXPORT ==== \\

    [Fact]
    public void TheExportIsOneRowPerSampleAndPerTotal()
    {
        var garden = Growing();
        garden.Grow(Day + Hour);
        var rows = garden.Tracking.HistoryCsv().TrimEnd().Split('\n');
        Assert.Equal("tier,start_hours,seconds,power,gen_per_s,drain_per_s,genes", rows[0]);
        Assert.Equal(garden.Tracking.History.All.Count(), rows.Length - 1);
        Assert.StartsWith("hour,0,3600,", rows[1]);
        var totals = garden.Tracking.TotalsCsv(garden);
        Assert.Contains("age_hours,25\n", totals);
        Assert.Contains($"parts_grown,{garden.Tracking.PartsGrown}\n", totals);
        Assert.StartsWith("age_hours,day,time,kind,text\n0,1,00:00,Planted,\"A new garden was planted\"", garden.Tracking.JournalCsv());
    }

    // The Almanac's calendar: Day 1 is the first 24 hours of growth, and the time is the time on that day.
    [Fact]
    public void AgesFallOnDaysAndTimes()
    {
        Assert.Equal((1, "00:00"), (Tracking.DayOf(0), Tracking.ClockOf(0)));
        Assert.Equal((1, "23:59"), (Tracking.DayOf(Day - 1), Tracking.ClockOf(Day - 1)));
        Assert.Equal((2, "00:00"), (Tracking.DayOf(Day), Tracking.ClockOf(Day)));
        Assert.Equal((3, "06:05"), (Tracking.DayOf(2 * Day + 6 * Hour + 5 * Minute + 30), Tracking.ClockOf(2 * Day + 6 * Hour + 5 * Minute + 30)));
    }
}
