using System.Globalization;
using System.Text;

namespace SpaceGarden.Core;

// What the garden remembers of its own life (DESIGN.md, Step 4: information and tracking): a few lifetime totals,
// four time series (History), and the Almanac's journal of moments. Garden keeps it up to date as it grows, a catch-up
// too; it changes nothing in the garden. Times are the garden's age: growth seconds it has lived, so they follow the
// growth speed and a catch-up's cap, as everything else in the rules does.
public sealed class Tracking
{
    // ==== TOTALS ==== \\

    // Growth seconds the garden has lived.
    public double Age;
    // Parts grown over its life, by kind (the first stem, passive growth, the player's buds and fruit; not the core).
    public readonly Dictionary<NodeKind, int> Grown = new();
    // Parts cut off by pruning (an undone prune doesn't count), and passive buds started by growth blobs.
    public int Pruned, PassiveBuds;
    // The most parts the garden has had at once (for the Almanac's milestones).
    public int MostParts;
    // The garden's size: the most main nodes (parts that aren't fruit, the core among them) it has had at once.
    // Pruning never lowers it. The shaping tools come by it (DESIGN.md, Upgrade tracks; see Garden.GainTools).
    public int MostMainNodes;

    public int PartsGrown => Grown.Values.Sum();

    public readonly History History = new();
    public readonly List<JournalEntry> Journal = new();
    // The firsts already logged, so each is logged once ("grew:Grove", "passive", "placed:Pot"). The journal favours the
    // garden's own moments; of the player's doings only the first of each shaping tool is one.
    public readonly HashSet<string> Firsts = new();

    // Logs a moment now.
    public void Log(JournalKind kind, string text) => Journal.Add(new JournalEntry(Age, kind, text));

    // Logs a first, once.
    public void First(string key, string text)
    {
        if (Firsts.Add(key)) Log(JournalKind.First, text);
    }

    // The garden reached these many parts at once (a part count milestone in the Almanac, the first time).
    public static readonly int[] PartMilestones = [25, 50, 100, 200, 300, 400, 500];

    // The kinds the garden has grown, each with the age it first grew at (the core: its planting). The Almanac's
    // Codex lists only these (DESIGN.md, Step 5: a kind is discovered the first time it grows).
    public readonly Dictionary<NodeKind, double> Discovered = new();
    // Wild flowers whose own life cycle ended them, by kind (an Ember burned out, a Comet Bloom withered).
    public readonly Dictionary<NodeKind, int> EndedCount = new();

    // A part grew (not loaded): counts it, and logs its kind's first (a wild flower's as its discovery; not fruit's:
    // Jeremy, Nov 3) and any part count reached for the first time.
    internal void Grew(NodeSpec spec, int parts)
    {
        Grown[spec.Kind] = Grown.GetValueOrDefault(spec.Kind) + 1;
        bool found = Discovered.TryAdd(spec.Kind, Age);
        if (spec.IsWild)
        {
            if (found) Log(JournalKind.Discovery, $"Wild flower discovered: {spec.Name}");
        }
        else if (spec.Kind == NodeKind.Hub) First("grew:Hub", "First Hub planted");
        else if (!spec.IsFruit) First($"grew:{spec.Kind}", $"First {spec.Name.ToLowerInvariant()} emerged");
        if (parts <= MostParts) return;
        foreach (var milestone in PartMilestones)
            if (MostParts < milestone && parts >= milestone) Log(JournalKind.Milestone, $"The garden reached {milestone} parts");
        MostParts = parts;
    }

    // The garden has this many main nodes now (not loaded): its size rises if that's a new most.
    internal void Reached(int mainNodes) => MostMainNodes = Math.Max(MostMainNodes, mainNodes);

    // A wild flower's life cycle ended it: "An Ember burned out", "A Comet Bloom withered".
    internal void Ended(NodeSpec spec)
    {
        EndedCount[spec.Kind] = EndedCount.GetValueOrDefault(spec.Kind) + 1;
        Log(JournalKind.Ended, $"{Article(spec.Name)} {spec.Name} {EndedVerb(spec)}");
    }

    public static string EndedVerb(NodeSpec spec) => spec.Dims ? "burned out" : "withered";

    // "A" or "An" before a name.
    public static string Article(string name) => "AEIOU".Contains(name[0]) ? "An" : "A";

    // ==== SAVES ==== \\

    public TrackingSave ToSave() => new(Age, new(Grown), Pruned, PassiveBuds, MostParts, History.ToSave(),
        Journal.ToList(), Firsts.ToList(), MostMainNodes, new(Discovered), new(EndedCount));

    public static Tracking FromSave(TrackingSave? save)
    {
        var tracking = new Tracking();
        if (save == null) return tracking;
        tracking.Age = save.Age;
        foreach (var (kind, count) in save.Grown) tracking.Grown[kind] = count;
        tracking.Pruned = save.Pruned;
        tracking.PassiveBuds = save.PassiveBuds;
        tracking.MostParts = save.MostParts;
        tracking.MostMainNodes = save.MostMainNodes;
        tracking.History.Load(save.History);
        tracking.Journal.AddRange(save.Journal);
        tracking.Firsts.UnionWith(save.Firsts);
        // A save from before step 5d has no discoveries: the kinds it grew count as found at its start.
        if (save.Discovered != null) foreach (var (kind, age) in save.Discovered) tracking.Discovered[kind] = age;
        else foreach (var kind in save.Grown.Keys.Append(NodeKind.Core)) tracking.Discovered[kind] = 0;
        foreach (var (kind, count) in save.Ended ?? []) tracking.EndedCount[kind] = count;
        return tracking;
    }

    // ==== EXPORT ==== \\
    // A debug aid (the Developer menu, --export): plain CSV, which any spreadsheet opens. Ages in hours, to chart.

    // Every sample, oldest first, with its tier.
    public string HistoryCsv()
    {
        var csv = new StringBuilder("tier,start_hours,seconds,power,gen_per_s,drain_per_s,genes\n");
        void Tier(string name, IEnumerable<Sample> samples)
        {
            foreach (var s in samples)
                csv.Append(Invariant($"{name},{s.Start / 3600:0.####},{s.Seconds:0.###},{s.Power:0.##},{s.Gen:0.###},{s.Drain:0.###},{s.Genes:0.##}\n"));
        }
        Tier("day", History.Days);
        Tier("hour", History.Hours);
        Tier("minute", History.Minutes);
        return csv.ToString();
    }

    // The lifetime totals, one per row (genes and upgrades bought are the garden's own: see Garden.Genes, Garden.Buy).
    public string TotalsCsv(Garden garden)
    {
        var csv = new StringBuilder("total,value\n");
        void Row(string name, double value) => csv.Append(Invariant($"{name},{value:0.##}\n"));
        Row("age_hours", Age / 3600);
        Row("genes_earned", garden.Genes);
        Row("gene_balance", garden.GeneBalance);
        Row("parts_grown", PartsGrown);
        Row("parts_pruned", Pruned);
        Row("passive_buds", PassiveBuds);
        Row("upgrades_bought", garden.Bought);
        Row("stems_bought", garden.StemsBought);
        Row("generators_bought", garden.GeneratorsBought);
        Row("core_bought", garden.CoreBought);
        Row("parts_now", garden.Nodes.Count);
        Row("most_main_nodes", MostMainNodes);
        Row("wild_flowers_now", garden.WildCount);
        Row("wild_flower_limit", garden.Capacity.WildFlowers);
        Row("kinds_discovered", Discovered.Count);
        Row("wild_flowers_ended", EndedCount.Values.Sum());
        Row("evolutions_chosen", garden.Evolved.Count);
        Row("seedlings_bloomed", garden.Blooms);
        Row("seedling_slots", garden.Capacity.Seedlings);
        Row("hub_genes", garden.HubGenes);
        Row("hub_points_earned", garden.HubPointsEarned);
        Row("hub_levels", garden.HubLevelsBought);
        foreach (var kind in Enum.GetValues<NodeKind>())
            if (Grown.TryGetValue(kind, out var count)) Row($"grown_{kind.ToString().ToLowerInvariant()}", count);
        return csv.ToString();
    }

    public string JournalCsv()
    {
        var csv = new StringBuilder("age_hours,day,time,kind,text\n");
        foreach (var e in Journal)
            csv.Append(Invariant($"{e.Age / 3600:0.####},{DayOf(e.Age)},{ClockOf(e.Age)},{e.Kind},\"{e.Text.Replace("\"", "\"\"")}\"\n"));
        return csv.ToString();
    }

    static string Invariant(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    // The Almanac's calendar: the day of the garden's life an age falls on (from Day 1), and the time on that day.
    public static int DayOf(double age) => (int)Math.Floor(age / History.Day) + 1;

    public static string ClockOf(double age)
    {
        int minutes = (int)Math.Floor(age % History.Day / 60);
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }

    // A garden age, short: "0m", "12m", "4h 12m", "3d 4h".
    public static string AgeText(double seconds)
    {
        double minutes = Math.Floor(seconds / 60);
        if (minutes < 60) return $"{minutes:0}m";
        double hours = Math.Floor(minutes / 60);
        if (hours < 24) return $"{hours:0}h {minutes % 60:0}m";
        return $"{Math.Floor(hours / 24):0}d {hours % 24:0}h";
    }
}

// Unlock is the old unlock track's (before step 5b), kept so older journals still read; Purchase is a core link or the
// Hub bought, Tool a shaping tool gained from the garden's size; Discovery a wild flower kind's first appearance, Ended
// a wild flower whose life cycle ended it (step 5d); Chosen and Bloomed an evolution chosen for a seedling, and its
// seedling bloomed (step 5c).
public enum JournalKind { Planted, First, Unlock, Milestone, Purchase, Tool, Discovery, Ended, Chosen, Bloomed }

// One moment in the Almanac: when (the garden's age), what sort, and what happened.
public sealed record JournalEntry(double Age, JournalKind Kind, string Text);

// One stretch of the garden's history: from Start (garden age) for Seconds, the stored power (the parts', fruit
// aside, as the garden card's Power) and Gen and Drain (per growth second) on average over it, and the genes that
// reached the core in it.
public readonly record struct Sample(double Start, double Seconds, float Power, float Gen, float Drain, double Genes);

// The four time series, tiered (DESIGN.md, Step 4): a sample a minute for the last day, an hour for the last month, a
// day beyond that. As minutes pass a day old they're rolled up, a whole hour at a time, into one hour's sample, and
// hours a month old into days. Fed once per economy step with sums the step works out anyway, so a three-day catch-up
// costs a few thousand samples and nothing per part.
public sealed class History
{
    public const double Minute = 60, Hour = 3600, Day = 86400;
    public const double MinuteSpan = Day, HourSpan = 30 * Day;

    public readonly List<Sample> Minutes = new(), Hours = new(), Days = new();

    // The minute being gathered: when it started, how long so far, and the time-weighted sums.
    double start, seconds, power, gen, drain, genes;

    // Every sample, oldest first.
    public IEnumerable<Sample> All => Days.Concat(Hours).Concat(Minutes);

    // Genes reached the core (during a step: they count in the minute it closes).
    public void AddGenes(double amount) => genes += amount;

    // One economy step of seconds ending at age, its stored power and rates as they stood. Closes the minute once
    // it's whole, then rolls up what has aged out of the finer tiers.
    public void Record(double age, double dt, float stored, float production, float upkeep)
    {
        if (seconds == 0) start = age - dt;
        seconds += dt;
        power += stored * dt;
        gen += production * dt;
        drain += upkeep * dt;
        if (seconds < Minute - 1e-6) return;
        Minutes.Add(new Sample(start, seconds, (float)(power / seconds), (float)(gen / seconds), (float)(drain / seconds), genes));
        seconds = power = gen = drain = genes = 0;
        RollUp(Minutes, Hours, Hour, age - MinuteSpan);
        RollUp(Hours, Days, Day, age - HourSpan);
    }

    // Folds the oldest samples into the coarser tier, a whole bucket (aligned to the garden's age) at a time, while
    // the bucket ended before keepAfter.
    static void RollUp(List<Sample> from, List<Sample> into, double bucket, double keepAfter)
    {
        int taken = 0;
        while (taken < from.Count)
        {
            double begins = Math.Floor(from[taken].Start / bucket) * bucket;
            if (begins + bucket > keepAfter + 1e-6) break;
            int end = taken;
            while (end < from.Count && from[end].Start < begins + bucket - 1e-6) end++;
            into.Add(Merge(from, taken, end));
            taken = end;
        }
        if (taken > 0) from.RemoveRange(0, taken);
    }

    // Samples [first, end) as one: the levels and rates averaged over their time, the genes summed.
    static Sample Merge(List<Sample> samples, int first, int end)
    {
        double seconds = 0, power = 0, gen = 0, drain = 0, genes = 0;
        for (int i = first; i < end; i++)
        {
            var s = samples[i];
            seconds += s.Seconds;
            power += s.Power * s.Seconds;
            gen += s.Gen * s.Seconds;
            drain += s.Drain * s.Seconds;
            genes += s.Genes;
        }
        return new Sample(samples[first].Start, seconds, (float)(power / seconds), (float)(gen / seconds),
            (float)(drain / seconds), genes);
    }

    public HistorySave ToSave() => new(Minutes.ToList(), Hours.ToList(), Days.ToList(),
        new Sample(start, seconds, (float)power, (float)gen, (float)drain, genes));

    internal void Load(HistorySave save)
    {
        Minutes.AddRange(save.Minutes);
        Hours.AddRange(save.Hours);
        Days.AddRange(save.Days);
        // The minute being gathered is saved with its sums, not its averages.
        (start, seconds, power, gen, drain, genes) = (save.Open.Start, save.Open.Seconds, save.Open.Power, save.Open.Gen,
            save.Open.Drain, save.Open.Genes);
    }
}

// What changed while the player was away (DESIGN.md, Step 4: "While you were away"): taken from the tracking and the
// journal between a Mark made before a catch-up and the garden after it.
// Highlighted are the lines to pick out (the wild flower kinds discovered), by index.
public sealed record AwayNote(double Seconds, IReadOnlyList<string> Lines, IReadOnlyList<int> Highlighted)
{
    // The journal's moments listed at most, the rest left to the Almanac.
    public const int MostMoments = 3;
    // A dip worth a line: stored power that fell below this share of what the garden held as the player left (not of
    // its store, which grows as it does: new parts start empty).
    public const float DipFill = 0.5f;

    public bool Any => Lines.Count > 0;

    public sealed record Mark(double Age, int JournalCount, Dictionary<NodeKind, int> Grown, double Genes, float Power,
        Dictionary<NodeKind, int> Ended, int HubPoints = 0);

    public static Mark MarkNow(Garden garden) =>
        new(garden.Tracking.Age, garden.Tracking.Journal.Count, new(garden.Tracking.Grown), garden.Genes,
            garden.Nodes.Where(n => n.HoldsGardenPower).Sum(n => n.Power), new(garden.Tracking.EndedCount),
            garden.HubPointsEarned);

    public static AwayNote Since(Garden garden, Mark mark)
    {
        var tracking = garden.Tracking;
        var lines = new List<string>();
        var highlighted = new List<int>();

        // Seedlings bloomed first (step 5c): "A seedling bloomed: Second Path, now a Tendril."
        var bloomed = garden.Evolved.Where(e => e.Bloomed && e.BloomedAt >= mark.Age).OrderBy(e => e.BloomedAt)
            .Select(e => $"{e.Name}, now {Tracking.Article(Name(e.Spec.Becomes)).ToLowerInvariant()} {Name(e.Spec.Becomes)}")
            .ToList();
        if (bloomed.Count > 0) lines.Add($"{(bloomed.Count == 1 ? "A seedling" : $"{bloomed.Count} seedlings")} bloomed: {string.Join("; ", bloomed)}.");

        var grew = Enum.GetValues<NodeKind>()
            .Select(k => (Kind: k, Count: tracking.Grown.GetValueOrDefault(k) - mark.Grown.GetValueOrDefault(k)))
            .Where(g => g.Count > 0).ToList();
        // The new parts, by kind (fruit not among them), then the fruit, only how many (Jeremy, Nov 3).
        var basic = grew.Where(g => !NodeSpecs.All[g.Kind].IsWild && !NodeSpecs.All[g.Kind].IsFruit).ToList();
        int parts = basic.Sum(g => g.Count);
        if (parts > 0)
        {
            var kinds = basic.Select(g => Count(g.Count, NodeSpecs.All[g.Kind].Name.ToLowerInvariant())).ToList();
            lines.Add($"{(parts == 1 ? "A new part" : $"{parts} new parts")} grew: {List(kinds)}.");
        }
        int fruit = grew.Where(g => !NodeSpecs.All[g.Kind].IsWild && NodeSpecs.All[g.Kind].IsFruit).Sum(g => g.Count);
        if (fruit > 0) lines.Add(fruit == 1 ? "A fruit grew." : $"{fruit} fruits grew.");

        // The wild flowers that grew (by their names), each kind new to the garden on a line of its own, picked out;
        // then those whose life cycle ended them.
        var wild = grew.Where(g => NodeSpecs.All[g.Kind].IsWild).ToList();
        if (wild.Count > 0)
        {
            var names = wild.Select(g => g.Count == 1 ? $"{Article(g.Kind).ToLowerInvariant()} {Name(g.Kind)}"
                : $"{g.Count} {Name(g.Kind)}s").ToList();
            lines.Add($"Wild flowers grew: {List(names)}.");
        }
        foreach (var entry in tracking.Journal.Skip(mark.JournalCount).Where(e => e.Kind == JournalKind.Discovery))
        {
            highlighted.Add(lines.Count);
            lines.Add($"{entry.Text} (see the Codex).");
        }
        foreach (var kind in Enum.GetValues<NodeKind>())
        {
            int ended = tracking.EndedCount.GetValueOrDefault(kind) - mark.Ended.GetValueOrDefault(kind);
            if (ended <= 0) continue;
            var spec = NodeSpecs.All[kind];
            lines.Add(ended == 1 ? $"{Tracking.Article(spec.Name)} {spec.Name} {Tracking.EndedVerb(spec)}."
                : $"{ended} {spec.Name}s {Tracking.EndedVerb(spec)}.");
        }

        double genes = garden.Genes - mark.Genes;
        if (genes >= 1) lines.Add($"{genes:#,0} genes reached the core.");
        // Hub points earned: "The Hub earned 3 points (5 to spend)."
        int points = garden.HubPointsEarned - mark.HubPoints;
        if (points > 0) lines.Add($"The Hub earned {(points == 1 ? "a point" : $"{points} points")} ({garden.HubPoints} to spend).");

        // (Blooms, discoveries and endings have their own lines above.)
        var moments = tracking.Journal.Skip(mark.JournalCount)
            .Where(e => e.Kind is not (JournalKind.Discovery or JournalKind.Ended or JournalKind.Chosen or JournalKind.Bloomed))
            .ToList();
        // Shaping tools gained first (what there's to place now), then firsts and milestones.
        static bool Gained(JournalEntry m) => m.Kind is JournalKind.Tool or JournalKind.Purchase or JournalKind.Unlock;
        var shown = moments.Where(Gained).Concat(moments.Where(m => !Gained(m))).Take(MostMoments).ToList();
        foreach (var m in shown) lines.Add($"{m.Text}.");
        if (moments.Count > shown.Count) lines.Add($"{moments.Count - shown.Count} more in the Almanac.");

        // (The history samples power without the fruit, as Mark does.)
        var during = tracking.History.All.Where(s => s.Start >= mark.Age - 1e-6).ToList();
        if (mark.Power > 0 && during.Count > 0)
        {
            float lowest = during.Min(s => s.Power) / mark.Power;
            if (lowest < DipFill) lines.Add($"Power ran low: down to {lowest * 100:0}% of what it held when you left.");
        }
        return new AwayNote(tracking.Age - mark.Age, lines, highlighted);
    }

    static string Name(NodeKind kind) => NodeSpecs.All[kind].Name;

    static string Article(NodeKind kind) => Tracking.Article(Name(kind));

    static string Count(int n, string name) => n == 1 ? $"1 {name}" : $"{n} {Plural(name)}";

    static string Plural(string name) => name switch
    {
        "research" => "research",
        "leaf" => "leaves",
        "berry" => "berries",
        _ => name + "s",
    };

    static string List(List<string> items) =>
        items.Count == 1 ? items[0] : string.Join(", ", items[..^1]) + " and " + items[^1];
}

public sealed record HistorySave(List<Sample> Minutes, List<Sample> Hours, List<Sample> Days, Sample Open);

public sealed record TrackingSave(double Age, Dictionary<NodeKind, int> Grown, int Pruned, int PassiveBuds, int MostParts,
    HistorySave History, List<JournalEntry> Journal, List<string> Firsts, int MostMainNodes = 0,
    Dictionary<NodeKind, double>? Discovered = null, Dictionary<NodeKind, int>? Ended = null);
