using System.Collections.Concurrent;
using System.Text;

namespace SpaceGarden.Core.Tests;

// Balance aids, run by hand (BALANCE=1). The wildflower test (Jeremy, Nov 4): at each stage of the game (its stem,
// generator and core link unlocks), the best garden with no wildflower against the best garden built around one
// wildflower of each kind, no seedlings or Hub. "Best" is a search: a layout is changed a part at a time (add, prune,
// change, move, insert a stem) and kept when it sends more genes, and no layout that would overload (cost more full
// than it makes full) counts. Each layout is measured as a mature garden: every part's fruit out, no growth, an hour to
// settle, then the genes reaching the core in the next hour.
public class BalanceTests
{
    const double Hour = 3600;

    // The stages: the unlocks of the pacing run's typical garden at about day 1, 2 and 3 (Nov 4: its stems and
    // generators bought each day), with no seedling's evolutions (so the core keeps its 2 links).
    public sealed record Stage(string Name, int Stems, int Generators, int CoreLinks);
    static readonly Stage[] Stages =
    [
        new("day 1", 12, 4, 2),
        new("day 2", 16, 6, 2),
        new("day 3", 19, 8, 2),
    ];

    // The wildflowers tested in isolation (the rest need growth, or other wildflowers, to show).
    static readonly NodeKind[] Tested =
    [
        NodeKind.Pulsar, NodeKind.TwinStar, NodeKind.Ember, NodeKind.WildLab, NodeKind.Lichen,
        NodeKind.Tendril, NodeKind.BigStem, NodeKind.Amplifier, NodeKind.Buffer,
    ];

    // ==== LAYOUTS ==== \\

    // A garden's shape, as a tree of kinds under the core.
    public sealed class Part
    {
        public NodeKind Kind;
        public readonly List<Part> Kids = new();
        public Part? Up;
        public Part(NodeKind kind) => Kind = kind;

        public IEnumerable<Part> All()
        {
            yield return this;
            foreach (var kid in Kids) foreach (var p in kid.All()) yield return p;
        }

        public Part Copy()
        {
            var copy = new Part(Kind);
            foreach (var kid in Kids)
            {
                var k = kid.Copy();
                k.Up = copy;
                copy.Kids.Add(k);
            }
            return copy;
        }

        public void Attach(Part kid)
        {
            kid.Up = this;
            Kids.Add(kid);
        }

        public void Detach()
        {
            Up!.Kids.Remove(this);
            Up = null;
        }

        public bool Under(Part other)
        {
            for (var at = this; at != null; at = at.Up) if (at == other) return true;
            return false;
        }

        public string Show() => Kids.Count == 0 ? Short(Kind) : $"{Short(Kind)}({string.Join(" ", Kids.Select(k => k.Show()))})";
    }

    static string Short(NodeKind kind) => kind switch
    {
        NodeKind.Core => "C", NodeKind.Stem => "s", NodeKind.Generator => "g", NodeKind.Lab => "L",
        _ => NodeSpecs.All[kind].Name.Replace(" ", "").ToUpperInvariant(),
    };

    // (A part's links count the one to its parent; the core's are all branches.)
    static int Links(Part p, Stage stage) => p.Kind == NodeKind.Core ? stage.CoreLinks : NodeSpecs.All[p.Kind].MaxLinks - 1;

    // Whether a layout fits the stage's caps and holds the wildflower once (or none, for the baseline).
    static bool Fits(Part core, Stage stage, NodeKind? wild)
    {
        int stems = 0, generators = 0, wilds = 0;
        foreach (var p in core.All())
        {
            if (p.Kids.Count > Links(p, stage)) return false;
            if (p.Kind == NodeKind.Core) continue;
            var slot = Garden.SlotOf(p.Kind);
            if (slot == NodeKind.Stem) stems++;
            if (slot == NodeKind.Generator) generators++;
            if (NodeSpecs.All[p.Kind].IsWild) wilds++;
        }
        return stems <= stage.Stems && generators <= stage.Generators && wilds == (wild == null ? 0 : 1);
    }

    // ==== MEASURING ==== \\

    public sealed record Measure(double GenesPerHour, float Production, float Upkeep, int Stems, int Generators, int Labs,
        double LabFill)
    {
        public bool Overloaded => Upkeep > Production;
        // What the search climbs: the genes, or, overloaded, how far over (always below any garden that isn't).
        public double Score => Overloaded ? -(Upkeep - Production) : GenesPerHour;
    }

    // The layout grown as a garden, every part's fruit out and full of power, then an hour to settle and an hour
    // measured.
    static Garden Build(Part core, Stage stage, int seed)
    {
        var garden = Garden.New(Layout.Radial, seed);
        garden.Capacity = new Capacity(stage.Stems, stage.Generators, stage.CoreLinks, WildFlowers: 1);
        garden.PassiveGrowth = false;
        garden.ShowBlobs = false;
        void Grow(Part part, PlantNode node)
        {
            foreach (var kid in part.Kids)
                Grow(kid, garden.GrowBranch(node, kid.Kind) ?? throw new InvalidOperationException($"no room for {kid.Kind}"));
        }
        Grow(core, garden.Core);
        foreach (var node in garden.Nodes.ToList())
            if (!node.Spec.IsFruit)
                while (node.CanFruit) garden.GrowFruit(node);
        foreach (var node in garden.Nodes) node.Power = node.Store;
        return garden;
    }

    static bool Overloads(Part core, Stage stage) =>
        Build(core, stage, 0) is var garden && garden.FullPowerBalance() is var (production, upkeep)
        && upkeep > production * (garden.Nodes.Any(n => n.Spec.Sends == BlobKind.Buff) ? BuffSlack : 1);

    // A garden with something sending buff motes (a Buffer, or glow fruit) may lean on its buffed generators to keep
    // up, so it isn't turned away before it's run unless it's over by more than a buff could make up; once run, it
    // overloads if what it would cost full is more than it would make full on average over the hour, buffs and all.
    const float BuffSlack = 1.3f;

    static Measure Run(Part core, Stage stage, int seed)
    {
        var garden = Build(core, stage, seed);
        var (production, upkeep) = garden.FullPowerBalance();
        var main = garden.Nodes.Where(n => !n.Spec.IsFruit && n != garden.Core).ToList();
        int stems = main.Count(n => Garden.SlotOf(n.Spec.Kind) == NodeKind.Stem);
        int generators = main.Count(n => Garden.SlotOf(n.Spec.Kind) == NodeKind.Generator);
        var labs = main.Where(n => n.Spec.Sends == BlobKind.Genes).ToList();
        bool buffs = garden.Nodes.Any(n => n.Spec.Sends == BlobKind.Buff);
        if (upkeep > production * (buffs ? BuffSlack : 1)) return new(0, production, upkeep, stems, generators, labs.Count, 0);
        garden.Grow(Hour);
        double before = garden.Genes, fill = 0;
        float made = 0;
        const int Samples = 60;
        for (int i = 0; i < Samples; i++)
        {
            garden.Grow(Hour / Samples);
            fill += labs.Count == 0 ? 0 : labs.Average(n => Math.Min(1, n.Fill)) / Samples;
            made += garden.FullPowerBalance().Production / Samples;
        }
        if (upkeep > made) return new(0, made, upkeep, stems, generators, labs.Count, fill);
        return new(garden.Genes - before, made, upkeep, stems, generators, labs.Count, fill);
    }

    // ==== THE SEARCH ==== \\

    static readonly NodeKind[] Basics = [NodeKind.Stem, NodeKind.Generator, NodeKind.Lab];

    // One change to a copy of the layout, or null if the change picked can't be made.
    static Part? Change(Part core, Stage stage, NodeKind? wild, Random rng)
    {
        var copy = core.Copy();
        var parts = copy.All().ToList();
        var kinds = wild is { } w ? Basics.Append(w).ToArray() : Basics;
        NodeKind AnyKind() => kinds[rng.Next(kinds.Length)];
        var open = parts.Where(p => p.Kids.Count < Links(p, stage)).ToList();
        var others = parts.Where(p => p.Kind != NodeKind.Core).ToList();
        switch (rng.Next(9))
        {
            case 0: // a new part on a free link
                if (open.Count == 0) return null;
                open[rng.Next(open.Count)].Attach(new Part(AnyKind()));
                break;
            case 1: // a part pruned, with all beyond it
                if (others.Count == 0) return null;
                others[rng.Next(others.Count)].Detach();
                break;
            case 2: // a part becomes another kind
            {
                if (others.Count == 0) return null;
                var p = others[rng.Next(others.Count)];
                p.Kind = AnyKind();
                break;
            }
            case 3: // a branch moved to another free link
            {
                if (others.Count == 0) return null;
                var p = others[rng.Next(others.Count)];
                var to = open.Where(o => !o.Under(p) && o != p.Up).ToList();
                if (to.Count == 0) return null;
                p.Detach();
                to[rng.Next(to.Count)].Attach(p);
                break;
            }
            case 4: // a stem put in before a part
            {
                if (others.Count == 0) return null;
                var p = others[rng.Next(others.Count)];
                var up = p.Up!;
                p.Detach();
                var stem = new Part(rng.Next(3) == 0 && wild is { } k ? k : NodeKind.Stem);
                up.Attach(stem);
                stem.Attach(p);
                break;
            }
            case 5: // a new stem on a free link, with a lab and a generator (or two labs) on it
            {
                if (open.Count == 0) return null;
                var stem = new Part(NodeKind.Stem);
                stem.Attach(new Part(NodeKind.Lab));
                stem.Attach(new Part(rng.Next(2) == 0 ? NodeKind.Generator : NodeKind.Lab));
                open[rng.Next(open.Count)].Attach(stem);
                break;
            }
            case 6: // the wildflower moved: what grew beyond it goes to its parent, and it goes in before another part
            {
                if (wild is not { } k || copy.All().FirstOrDefault(p => p.Kind == k) is not { } flower) return null;
                var up = flower.Up!;
                flower.Detach();
                foreach (var kid in flower.Kids.ToList())
                {
                    flower.Kids.Remove(kid);
                    up.Attach(kid);
                }
                var spots = copy.All().Where(p => p.Kind != NodeKind.Core).ToList();
                if (Links(flower, stage) == 0 || spots.Count == 0)
                {
                    var free = copy.All().Where(p => p.Kids.Count < Links(p, stage)).ToList();
                    if (free.Count == 0) return null;
                    free[rng.Next(free.Count)].Attach(flower);
                    break;
                }
                var before = spots[rng.Next(spots.Count)];
                var above = before.Up!;
                before.Detach();
                above.Attach(flower);
                flower.Attach(before);
                break;
            }
            default: // two parts swap kinds
            {
                if (others.Count < 2) return null;
                var a = others[rng.Next(others.Count)];
                var b = others[rng.Next(others.Count)];
                (a.Kind, b.Kind) = (b.Kind, a.Kind);
                break;
            }
        }
        // (Cleared of anything left dangling that can only cost: a stem or generator at the end of a branch is kept,
        // as the search may still build on it.)
        return Fits(copy, stage, wild) ? copy : null;
    }

    // A first layout: random parts added, each kept if the garden still doesn't overload, until nothing more fits.
    static Part Seed(Stage stage, NodeKind? wild, Random rng)
    {
        var core = new Part(NodeKind.Core);
        if (wild is { } w) core.Attach(new Part(w)); // (the one wildflower the search must keep)
        for (int i = 0, misses = 0; i < 400 && misses < 60; i++)
        {
            var open = core.All().Where(p => p.Kids.Count < Links(p, stage)).ToList();
            if (open.Count == 0) break;
            var next = core.Copy();
            var spot = next.All().Where(p => p.Kids.Count < Links(p, stage)).ToList()[rng.Next(open.Count)];
            spot.Attach(new Part(Basics[rng.Next(Basics.Length)]));
            if (Fits(next, stage, wild) && !Overloads(next, stage)) (core, misses) = (next, 0);
            else misses++;
        }
        return core;
    }

    // Annealed climb: a change is kept if it scores better, or a little worse early on; the best seen is returned.
    static (Part Layout, Measure Measure) Climb(Part start, Stage stage, NodeKind? wild, int steps, int seed)
    {
        var rng = new Random(seed);
        var at = start;
        var atMeasure = Run(at, stage, seed);
        var (best, bestMeasure) = (at, atMeasure);
        for (int i = 0; i < steps; i++)
        {
            if (Change(at, stage, wild, rng) is not { } next) continue;
            var m = Run(next, stage, seed);
            double temperature = 0.02 * (1 - (double)i / steps); // (share of the score a step may lose)
            double scale = Math.Max(1, Math.Abs(atMeasure.Score));
            if (m.Score >= atMeasure.Score || rng.NextDouble() < Math.Exp((m.Score - atMeasure.Score) / (temperature * scale + 1e-9)))
                (at, atMeasure) = (next, m);
            if (m.Score > bestMeasure.Score) (best, bestMeasure) = (next, m);
        }
        return (best, bestMeasure);
    }

    // The best layout found for a stage and wildflower: a few climbs from fresh starts and from the given layouts (the
    // baseline's best, for a wildflower), the winner judged again over more seeds.
    static (Part Layout, double Genes, double Spread, Measure Measure) Best(Stage stage, NodeKind? wild, int steps,
        IEnumerable<Part> from, int seed, int fresh = 2)
    {
        var rng = new Random(seed);
        var starts = Enumerable.Range(0, fresh).Select(_ => Seed(stage, wild, rng)).ToList();
        foreach (var f in from)
        {
            var s = f.Copy();
            if (wild is { } w && !Fits(s, stage, w))
            {
                // (Fitted with the wildflower: on a free link if there is one, else in place of a part of its slot.)
                var open = s.All().Where(p => p.Kids.Count < Links(p, stage)).ToList();
                if (open.Count > 0) open[rng.Next(open.Count)].Attach(new Part(w));
                else
                {
                    var swap = s.All().Where(p => p.Kind != NodeKind.Core && p.Kids.Count <= NodeSpecs.All[w].MaxLinks - 1).ToList();
                    swap[rng.Next(swap.Count)].Kind = w;
                }
                while (!Fits(s, stage, w))
                {
                    // (Over a cap: prune a basic part of the wildflower's slot, from the end of a branch.)
                    var slot = Garden.SlotOf(w);
                    var leaf = s.All().LastOrDefault(p => p.Kids.Count == 0 && p.Kind != w && p.Kind != NodeKind.Core
                        && (slot == null || Garden.SlotOf(p.Kind) == slot));
                    if (leaf == null) break;
                    leaf.Detach();
                }
                if (!Fits(s, stage, w)) continue;
            }
            starts.Add(s);
        }
        var climbs = starts.AsParallel().Select((s, i) => Climb(s, stage, wild, steps, seed * 31 + i)).ToList();
        return Judge(climbs.MaxBy(c => c.Measure.Score).Layout, stage);
    }

    // A layout's genes over six more seeds: their mean and spread, and the first one's measure.
    static (Part Layout, double Genes, double Spread, Measure Measure) Judge(Part layout, Stage stage)
    {
        var judged = Enumerable.Range(0, 6).Select(i => Run(layout, stage, 1000 + i)).ToList();
        double mean = judged.Average(m => m.GenesPerHour);
        double spread = Math.Sqrt(judged.Average(m => Math.Pow(m.GenesPerHour - mean, 2)));
        return (layout, mean, spread, judged[0]);
    }

    // A wildflower's layout without it, if that leaves a garden that doesn't overload.
    static Part? Strip(Part layout, Stage stage, NodeKind wild)
    {
        var copy = layout.Copy();
        var w = copy.All().First(p => p.Kind == wild);
        if (w.Kids.Count == 0) w.Detach();
        else
        {
            w.Kind = NodeKind.Stem;
            if (!Fits(copy, stage, null)) w.Detach();
        }
        return Fits(copy, stage, null) && !Overloads(copy, stage) ? copy : null;
    }

    [Fact]
    public void WildflowerBalance()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("BALANCE") == "1", "a tuning aid, run by hand (BALANCE=1)");
        int steps = int.TryParse(Environment.GetEnvironmentVariable("BALANCE_STEPS"), out int s) ? s : 1500;
        var text = new StringBuilder();
        text.AppendLine($"Wildflower balance: the best garden at each stage with one wildflower, against the best with none ({steps} search steps a climb).");
        var started = DateTime.Now;
        var results = new ConcurrentDictionary<(int Stage, NodeKind? Wild), (Part Layout, double Genes, double Spread, Measure Measure)>();
        // Stage by stage, each search starting from the stage before's best too (its caps are less); the baseline first
        // (the wildflowers' searches start from it too), then the wildflowers side by side.
        int master = int.TryParse(Environment.GetEnvironmentVariable("BALANCE_SEED"), out int ms) ? ms : 0;
        var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
        for (int i = 0; i < Stages.Length; i++)
        {
            int at = i;
            var before = (NodeKind? w) => at == 0 ? Enumerable.Empty<Part>() : [results[(at - 1, w)].Layout];
            results[(i, null)] = Best(Stages[i], null, steps, before(null), master + 11 + i, fresh: 6);
            Parallel.ForEach(Tested, options, w =>
                results[(at, w)] = Best(Stages[at], w, steps, before(w).Append(results[(at, null)].Layout),
                    master + 101 + at * 17 + (int)w));
            // The baseline is at least as good as any wildflower's garden with the wildflower taken out (a stem in its
            // place if it held a branch and a stem fits, else it goes with what grew beyond it), climbed a little more.
            var stripped = Tested.Select(w => Strip(results[(at, w)].Layout, Stages[at], w)).Where(p => p != null).ToList();
            var better = stripped.AsParallel().Select((p, k) => Climb(p!, Stages[at], null, steps / 5, master + 500 + k))
                .Select(c => Judge(c.Layout, Stages[at])).MaxBy(r => r.Genes);
            if (better.Genes > results[(i, null)].Genes) results[(i, null)] = better;
        }
        for (int i = 0; i < Stages.Length; i++)
        {
            var stage = Stages[i];
            var baseline = results[(i, null)];
            text.AppendLine();
            text.AppendLine($"{stage.Name}: {stage.Stems} stems, {stage.Generators} generators, {stage.CoreLinks} core links");
            text.AppendLine($"  {"",-11} {"genes/h",8} {"±",5} {"vs none",8}  {"stems",5} {"gens",4} {"labs",4} {"lab fill",8} {"power",11}");
            void Row(string name, (Part Layout, double Genes, double Spread, Measure Measure) r)
            {
                var m = r.Measure;
                string delta = name == "none" ? "" : $"{(r.Genes / baseline.Genes - 1) * 100:+0;-0}%";
                text.AppendLine($"  {name,-11} {r.Genes,8:0} {r.Spread,5:0} {delta,8}  {m.Stems,5} {m.Generators,4} {m.Labs,4} {m.LabFill,8:0%} {m.Production,5:0}/{m.Upkeep,-5:0}");
            }
            Row("none", baseline);
            foreach (var w in Tested.OrderByDescending(w => results[(i, w)].Genes)) Row(NodeSpecs.All[w].Name, results[(i, w)]);
            text.AppendLine("  layouts:");
            text.AppendLine($"    none: {baseline.Layout.Show()}");
            foreach (var w in Tested) text.AppendLine($"    {NodeSpecs.All[w].Name}: {results[(i, w)].Layout.Show()}");
        }
        text.AppendLine();
        text.AppendLine($"({(DateTime.Now - started).TotalMinutes:0.#} minutes)");
        if (Environment.GetEnvironmentVariable("BALANCE_OUT") is { Length: > 0 } path) File.WriteAllText(path, text.ToString());
        Assert.Skip(text.ToString());
    }

    // For a feel of the search's cost: one short climb.
    [Fact]
    public void BalanceRunTime()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("BALANCE") == "1", "a tuning aid, run by hand (BALANCE=1)");
        var rng = new Random(3);
        var core = Seed(Stages[2], null, rng);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var (best, m) = Climb(core, Stages[2], null, 100, 1);
        Assert.Skip($"{watch.ElapsedMilliseconds} ms for 100 steps: {m}; {best.Show()}");
    }
}
