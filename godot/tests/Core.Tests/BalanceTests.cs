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
    // Groves: how many groves a layout must have (a real garden keeps one to grow; Jeremy, Nov 4). Evolution: one bloomed
    // (its effect only: its caps are the stage's own, raised already). WildCount: how many of the wildflower a layout must
    // hold (Wild Bloom's two Lichens). Exchange: one slot may be traded, a generator's for a stem's or a stem's for a
    // generator's (Jeremy, Nov 4: at day 2 they cost about the same; one at most).
    public sealed record Stage(string Name, int Stems, int Generators, int CoreLinks, int Groves = 0,
        EvolutionKind? Evolution = null, int WildCount = 1, bool Exchange = false)
    {
        // The most stems a garden with so many generators may have.
        public int StemsWith(int generators) => Stems + (Exchange && generators < Generators ? 1 : 0);

        // Whether a garden may have so many stems and generators.
        public bool Allows(int stems, int generators) =>
            (stems <= Stems && generators <= Generators)
            || (Exchange && ((stems == Stems + 1 && generators < Generators) || (generators == Generators + 1 && stems < Stems)));
    }
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
        NodeKind.Core => "C", NodeKind.Stem => "s", NodeKind.Generator => "g", NodeKind.Lab => "L", NodeKind.Grove => "G",
        _ => NodeSpecs.All[kind].Name.Replace(" ", "").ToUpperInvariant(),
    };

    // (A part's links count the one to its parent; the core's are all branches.)
    static int Links(Part p, Stage stage) => p.Kind == NodeKind.Core ? stage.CoreLinks : NodeSpecs.All[p.Kind].MaxLinks - 1;

    // Whether a layout fits the stage's caps, has its groves, and holds the wildflower once (or none, for the baseline).
    static bool Fits(Part core, Stage stage, NodeKind? wild)
    {
        int stems = 0, generators = 0, wilds = 0, groves = 0;
        foreach (var p in core.All())
        {
            if (p.Kind == NodeKind.Grove) groves++;
            if (p.Kids.Count > Links(p, stage)) return false;
            if (p.Kind == NodeKind.Core) continue;
            var slot = Garden.SlotOf(p.Kind);
            if (slot == NodeKind.Stem) stems++;
            if (slot == NodeKind.Generator) generators++;
            if (NodeSpecs.All[p.Kind].IsWild) wilds++;
        }
        return stage.Allows(stems, generators) && wilds == (wild == null ? 0 : stage.WildCount) && groves == stage.Groves;
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
        if (stage.Evolution is { } evolution)
        {
            // (Its effect; the flower it grows goes, and its caps are the stage's.)
            garden.EvolveNow(evolution);
            foreach (var n in garden.Nodes.Where(n => n.Parent == garden.Core).ToList()) garden.Prune(n);
            garden.Cut.Clear();
        }
        garden.Capacity = new Capacity(stage.Stems + (stage.Exchange ? 1 : 0), stage.Generators + (stage.Exchange ? 1 : 0), stage.CoreLinks, WildFlowers: Math.Max(1, stage.WildCount));
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

    // (minutes: the time to settle and the time measured, an hour each unless screening, the genes counted an hour.)
    static Measure Run(Part core, Stage stage, int seed, double minutes = 60)
    {
        var garden = Build(core, stage, seed);
        var (production, upkeep) = garden.FullPowerBalance();
        var main = garden.Nodes.Where(n => !n.Spec.IsFruit && n != garden.Core).ToList();
        int stems = main.Count(n => Garden.SlotOf(n.Spec.Kind) == NodeKind.Stem);
        int generators = main.Count(n => Garden.SlotOf(n.Spec.Kind) == NodeKind.Generator);
        var labs = main.Where(n => n.Spec.Sends == BlobKind.Genes).ToList();
        bool buffs = garden.Nodes.Any(n => n.Spec.Sends == BlobKind.Buff);
        if (upkeep > production * (buffs ? BuffSlack : 1)) return new(0, production, upkeep, stems, generators, labs.Count, 0);
        double span = minutes * 60;
        garden.Grow(span);
        double before = garden.Genes, fill = 0;
        float made = 0;
        int Samples = (int)Math.Max(10, minutes);
        for (int i = 0; i < Samples; i++)
        {
            garden.Grow(span / Samples);
            fill += labs.Count == 0 ? 0 : labs.Average(n => Math.Min(1, n.Fill)) / Samples;
            made += garden.FullPowerBalance().Production / Samples;
        }
        if (upkeep > made) return new(0, made, upkeep, stems, generators, labs.Count, fill);
        return new((garden.Genes - before) * Hour / span, made, upkeep, stems, generators, labs.Count, fill);
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
        switch (rng.Next(10))
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
            case 9: // a branch's end extended: a part becomes a stem holding it and a generator and a lab (on a stem of
                    // their own), a generator, or a lab (Nov 4: one part at a time, the search never filled a full garden's
                    // spare stems and generators: a stem or generator alone only costs)
            {
                var leaves = others.Where(p => p.Kids.Count == 0).ToList();
                if (leaves.Count == 0) return null;
                foreach (var _ in Extend(leaves[rng.Next(leaves.Count)], rng.Next(3))) { }
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
        if (wild is { } w)
            for (int i = 0; i < stage.WildCount; i++) core.Attach(new Part(w)); // (the wildflowers the search must keep)
        if (stage.Groves > 0)
        {
            // (and the groves, on a stem of their own)
            var stem = new Part(NodeKind.Stem);
            for (int i = 0; i < stage.Groves; i++) stem.Attach(new Part(NodeKind.Grove));
            core.Attach(stem);
        }
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

    // A part at a branch's end extended in place (see Change, case 9): it moves down onto a new stem, which also holds
    // a stem with a generator and a lab (variant 0), a generator (1) or a lab (2). (An iterator only so a caller can
    // do it as a statement; it's done at once.)
    static IEnumerable<int> Extend(Part leaf, int variant)
    {
        var up = leaf.Up!;
        leaf.Detach();
        var stem = new Part(NodeKind.Stem);
        up.Attach(stem);
        stem.Attach(leaf);
        if (variant == 0)
        {
            var pair = new Part(NodeKind.Stem);
            pair.Attach(new Part(NodeKind.Generator));
            pair.Attach(new Part(NodeKind.Lab));
            stem.Attach(pair);
        }
        else stem.Attach(new Part(variant == 1 ? NodeKind.Generator : NodeKind.Lab));
        yield break;
    }

    // The finishing pass (Nov 4): while the caps have room, the best of every way to add to the garden (each end part
    // extended, see Extend, or a generator, a lab, or a stem with both on a free link), kept while it sends more genes
    // (screened), so no spare stem and generator is left that would pay.
    static Part Fill(Part layout, Stage stage, NodeKind? wild, double screen)
    {
        double Screen(Part p) => (Run(p, stage, 1, screen).Score + Run(p, stage, 2, screen).Score) / 2;
        double score = Screen(layout);
        for (int round = 0; round < 12; round++)
        {
            var tries = new List<Part>();
            int count = layout.All().Count();
            for (int i = 1; i < count; i++)
                for (int v = 0; v < 3; v++)
                {
                    var copy = layout.Copy();
                    var p = copy.All().ElementAt(i);
                    if (p.Kids.Count > 0) continue;
                    foreach (var _ in Extend(p, v)) { }
                    tries.Add(copy);
                }
            for (int i = 0; i < count; i++)
                for (int v = 0; v < 3; v++)
                {
                    var copy = layout.Copy();
                    var p = copy.All().ElementAt(i);
                    if (p.Kids.Count >= Links(p, stage)) continue;
                    if (v < 2) p.Attach(new Part(v == 0 ? NodeKind.Generator : NodeKind.Lab));
                    else
                    {
                        var pair = new Part(NodeKind.Stem);
                        pair.Attach(new Part(NodeKind.Generator));
                        pair.Attach(new Part(NodeKind.Lab));
                        p.Attach(pair);
                    }
                    tries.Add(copy);
                }
            var best = tries.Where(t => Fits(t, stage, wild) && !Overloads(t, stage)).DistinctBy(Key).AsParallel()
                .Select(t => (t, g: Screen(t))).OrderByDescending(x => x.g).FirstOrDefault();
            if (best.t == null || best.g <= score * 1.002) break;
            (layout, score) = (best.t, best.g);
        }
        return layout;
    }

    // Annealed climb: a change is kept if it scores better, or a little worse early on; the best seen is returned.
    static (Part Layout, Measure Measure) Climb(Part start, Stage stage, NodeKind? wild, int steps, int seed, double minutes = 60)
    {
        var rng = new Random(seed);
        var at = start;
        var atMeasure = Run(at, stage, seed, minutes);
        var (best, bestMeasure) = (at, atMeasure);
        for (int i = 0; i < steps; i++)
        {
            if (Change(at, stage, wild, rng) is not { } next) continue;
            var m = Run(next, stage, seed, minutes);
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

    // A layout written as Show writes it ("C(s(L g) s(L L))"): C core, s stem, g generator, L lab, a wildflower by its
    // name in capitals.
    static Part Parse(string text)
    {
        int at = 0;
        Part Read()
        {
            int start = at;
            while (at < text.Length && char.IsLetter(text[at])) at++;
            string name = text[start..at];
            var kind = name switch
            {
                "C" => NodeKind.Core, "s" => NodeKind.Stem, "g" => NodeKind.Generator, "L" => NodeKind.Lab, "G" => NodeKind.Grove,
                _ => NodeSpecs.All.Values.First(spec => spec.Name.Replace(" ", "").ToUpperInvariant() == name).Kind,
            };
            var part = new Part(kind);
            if (at < text.Length && text[at] == '(')
            {
                at++;
                while (text[at] != ')')
                {
                    if (text[at] == ' ') { at++; continue; }
                    part.Attach(Read());
                }
                at++;
            }
            return part;
        }
        return Read();
    }

    // Hand-built layouts (BALANCE_LAYOUTS, split by ';') at one stage (BALANCE_STAGE "stems,generators,core links,groves",
    // 12,4,2,1 by default: one grove, as a real garden keeps), each measured over six seeds, against a long search from scratch (BALANCE_STEPS a climb,
    // BALANCE_CLIMBS climbs: 3000 and 8 by default) and from each hand-built one.
    [Fact]
    public void HandBuilt()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("BALANCE") == "1", "a tuning aid, run by hand (BALANCE=1)");
        var caps = (Environment.GetEnvironmentVariable("BALANCE_STAGE") ?? "12,4,2,1").Split(',').Select(int.Parse).ToArray();
        var stage = new Stage("custom", caps[0], caps[1], caps[2], caps.Length > 3 ? caps[3] : 0);
        int steps = int.TryParse(Environment.GetEnvironmentVariable("BALANCE_STEPS"), out int st) ? st : 3000;
        int climbs = int.TryParse(Environment.GetEnvironmentVariable("BALANCE_CLIMBS"), out int c) ? c : 8;
        var hand = (Environment.GetEnvironmentVariable("BALANCE_LAYOUTS") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => Parse(t.Trim())).ToList();
        var text = new StringBuilder($"Hand-built against searched, {stage.Stems} stems, {stage.Generators} generators, {stage.CoreLinks} core links, {stage.Groves} grove(s):\n");
        void Row(string name, (Part Layout, double Genes, double Spread, Measure Measure) r) => text.AppendLine(
            $"  {name,-14} {r.Genes,6:0} ±{r.Spread,-4:0} {r.Measure.Stems,2}s {r.Measure.Generators,2}g {r.Measure.Labs,2}L fill {r.Measure.LabFill:0%} power {r.Measure.Production:0}/{r.Measure.Upkeep:0}{(r.Measure.Overloaded ? " OVERLOADED" : "")}  {r.Layout.Show()}");
        foreach (var (h, i) in hand.Select((h, i) => (h, i)))
        {
            if (!Fits(h, stage, null)) { text.AppendLine($"  hand {i + 1}: doesn't fit the caps: {h.Show()}"); continue; }
            Row($"hand {i + 1}", Judge(h, stage));
        }
        var rng = new Random(5);
        var starts = Enumerable.Range(0, climbs).Select(_ => Seed(stage, null, rng)).Concat(hand.Where(h => Fits(h, stage, null))).ToList();
        var found = starts.AsParallel().Select((p, i) => (From: i < climbs ? "search" : $"from hand {i - climbs + 1}",
            Result: Judge(Climb(p, stage, null, steps, 900 + i).Layout, stage))).ToList();
        foreach (var f in found.OrderByDescending(f => f.Result.Genes)) Row(f.From, f.Result);
        if (Environment.GetEnvironmentVariable("BALANCE_OUT") is { Length: > 0 } path) File.WriteAllText(path, text.ToString());
        Assert.Skip(text.ToString());
    }

    // Every way to put one wildflower into a layout: in place of any part (with what grows beyond it, if it has the
    // links), in before any part (if it has a link onward), or on a free link; those that fit the caps.
    static IEnumerable<Part> Placements(Part layout, Stage stage, NodeKind wild)
    {
        int count = layout.All().Count();
        for (int i = 1; i < count; i++)
        {
            var copy = layout.Copy();
            var p = copy.All().ElementAt(i);
            if (p.Kind == NodeKind.Grove) continue;
            p.Kind = wild;
            if (Fits(copy, stage, wild)) yield return copy;
        }
        if (Links(new Part(wild), stage) > 0)
            for (int i = 1; i < count; i++)
            {
                var copy = layout.Copy();
                var before = copy.All().ElementAt(i);
                var above = before.Up!;
                before.Detach();
                var flower = new Part(wild);
                above.Attach(flower);
                flower.Attach(before);
                if (Fits(copy, stage, wild)) yield return copy;
            }
        for (int i = 0; i < count; i++)
        {
            var copy = layout.Copy();
            var p = copy.All().ElementAt(i);
            if (p.Kids.Count >= Links(p, stage)) continue;
            p.Attach(new Part(wild));
            if (Fits(copy, stage, wild)) yield return copy;
        }
    }

    const string BestGrove = "C(s(s(s(G L) s(L g)) s(s(L g) L)) s(s(s(L L) s(L g)) s(s(L g) L)))";

    // A garden built by rule (Nov 4): the core's two links each lead to a balanced tree of stems (so many on each side),
    // the groves at the deepest tips, the generators one to a stem, the deepest stems first, alternating sides, then as
    // many labs as the power allows, nearest the core first; any link left over stays free.
    static Part Template(Stage stage, int left, int right, int generators) => Template(stage, [left, right], generators);

    static Part Template(Stage stage, int[] split, int generators)
    {
        var core = new Part(NodeKind.Core);
        var sides = new List<List<Part>>();
        foreach (int n in split.Where(n => n > 0))
        {
            var root = new Part(NodeKind.Stem);
            core.Attach(root);
            var stems = new List<Part> { root };
            var queue = new Queue<Part>(stems);
            while (stems.Count < n)
            {
                var at = queue.Peek();
                if (at.Kids.Count >= Links(at, stage)) { queue.Dequeue(); continue; }
                var stem = new Part(NodeKind.Stem);
                at.Attach(stem);
                stems.Add(stem);
                queue.Enqueue(stem);
            }
            sides.Add(stems);
        }
        int Depth(Part p) { int d = 0; for (var at = p; at.Up != null; at = at.Up) d++; return d; }
        bool Free(Part p) => p.Kids.Count < Links(p, stage);
        // (The deepest free stem on a side, preferring one with no generator yet.)
        Part? Deepest(List<Part> side, bool noGenerator) => side.Where(p => Free(p)
            && (!noGenerator || p.Kids.All(k => k.Kind != NodeKind.Generator))).OrderByDescending(Depth).FirstOrDefault();
        for (int i = 0; i < stage.Groves && sides.Count > 0; i++) Deepest(sides[0], false)?.Attach(new Part(NodeKind.Grove));
        for (int i = 0; i < generators; i++)
        {
            var side = sides[i % sides.Count];
            (Deepest(side, true) ?? Deepest(side, false) ?? sides.SelectMany(x => x).FirstOrDefault(Free))?.Attach(new Part(NodeKind.Generator));
        }
        while (core.All().Where(Free).Where(p => p.Kind == NodeKind.Stem).OrderBy(Depth).FirstOrDefault() is { } spot)
        {
            var lab = new Part(NodeKind.Lab);
            spot.Attach(lab);
            if (Overloads(core, stage)) { lab.Detach(); break; }
        }
        return core;
    }

    // The starting gardens for a stage: 0 to 2 stems and 0 to 5 generators fewer than the caps (an evolution that doubles
    // the power made wants far fewer), the stems split evenly or
    // up to 6 more on one side.
    static List<Part> Templates(Stage stage)
    {
        var all = new List<Part>();
        for (int generators = stage.Generators + (stage.Exchange ? 1 : 0); generators >= Math.Max(0, stage.Generators - 5); generators--)
            for (int stems = stage.StemsWith(generators); stems >= Math.Max(2, stage.Stems - 2); stems--)
                foreach (var split in Splits(stems, stage.CoreLinks))
                {
                    var layout = Template(stage, split, generators);
                    if (Fits(layout, stage, null)) all.Add(layout);
                }
        return all;
    }

    // Ways to share stems among the core's links: as evenly as can be, then 2, 4 or 6 moved from the last side to the
    // first.
    static IEnumerable<int[]> Splits(int stems, int links)
    {
        links = Math.Max(1, links);
        for (int lean = 0; lean <= 6; lean += 2)
        {
            var split = Enumerable.Range(0, links).Select(i => stems / links + (i < stems % links ? 1 : 0)).ToArray();
            int move = Math.Min(lean, split[^1] - 1);
            if (lean > 0 && move < lean) yield break;
            split[0] += move;
            split[^1] -= move;
            yield return split;
        }
    }

    // A layout's shape, the same for mirror images (a part's branches in any order): to measure each garden once.
    static string Key(Part p) => p.Kids.Count == 0 ? Short(p.Kind) : $"{Short(p.Kind)}({string.Join(" ", p.Kids.Select(Key).Order())})";

    const NodeKind None = NodeKind.Core; // (the key for no wildflower)
    public sealed record WildResult(Part Layout, double Genes, double Spread, Measure Measure, int Spots);

    // Each wildflower (and none) built into each starting garden (Templates, and those given), in every placement, each
    // garden once (mirror images aside), screened on two seeds over a short time (BALANCE_SCREEN minutes to settle and
    // as many measured, 20 by default), the search run from the best three (BALANCE_STEPS a climb, 1000 by default, also
    // screened), the winner judged on six seeds over the full hour and hour. A wildflower's own earlier gardens (from
    // another stage) go in as they are.
    static Dictionary<NodeKind, WildResult> WildAt(Stage stage, IEnumerable<Part> gardens, IReadOnlyDictionary<NodeKind, List<Part>> earlier)
    {
        int steps = int.TryParse(Environment.GetEnvironmentVariable("BALANCE_STEPS"), out int st) ? st : 1000;
        double screen = double.TryParse(Environment.GetEnvironmentVariable("BALANCE_SCREEN"), out double sc) ? sc : 20;
        var templates = Templates(stage).Concat(gardens.Where(t => Fits(t, stage, null))).ToList();
        var results = new ConcurrentDictionary<NodeKind, WildResult>();
        Parallel.ForEach(Tested.Prepend(None), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, w =>
        {
            NodeKind? wild = w == None ? null : w;
            var spots = (wild is { } k ? templates.SelectMany(t => Placements(t, stage, k)) : templates)
                .Concat(earlier.GetValueOrDefault(w, []).Where(l => Fits(l, stage, wild)))
                .DistinctBy(Key).ToList();
            var ranked = spots.Select(p => (Layout: p, Genes: (Run(p, stage, 1, screen).Score + Run(p, stage, 2, screen).Score) / 2))
                .OrderByDescending(r => r.Genes).ToList();
            var climbed = ranked.Take(3).Select((r, i) => Climb(r.Layout, stage, wild, steps, 300 + i, screen).Layout);
            var best = climbed.Append(ranked[0].Layout).Concat(earlier.GetValueOrDefault(w, []).Where(l => Fits(l, stage, wild)))
                .DistinctBy(Key).Select(l => Judge(l, stage)).MaxBy(r => r.Genes);
            results[w] = new(best.Layout, best.Genes, best.Spread, best.Measure, spots.Count);
        });
        results[None] = BetterBaseline(stage, results[None], Tested.Select(w => (w, results[w].Layout)), steps, screen);
        return new(results);
    }

    // A wildflower's garden without it, every way: its spot taken by its base kind, a lab, a generator or a stem (what
    // grew beyond it kept, if that fits), or it pruned (with what grew beyond it, or that moved to its parent); those
    // that fit and don't overload.
    static IEnumerable<Part> Unwild(Part layout, Stage stage, NodeKind wild, NodeKind? left = null)
    {
        var kinds = new[] { NodeSpecs.All[wild].Wild?.Base, NodeKind.Lab, NodeKind.Generator, NodeKind.Stem }
            .OfType<NodeKind>().Distinct();
        var made = new List<Part>();
        foreach (var kind in kinds)
        {
            var copy = layout.Copy();
            copy.All().First(p => p.Kind == wild).Kind = kind;
            made.Add(copy);
        }
        var pruned = layout.Copy();
        pruned.All().First(p => p.Kind == wild).Detach();
        made.Add(pruned);
        var lifted = layout.Copy();
        var flower = lifted.All().First(p => p.Kind == wild);
        var up = flower.Up!;
        flower.Detach();
        foreach (var kid in flower.Kids.ToList()) up.Attach(kid);
        made.Add(lifted);
        // (left: the wildflower kind still in it, when there were more than one.)
        return made.Where(p => Fits(p, stage, left) && !Overloads(p, stage)).DistinctBy(Key);
    }

    // The baseline given the same chances as the wildflowers (Nov 4: with only the starting gardens it trailed, as each
    // wildflower had a thousand placements to pick from): every wildflower's garden without it (Unwild), screened, the
    // search run from the best three, the winner judged; kept if it beats the baseline found.
    static WildResult BetterBaseline(Stage stage, WildResult current, IEnumerable<(NodeKind Wild, Part Layout)> wilds,
        int steps, double screen)
    {
        var spots = wilds.SelectMany(x => Unwild(x.Layout, stage, x.Wild)).Append(current.Layout).DistinctBy(Key).ToList();
        var ranked = spots.AsParallel().Select(p => (Layout: p, Genes: (Run(p, stage, 1, screen).Score + Run(p, stage, 2, screen).Score) / 2))
            .OrderByDescending(r => r.Genes).ToList();
        var best = ranked.Take(3).AsParallel().Select((r, i) => Climb(r.Layout, stage, null, steps, 700 + i, screen).Layout)
            .Append(ranked[0].Layout).DistinctBy(Key).AsParallel().Select(l => Judge(l, stage)).MaxBy(r => r.Genes);
        return best.Genes > current.Genes ? new(best.Layout, best.Genes, best.Spread, best.Measure, current.Spots + spots.Count) : current;
    }

    // The baselines of an earlier sweep's results (BALANCE_FROM, its output) given the same chances (BetterBaseline),
    // each no worse than one at a stage that fits inside it; the tables again against them.
    [Fact]
    public void SweepBaselines()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("BALANCE") == "1", "a tuning aid, run by hand (BALANCE=1)");
        var lines = File.ReadAllLines(Environment.GetEnvironmentVariable("BALANCE_FROM")!);
        int steps = int.TryParse(Environment.GetEnvironmentVariable("BALANCE_STEPS"), out int st) ? st : 1000;
        double screen = double.TryParse(Environment.GetEnvironmentVariable("BALANCE_SCREEN"), out double sc) ? sc : 20;
        var names = Tested.Prepend(None).ToDictionary(Name);
        var all = new List<(Stage Stage, Dictionary<NodeKind, WildResult> Results)>();
        var text = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length == 0 || char.IsWhiteSpace(lines[i][0]) || !lines[i].Contains(" stems, ") || lines[i].StartsWith("Against")) continue;
            var caps = lines[i][..lines[i].IndexOf(':')].Split('/').Select(int.Parse).ToArray();
            var stage = new Stage(lines[i][..lines[i].IndexOf(':')], caps[0], caps[1], 2, 1);
            var results = new Dictionary<NodeKind, WildResult>();
            for (int j = i + 2; j < lines.Length && lines[j].StartsWith("  "); j++)
            {
                var name = lines[j][2..13].Trim();
                var layout = Parse(lines[j][(lines[j].IndexOf(" C(") + 1)..].Trim());
                double genes = double.Parse(lines[j][13..].Trim().Split(' ')[0]);
                results[names[name]] = new(layout, genes, 0, Run(layout, stage, 1000), 0);
            }
            var at = DateTime.Now;
            results[None] = BetterBaseline(stage, results[None], Tested.Select(w => (w, results[w].Layout)), steps, screen);
            foreach (var (s, r) in all.Where(a => a.Stage.Stems <= stage.Stems && a.Stage.Generators <= stage.Generators))
                foreach (var w in results.Keys.ToList())
                    if (r[w].Genes > results[w].Genes) results[w] = r[w];
            all.Add((stage, results));
            Table(text, stage, results, DateTime.Now - at);
            text.AppendLine();
        }
        foreach (var group in all.Chunk(4))
        {
            text.AppendLine($"Against none, {string.Join(", ", group.Select(g => g.Stage.Name))}, and on average:");
            foreach (var w in Tested.OrderByDescending(w => group.Average(g => g.Results[w].Genes / g.Results[None].Genes)))
            {
                var deltas = group.Select(g => (g.Results[w].Genes / g.Results[None].Genes - 1) * 100).ToList();
                text.AppendLine($"  {Name(w),-11} {string.Join(" ", deltas.Select(d => $"{d,4:+0;-0}%"))}   {deltas.Average(),4:+0;-0}%");
            }
            text.AppendLine($"  {"none",-11} {string.Join(" ", group.Select(g => $"{g.Results[None].Genes,5:0}"))}  genes/h");
        }
        if (Environment.GetEnvironmentVariable("BALANCE_OUT") is { Length: > 0 } path) File.WriteAllText(path, text.ToString());
        Assert.Skip(text.ToString());
    }

    static string Name(NodeKind w) => w == None ? "none" : NodeSpecs.All[w].Name;

    static void Table(StringBuilder text, Stage stage, Dictionary<NodeKind, WildResult> results, TimeSpan took)
    {
        var baseline = results[None];
        text.AppendLine($"{stage.Name}: {stage.Stems} stems, {stage.Generators} generators, {stage.CoreLinks} core links, {stage.Groves} grove ({took.TotalMinutes:0.#} minutes)");
        text.AppendLine($"  {"",-11} {"genes/h",7} {"±",4} {"vs none",7}  {"stems",5} {"gens",4} {"labs",4} {"fill",5} {"power",9}  layout");
        foreach (var (w, r) in results.OrderByDescending(r => r.Value.Genes))
        {
            var m = r.Measure;
            string delta = w == None ? "" : $"{(r.Genes / baseline.Genes - 1) * 100:+0;-0}%";
            text.AppendLine($"  {Name(w),-11} {r.Genes,7:0} {r.Spread,4:0} {delta,7}  {m.Stems,5} {m.Generators,4} {m.Labs,4} {m.LabFill,5:0%} {m.Production,4:0}/{m.Upkeep,-4:0}  {r.Layout.Show()}");
        }
    }

    // One stage (BALANCE_STAGE "stems,generators,core links,groves", 12,4,2,1 by default), with any hand-built gardens
    // (BALANCE_LAYOUTS, split by ';'; else the best found by hand at 12/4/2/1, wherever it fits).
    [Fact]
    public void HandBuiltWild()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("BALANCE") == "1", "a tuning aid, run by hand (BALANCE=1)");
        var caps = (Environment.GetEnvironmentVariable("BALANCE_STAGE") ?? "12,4,2,1").Split(',').Select(int.Parse).ToArray();
        var stage = new Stage("custom", caps[0], caps[1], caps[2], caps.Length > 3 ? caps[3] : 0);
        var started = DateTime.Now;
        var results = WildAt(stage, HandGardens(), new Dictionary<NodeKind, List<Part>>());
        var text = new StringBuilder();
        Table(text, stage, results, DateTime.Now - started);
        if (Environment.GetEnvironmentVariable("BALANCE_OUT") is { Length: > 0 } path) File.WriteAllText(path, text.ToString());
        Assert.Skip(text.ToString());
    }

    static IEnumerable<Part> HandGardens() => (Environment.GetEnvironmentVariable("BALANCE_LAYOUTS") ?? BestGrove)
        .Split(';', StringSplitOptions.RemoveEmptyEntries).Select(t => Parse(t.Trim()));

    // Several stages in order (BALANCE_SWEEP: "stems,generators" pairs split by ';', 2 core links and a grove each;
    // by default day 2's 16/6 and day 3's 19/8, each with a stem more, a generator more, and both), each wildflower's
    // (and the baseline's) best gardens so far starting the search at every later stage they fit. A stage's results are
    // then never below those of a stage that fits inside it (its gardens fit too). Then each wildflower against none at
    // each stage, and on average, for each group of four (a day and its three with more room).
    [Fact]
    public void HandBuiltSweep()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("BALANCE") == "1", "a tuning aid, run by hand (BALANCE=1)");
        var stages = (Environment.GetEnvironmentVariable("BALANCE_SWEEP") ?? "16,6;17,6;16,7;17,7;19,8;20,8;19,9;20,9")
            .Split(';').Select(t => t.Split(',').Select(int.Parse).ToArray())
            .Select(c => new Stage($"{c[0]}/{c[1]}", c[0], c[1], 2, 1)).ToList();
        var earlier = Tested.Prepend(None).ToDictionary(w => w, _ => new List<Part>());
        var all = new List<(Stage Stage, Dictionary<NodeKind, WildResult> Results)>();
        var text = new StringBuilder();
        var started = DateTime.Now;
        foreach (var stage in stages)
        {
            var at = DateTime.Now;
            var results = WildAt(stage, HandGardens().Concat(earlier[None]), earlier);
            // (No worse than a stage that fits inside this one.)
            foreach (var (s, r) in all.Where(a => a.Stage.Stems <= stage.Stems && a.Stage.Generators <= stage.Generators))
                foreach (var w in results.Keys.ToList())
                    if (r[w].Genes > results[w].Genes) results[w] = r[w];
            foreach (var (w, r) in results) earlier[w].Add(r.Layout);
            all.Add((stage, results));
            Table(text, stage, results, DateTime.Now - at);
            text.AppendLine();
            if (Environment.GetEnvironmentVariable("BALANCE_OUT") is { Length: > 0 } partial) File.WriteAllText(partial, text.ToString());
        }
        foreach (var group in all.Chunk(4))
        {
            text.AppendLine($"Against none, {string.Join(", ", group.Select(g => g.Stage.Name))}, and on average:");
            foreach (var w in Tested.OrderByDescending(w => group.Average(g => g.Results[w].Genes / g.Results[None].Genes)))
            {
                var deltas = group.Select(g => (g.Results[w].Genes / g.Results[None].Genes - 1) * 100).ToList();
                text.AppendLine($"  {Name(w),-11} {string.Join(" ", deltas.Select(d => $"{d,4:+0;-0}%"))}   {deltas.Average(),4:+0;-0}%");
            }
            text.AppendLine($"  {"none",-11} {string.Join(" ", group.Select(g => $"{g.Results[None].Genes,5:0}"))}  genes/h");
        }
        text.AppendLine($"({(DateTime.Now - started).TotalMinutes:0.#} minutes)");
        if (Environment.GetEnvironmentVariable("BALANCE_OUT") is { Length: > 0 } path) File.WriteAllText(path, text.ToString());
        Assert.Skip(text.ToString());
    }

    // ==== EVOLUTIONS ==== \\

    // One case of the evolution test: an evolution (or none), and the wildflower its gardens must hold, if any.
    public sealed record EvoCase(string Name, EvolutionKind? Evolution, NodeKind? Wild = null, int WildCount = 1);

    // The evolution test's cases (Jeremy, Nov 4): each evolution's effect alone at level 1, its flower left out but the
    // base slot it would take granted; Wild Bloom with two Lichens in its two slots (the steadiest wildflower);
    // Resonant Frequency with a Buffer for free, as alone it has nothing to strengthen, and a Buffer with no evolution
    // to take out what the Buffer itself adds.
    const string BufferOnly = "Buffer (no evolution)";

    static List<EvoCase> EvoCases() =>
        new List<EvoCase> { new("none", null), new(BufferOnly, null, NodeKind.Buffer) }
        .Concat(Evolutions.All.Values.Where(e => !e.Retired).Select(e => e.Kind switch
        {
            EvolutionKind.WildBloom => new EvoCase(e.Name, e.Kind, NodeKind.Lichen, 2),
            EvolutionKind.ResonantFrequency => new EvoCase(e.Name + " + Buffer", e.Kind, NodeKind.Buffer),
            _ => new EvoCase(e.Name, e.Kind),
        }))
        .ToList();

    // A stage with an evolution bloomed: its caps raised as a bloom raises them (a core link, generators), less the
    // wildflower slot, plus the base slot its flower would take (a stem for a Tendril, a generator for a Pulsar).
    static Stage WithEvolution(Stage stage, EvoCase c)
    {
        var at = stage with { Name = $"{stage.Name} {c.Name}", Evolution = c.Evolution, WildCount = c.Wild == null ? 1 : c.WildCount };
        if (c.Evolution is not { } kind) return at;
        var garden = Garden.New(Layout.Radial, 1);
        garden.Capacity = new Capacity(0, 0, 0, WildFlowers: 0);
        garden.EvolveNow(kind);
        var caps = garden.Capacity;
        return at with { Stems = stage.Stems + caps.Stems, Generators = stage.Generators + caps.Generators, CoreLinks = stage.CoreLinks + caps.CoreLinks };
    }

    // The best garden for a stage and wildflower (or none): the starting gardens, and with a wildflower every placement
    // of it in them (WildCount of them: the best few after each), each garden once, screened on two seeds over a short
    // time, the search run from the best three, the winner judged on six seeds over the full hour and hour.
    static WildResult Solve(Stage stage, NodeKind? wild, IEnumerable<Part> starts, int steps, double screen)
    {
        double Screen(Part p) => (Run(p, stage, 1, screen).Score + Run(p, stage, 2, screen).Score) / 2;
        var bases = starts.Where(t => Fits(t, stage, null)).DistinctBy(Key).ToList();
        var given = starts.Where(t => wild != null && Fits(t, stage, wild)).ToList();
        List<Part> spots = bases;
        if (wild is { } k)
        {
            for (int n = 1; n <= stage.WildCount; n++)
            {
                var placed = spots.SelectMany(t => Placements(t, stage with { WildCount = n }, k)).DistinctBy(Key).ToList();
                spots = n == stage.WildCount ? placed : placed.AsParallel().Select(p => (p, g: Screen(p))).OrderByDescending(x => x.g).Take(6).Select(x => x.p).ToList();
            }
            spots = spots.Concat(given).DistinctBy(Key).ToList();
        }
        var ranked = spots.AsParallel().Select(p => (Layout: p, Genes: Screen(p))).OrderByDescending(r => r.Genes).ToList();
        var climbed = ranked.Take(3).AsParallel().Select((r, i) => Fill(Climb(r.Layout, stage, wild, steps, 300 + i, screen).Layout, stage, wild, screen)).ToList();
        var best = climbed.Append(Fill(ranked[0].Layout, stage, wild, screen)).Concat(given).DistinctBy(Key).AsParallel().Select(l => Judge(l, stage)).MaxBy(r => r.Genes);
        return new(best.Layout, best.Genes, best.Spread, best.Measure, spots.Count);
    }

    // Stems and generators left unused of the caps, after the one exchange if made ("0s 1g (g→s)": a generator's slot
    // went to a stem, and another generator's is still free).
    static string Unused(Stage cap, Measure m)
    {
        int s = cap.Stems - m.Stems, g = cap.Generators - m.Generators;
        if (s < 0) return $"0s {g + s}g (g→s)";
        if (g < 0) return $"{s + g}s 0g (s→g)";
        return $"{s}s {g}g";
    }

    // Evolutions at day 2 (Jeremy, Nov 4: BALANCE_SWEEP, by default 16/6 with a stem more, a generator more and both;
    // 2 core links and a grove; a generator slot left unused may be a stem's): each case's best garden (Solve) against the best with none, the starting gardens shared
    // where they fit (the rule-built ones, the hand-built best, every case's best so far, wildflowers taken out for the
    // baseline), each stage no worse than one that fits inside it; then each evolution against none on average.
    [Fact]
    public void EvolutionSweep()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("BALANCE") == "1", "a tuning aid, run by hand (BALANCE=1)");
        int steps = int.TryParse(Environment.GetEnvironmentVariable("BALANCE_STEPS"), out int st) ? st : 1000;
        double screen = double.TryParse(Environment.GetEnvironmentVariable("BALANCE_SCREEN"), out double sc) ? sc : 20;
        var stages = (Environment.GetEnvironmentVariable("BALANCE_SWEEP") ?? "16,6;17,6;16,7;17,7")
            .Split(';').Select(t => t.Split(',').Select(int.Parse).ToArray())
            .Select(c => new Stage($"{c[0]}/{c[1]}", c[0], c[1], 2, 1, Exchange: true)).ToList();
        // (BALANCE_CASES: only these, split by ';', with none and the Buffer alone.)
        var only = Environment.GetEnvironmentVariable("BALANCE_CASES")?.Split(';').Select(n => n.Trim()).ToHashSet();
        var cases = EvoCases().Where(c => only == null || c.Evolution == null || only.Contains(c.Name)).ToList();
        var found = new List<Part>(); // (every case's best garden so far)
        var all = new List<(Stage Stage, Dictionary<string, WildResult> Results)>();
        var text = new StringBuilder();
        var started = DateTime.Now;
        foreach (var stage in stages)
        {
            var at = DateTime.Now;
            var results = new ConcurrentDictionary<string, WildResult>();
            var capsOf = new Dictionary<string, Stage>();
            foreach (var c in cases)
            {
                var cs = WithEvolution(stage, c);
                capsOf[c.Name] = cs;
                var starts = Templates(cs).Concat(HandGardens()).Concat(found).ToList();
                results[c.Name] = Solve(cs, c.Wild, starts, steps, screen);
            }
            // (The baseline once more, with every case's garden, its wildflowers taken out, to start from.)
            var stripped = cases.Where(c => c.Wild != null).SelectMany(c => c.WildCount > 1
                ? Unwild(results[c.Name].Layout, stage with { WildCount = c.WildCount - 1 }, c.Wild!.Value, c.Wild).SelectMany(p => Unwild(p, stage, c.Wild!.Value))
                : Unwild(results[c.Name].Layout, stage, c.Wild!.Value));
            var again = Solve(stage, null, results.Values.Select(r => r.Layout).Concat(stripped).Append(results["none"].Layout), steps, screen);
            if (again.Genes > results["none"].Genes) results["none"] = again;
            var res = new Dictionary<string, WildResult>(results);
            foreach (var (s2, r) in all.Where(a => a.Stage.Stems <= stage.Stems && a.Stage.Generators <= stage.Generators))
                foreach (var name in res.Keys.ToList())
                    if (r[name].Genes > res[name].Genes) res[name] = r[name];
            found.AddRange(res.Values.Select(r => r.Layout));
            all.Add((stage, res));
            var none = res["none"];
            text.AppendLine($"{stage.Name}: {stage.Stems} stems, {stage.Generators} generators, {stage.CoreLinks} core links, {stage.Groves} grove; each evolution's effect alone ({(DateTime.Now - at).TotalMinutes:0.#} minutes)");
            // (Stems and generators used of the caps, and how many go unused: an evolution may shift what power the
            // garden can use, Jeremy, Nov 4.)
            text.AppendLine($"  {"",-27} {"genes/h",7} {"±",4} {"vs none",7}  {"stems",6} {"gens",5} {"unused (exchanged)",7} {"labs",4} {"links",5} {"fill",5} {"power",9}  layout");
            foreach (var c in cases.OrderByDescending(c => res[c.Name].Genes))
            {
                var r = res[c.Name];
                var m = r.Measure;
                string delta = c.Name == "none" ? "" : $"{(r.Genes / none.Genes - 1) * 100:+0;-0}%";
                var cap = capsOf[c.Name];
                string unused = Unused(cap, m);
                text.AppendLine($"  {c.Name,-27} {r.Genes,7:0} {r.Spread,4:0} {delta,7}  {$"{m.Stems}/{cap.Stems}",6} {$"{m.Generators}/{cap.Generators}",5} {unused,7} {m.Labs,4} {r.Layout.Kids.Count,5} {m.LabFill,5:0%} {m.Production,4:0}/{m.Upkeep,-4:0}  {r.Layout.Show()}");
            }
            text.AppendLine();
            if (Environment.GetEnvironmentVariable("BALANCE_OUT") is { Length: > 0 } partial) File.WriteAllText(partial, text.ToString());
        }
        text.AppendLine($"Against none, {string.Join(", ", all.Select(g => g.Stage.Name))}, and on average:");
        foreach (var c in cases.Where(c => c.Name != "none").OrderByDescending(c => all.Average(g => g.Results[c.Name].Genes / g.Results["none"].Genes)))
        {
            var deltas = all.Select(g => (g.Results[c.Name].Genes / g.Results["none"].Genes - 1) * 100).ToList();
            string unused = string.Join(" ", all.Select(g =>
            {
                var cap = WithEvolution(g.Stage, c);
                return Unused(cap, g.Results[c.Name].Measure).Replace(" ", "");
            }));
            text.AppendLine($"  {c.Name,-27} {string.Join(" ", deltas.Select(d => $"{d,5:+0;-0}%"))}   {deltas.Average(),5:+0;-0}%   unused {unused}");
        }
        text.AppendLine($"  {"none",-27} {string.Join(" ", all.Select(g => $"{g.Results["none"].Genes,6:0}"))}  genes/h");
        // (Resonant Frequency's own share: with its Buffer, against the Buffer with no evolution.)
        if (cases.FirstOrDefault(c => c.Evolution == EvolutionKind.ResonantFrequency) is { } rf)
        {
            var own = all.Select(g => (g.Results[rf.Name].Genes / g.Results[BufferOnly].Genes - 1) * 100).ToList();
            text.AppendLine($"  {"Resonant's own",-27} {string.Join(" ", own.Select(d => $"{d,5:+0;-0}%"))}   {own.Average(),5:+0;-0}%   (with its Buffer, against a Buffer alone)");
        }
        text.AppendLine($"({(DateTime.Now - started).TotalMinutes:0.#} minutes)");
        if (Environment.GetEnvironmentVariable("BALANCE_OUT") is { Length: > 0 } path) File.WriteAllText(path, text.ToString());
        Assert.Skip(text.ToString());
    }

    // For checking the starting gardens: each one, measured.
    [Fact]
    public void BalanceTemplates()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("BALANCE") == "1", "a tuning aid, run by hand (BALANCE=1)");
        var stage = new Stage("custom", 12, 4, 2, 1);
        var text = new StringBuilder();
        foreach (var t in Templates(stage).AsParallel().AsOrdered().Select(t => (t, Run(t, stage, 1))))
            text.AppendLine($"{t.Item2.Score,6:0} {t.Item2.Stems}s {t.Item2.Generators}g {t.Item2.Labs}L {t.Item2.Production:0}/{t.Item2.Upkeep:0}  {t.t.Show()}");
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
