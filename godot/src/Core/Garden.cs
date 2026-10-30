using System.Numerics;

namespace SpaceGarden.Core;

public enum Layout
{
    Radial,  // core in the middle, branches spreading every way, the whole plant floating free: the TS game's
             // space plant, and the one in use
    Upright, // core planted (fixed) at the bottom, the plant pulled upward and swaying (shelved: it forces the physics)
}

// The whole garden: its parts, the force-directed physics that holds them together (ported from the TS game's
// FDGContainer), the motes of light flowing through them, and growth over real time.
//
// Two clocks: Step() is one 60 Hz physics tick and only moves things; Grow() takes real seconds and runs the
// economy (power and upkeep) and growth on them. Speeding growth up (or catching up time away) never changes
// how the plant moves.
public sealed class Garden
{
    public readonly List<PlantNode> Nodes = new();
    public readonly List<Link> Links = new();
    public readonly List<Mote> Motes = new();
    // Branches just pruned, drifting loose for a few seconds before they go (see DriftCut).
    public readonly List<CutBranch> Cut = new();
    // The shaping tools placed (see Shaping.cs).
    public readonly List<ShapingTool> Shaping = new();
    public readonly Layout Layout;
    public PlantNode Core => Nodes[0];
    // Only an upright plant's core is planted; a radial one floats like any other part.
    public bool Planted => Layout == Layout.Upright;

    // Seconds of motion (drives the wind), and real seconds not yet run through the economy (less than one
    // EconomyStepSeconds).
    public double Time;
    public double EconomyClock;

    // The limits on growth (see Capacity). Raising CoreLinks gives the core room for another branch.
    public Capacity Capacity
    {
        get => capacity;
        set
        {
            capacity = value;
            if (Nodes.Count > 0) Core.MaxLinks = value.CoreLinks;
        }
    }
    Capacity capacity = Capacity.Start;

    // Genes that have reached the core over the garden's life (they never drop).
    public double Genes;
    // The genes left to spend (DESIGN.md, Upgrade tracks): every gene reaching the core adds to it as to Genes, and
    // only spending takes from it (see Spend, and Buy, which the Research panel calls).
    public double GeneBalance;
    // How many of each track's upgrades have been bought (see Buy). The view compares their sum with what it has
    // shown, to ripple the core.
    public int StemsBought, GeneratorsBought, CoreBought;
    public int Bought => StemsBought + GeneratorsBought + CoreBought;

    // Its lifetime totals, history and journal (see Tracking).
    public Tracking Tracking { get; private set; } = new();

    // Off, blobs still do their work but aren't shown travelling (a catch-up, which draws nothing).
    public bool ShowBlobs = true;

    // Off, growth blobs still grow fruit but start no passive buds (tests, and later a "hold growth" choice).
    public bool PassiveGrowth = true;

    // The part the player is holding (see Grab), and where the pointer is pulling it.
    public PlantNode? Held { get; private set; }
    public Vector2 HoldTarget;

    // How far the fastest part moved in the last tick, in garden units: the widget runs at full frame rate while
    // this is noticeable.
    public float Speed { get; private set; }

    // Walls the plant bounces off (the TS game's borders), or none.
    public (Vector2 Min, Vector2 Max)? Borders;

    // Fruit stays out of the grid and borrows its parent's push (see Repel): about 4x cheaper, nearly as tidy.
    public bool FruitBorrowsPush = true;

    readonly Random rng;
    readonly Dictionary<PlantNode, Link> linkToParent = new();
    readonly Dictionary<(int X, int Y), Cell> cells = new();
    readonly List<((int X, int Y) Key, Cell Cell)> occupied = new();
    readonly List<PlantNode> farReaching = new();
    readonly List<PlantNode> sending = new(); // flowers with a blob ready, this economy step
    readonly List<PlantNode> budding = new(); // parts whose bud grew whole on its own, this economy step
    readonly List<PlantNode> ending = new(); // wild flowers whose life cycle ends this economy step
    readonly List<(PlantNode From, PlantNode To, float Power)> transfers = new(); // clumps of power moving, this economy step
    readonly Random looks = new(); // for what's only shown (when a flash shows), so the garden's own dice match a catch-up
    long ticks;
    int nextId;
    bool loading; // parts added by FromSave didn't grow just now

    public Garden(Layout layout, int? seed = null)
    {
        Layout = layout;
        rng = seed is { } s ? new Random(s) : new Random();
    }

    public static Garden New(Layout layout, int? seed = null)
    {
        var garden = new Garden(layout, seed);
        var core = garden.Add(NodeSpecs.All[NodeKind.Core], null, Vector2.Zero);
        core.Fixed = garden.Planted;
        core.Age = 10;
        core.Power = core.Store;
        core.FillLook = 1;
        garden.Tracking.Log(JournalKind.Planted, "A new garden was planted");
        garden.Tracking.Discovered[NodeKind.Core] = 0;
        return garden;
    }

    // ==== GROWTH ==== \\

    // Spends real seconds on the economy and growth, in fixed steps; returns how many parts grew (fruit too). However
    // the time is chunked (a frame at a time, or three days at once on return), the same steps run.
    public int Grow(double seconds)
    {
        EconomyClock += seconds;
        int grew = 0;
        while (EconomyClock >= Tuning.EconomyStepSeconds)
        {
            EconomyClock -= Tuning.EconomyStepSeconds;
            grew += EconomyStep(Tuning.EconomyStepSeconds);
        }
        return grew;
    }

    // Time away: grow for it (capped), then let the physics settle so the plant isn't shown mid-tangle.
    // A prune can't be undone after time away (the window has passed).
    public int CatchUp(double secondsAway)
    {
        ForgetPrune();
        bool show = ShowBlobs;
        ShowBlobs = false;
        int sprouted = Grow(Math.Clamp(secondsAway, 0, Tuning.CatchUpCapSeconds));
        ShowBlobs = show;
        SettleLooks();
        if (sprouted > 0)
        {
            for (int i = 0; i < Tuning.SettleTicks; i++) Step();
            foreach (var node in Nodes) node.Age = Math.Max(node.Age, 10);
        }
        return sprouted;
    }

    // A new part somewhere on the plant, for free (the first stem, stress tests), or null if there's no room: a
    // branch from the core while it has a free link, else fruit FruitChance of the time, or a passive branch.
    public PlantNode? Sprout()
    {
        if (Nodes.Count >= Tuning.MaxNodes) return null;
        var counts = CountKinds();
        var branchable = Nodes.Where(n => BranchKinds(n, counts).Count > 0).ToList();
        var fruitable = Nodes.Where(n => n.CanFruit).ToList();
        bool fruit = false;
        PlantNode parent = Core;
        if (!branchable.Contains(Core))
        {
            fruit = branchable.Count == 0 || (fruitable.Count > 0 && rng.NextDouble() < Tuning.FruitChance);
            if (fruit && fruitable.Count == 0) return null;
            parent = Pick(fruit ? fruitable : branchable);
        }
        return fruit ? AddFruit(parent) : AddBranch(parent, Roll(BranchKinds(parent, counts)));
    }

    // A bud the player asked for (DESIGN.md: "Manual buds cost nothing"): a bud of the chosen kind starts forming in
    // the part (or the part a fruit hangs from) at once, if it has a free link and the caps have room, holding its
    // link and cap slot from now on. It grows on its own like any bud (see GrowBuds) and growth blobs hurry it. A
    // fruit forming there gives way to it (the player's pick over the passive one); a bud already forming there
    // doesn't. Never fruit: only growth blobs grow it. Groves start only this way. Like any new bud it may grow wild
    // (see GrowsWild), at the planted chance. Returns whether the bud started.
    public bool SproutFrom(PlantNode node, NodeKind kind)
    {
        if (node.Spec.IsFruit && node.Parent != null) node = node.Parent;
        if (!CanSproutFrom(node, kind)) return false;
        node.Forming = NodeSpecs.All[kind].IsWild ? kind : GrowsWild(node, kind, passive: false, CountKinds());
        node.Growth = 0;
        node.FormingLook = 0;
        return true;
    }

    public bool CanSproutFrom(PlantNode node, NodeKind kind)
    {
        if (node.Spec.IsFruit && node.Parent != null) node = node.Parent;
        var spec = NodeSpecs.All[kind];
        return !spec.IsFruit && kind != NodeKind.Core && node.CanBranch && !node.FormingBud
            && Nodes.Count < Tuning.MaxNodes && HasRoomFor(kind, CountKinds());
    }

    // A branch of the chosen kind, grown at once, where SproutFrom would start one: for tests and dev aids (the
    // player's buds form).
    public PlantNode? GrowBranch(PlantNode node, NodeKind kind)
    {
        if (node.Spec.IsFruit && node.Parent != null) node = node.Parent;
        return CanSproutFrom(node, kind) ? AddBranch(node, kind) : null;
    }

    // A fruit of the part's own kind, if it has room: for tests only (players can't grow fruit; growth blobs do).
    internal PlantNode? GrowFruit(PlantNode node) => node.CanFruit && Nodes.Count < Tuning.MaxNodes ? AddFruit(node) : null;

    // Deeper parts are likelier, so the plant reaches outward instead of thickening at the base.
    PlantNode Pick(List<PlantNode> candidates)
    {
        double total = candidates.Sum(n => n.Depth + 1.0);
        double roll = rng.NextDouble() * total;
        foreach (var n in candidates)
        {
            roll -= n.Depth + 1;
            if (roll <= 0) return n;
        }
        return candidates[^1];
    }

    // Cuts a part off with everything that grew from it; returns what was cut. The core can't be pruned. The cut
    // branch leaves the garden at once (its power, caps and blobs), but its parts, every link between them cut too,
    // drift loose for a while as the plant's push sends them off (see DriftCut).
    public List<PlantNode> Prune(PlantNode node)
    {
        if (node.Parent == null) return new();
        var removed = Detach(node);
        lastPrune = (removed, removed.Select(n => n.Position - node.Parent.Position).ToList());
        Tracking.Pruned += removed.Count;
        UndoSecondsLeft = Tuning.UndoPruneSeconds;
        return removed;
    }

    // Takes a part and everything beyond it out of the garden, drifting loose (a prune, or a wild flower ending).
    List<PlantNode> Detach(PlantNode node)
    {
        var removed = new List<PlantNode>();
        void Collect(PlantNode n)
        {
            removed.Add(n);
            foreach (var child in n.Neighbours().Where(c => c.Parent == n)) Collect(child);
        }
        Collect(node);
        var gone = removed.ToHashSet();

        (node.Spec.IsFruit ? node.Parent!.Fruits : node.Parent!.Branches).Remove(node);
        Nodes.RemoveAll(gone.Contains);
        Cut.Add(new CutBranch(removed));
        Links.RemoveAll(l => gone.Contains(l.Target));
        Motes.RemoveAll(m => m.Route.Any(gone.Contains));
        foreach (var n in removed) linkToParent.Remove(n);
        if (Held != null && gone.Contains(Held)) LetGo();
        return removed;
    }

    // The last prune, while it can still be undone (see Tuning.UndoPruneSeconds): what was cut, and where each part
    // was then from its parent. Only the last one: a new prune replaces it.
    (List<PlantNode> Nodes, List<Vector2> FromParent)? lastPrune;
    public double UndoSecondsLeft { get; private set; }

    // Whether the last prune can still be put back: inside the window, its parent still there with a free link (or
    // fruit slot), and the caps and part limit still with room for it (a bud may have taken the room since).
    public bool CanUndoPrune
    {
        get
        {
            if (lastPrune is not var (nodes, _)) return false;
            var root = nodes[0];
            var parent = root.Parent!;
            if (!Nodes.Contains(parent) || Nodes.Count + nodes.Count > Tuning.MaxNodes) return false;
            if (root.Spec.IsFruit ? parent.Fruits.Count >= parent.Spec.MaxFruits : !parent.CanBranch) return false;
            var counts = CountKinds();
            var (stems, generators, wild) = (counts.Stems, counts.Generators, counts.Wild);
            stems += nodes.Count(n => n.Spec.Kind == NodeKind.Stem);
            generators += nodes.Count(n => n.Spec.Kind == NodeKind.Generator);
            wild += nodes.Count(n => n.Spec.IsWild);
            return stems <= Capacity.Stems && generators <= Capacity.Generators && wild <= Capacity.WildFlowers
                && nodes.Where(n => n.Spec.IsWild).GroupBy(n => n.Spec.Kind)
                    .All(g => KindCap(g.Key) is not { } cap || CountOf(g.Key) + g.Count() <= cap);
        }
    }

    // Puts the last prune back as it was (power, pins, fruit and all), moved along with its parent if the plant has
    // drifted since, and growing in again. Returns what came back, or null if it can't (see CanUndoPrune).
    public List<PlantNode>? UndoPrune()
    {
        if (!CanUndoPrune) return null;
        var (nodes, fromParent) = lastPrune!.Value;
        ForgetPrune();
        Tracking.Pruned -= nodes.Count; // never cut after all
        var root = nodes[0];
        Cut.RemoveAll(c => c.Nodes[0] == root); // if it's still drifting
        (root.Spec.IsFruit ? root.Parent!.Fruits : root.Parent!.Branches).Add(root);
        // Cut in growth order (a part before what grew from it), so they go back in that order, where they were.
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            node.Position = root.Parent.Position + fromParent[i];
            node.PreviousPosition = node.Position;
            node.Velocity = Vector2.Zero;
            node.Age = 0;
            // Still tied, unless its tool has gone since, or its pot has taken another part.
            if (node.TiedTo is { } tool && (!Shaping.Contains(tool) || (tool.Kind == ShapingKind.Pot && Potted(tool) != null)))
                node.TiedTo = null;
            Nodes.Add(node);
            var link = new Link(node.Parent!, node);
            Links.Add(link);
            linkToParent[node] = link;
        }
        return nodes;
    }

    // Closes the undo window now (time away; a simulation that runs no physics ticks to count it down).
    public void ForgetPrune()
    {
        lastPrune = null;
        UndoSecondsLeft = 0;
    }

    // Holding a part pulls it to the pointer and the links drag the rest along at its real weight. Letting go just
    // stops the pull: everything keeps the momentum it has and drifts to rest, as in the TS game. (An upright
    // plant's core is uprooted while held and planted again, stopping dead, where it's let go.)
    public void Grab(PlantNode node)
    {
        Held = node;
        HoldTarget = node.Position;
        if (Planted) Core.Fixed = false;
    }

    public void LetGo()
    {
        Held = null;
        if (!Planted) return;
        Core.Fixed = true;
        Core.Velocity = Vector2.Zero;
    }

    // Moves the whole garden by offset (to bring a plant back onto a screen it's no longer on).
    public void Translate(Vector2 offset)
    {
        foreach (var node in Nodes.Concat(Cut.SelectMany(c => c.Nodes)))
        {
            node.Position += offset;
            node.PreviousPosition += offset;
        }
        foreach (var tool in Shaping) tool.Position += offset;
        HoldTarget += offset;
    }

    // ==== SHAPING ==== \\

    // Whether there's room under the caps for another tool of this kind.
    public bool CanPlace(ShapingKind kind) => Shaping.Count(t => t.Kind == kind) < Capacity.Of(kind);

    // A new tool here (a pot facing angle, a trellis along it), or null if the caps have no room for it.
    public ShapingTool? Place(ShapingKind kind, Vector2 at, float angle = -MathF.PI / 2)
    {
        if (!CanPlace(kind)) return null;
        var tool = new ShapingTool(kind, at, kind == ShapingKind.Trellis ? 0 : angle);
        Shaping.Add(tool);
        Tracking.First($"placed:{kind}", $"First {ToolName(kind)} placed");
        return tool;
    }

    // Takes a tool away, letting go of what it held.
    public void Remove(ShapingTool tool)
    {
        Shaping.Remove(tool);
        foreach (var node in Nodes.Concat(Cut.SelectMany(c => c.Nodes)))
            if (node.TiedTo == tool) node.TiedTo = null;
    }

    // Ties a part to a pot (which lets go of the part it held: a pot holds one) or a trellis. A well holds nothing, and
    // fruit can't be tied. Returns whether it was.
    public bool Tie(PlantNode node, ShapingTool tool)
    {
        if (tool.Kind == ShapingKind.Well || node.Spec.IsFruit || !Shaping.Contains(tool)) return false;
        if (tool.Kind == ShapingKind.Pot)
            foreach (var other in Nodes)
                if (other.TiedTo == tool) other.TiedTo = null;
        node.TiedTo = tool;
        node.Velocity = Vector2.Zero;
        return true;
    }

    public void Untie(PlantNode node) => node.TiedTo = null;

    // An empty pot let go over a part plants it: the part nearest the pot's centre within its radius and slop (not
    // fruit) is tied to it. Returns the part, or null (not an empty pot, or nothing under it).
    public PlantNode? PlantUnder(ShapingTool pot, float slop)
    {
        if (pot.Kind != ShapingKind.Pot || Potted(pot) != null || NodeAt(pot.Position, slop) is not { } node) return null;
        return Tie(node, pot) ? node : null;
    }

    // What a pot holds, if anything.
    public PlantNode? Potted(ShapingTool pot) => Nodes.FirstOrDefault(n => n.TiedTo == pot);

    // The radius of the part a pot holds (or EmptyPot's, empty): the pot is drawn and caught just outside it.
    public float PotRadius(ShapingTool pot) => Potted(pot)?.Spec.Radius ?? ShapingTool.EmptyPot;

    // The tool under the pointer, and which bit of it: a handle (a pot's knob, a trellis's end) wherever one is within
    // its reach, as they're small and sit on or by their tool's body; else the body nearest the pointer.
    public (ShapingTool Tool, ShapingHandle Handle)? ShapingAt(Vector2 point, float slop)
    {
        (ShapingTool, ShapingHandle)? handle = null, body = null;
        float handleDist = float.MaxValue, bodyDist = float.MaxValue;
        void Consider(ShapingTool tool, ShapingHandle part, Vector2 at, float reach)
        {
            float dist = Vector2.Distance(point, at);
            bool isBody = part == ShapingHandle.Body;
            if (dist > reach + slop || dist >= (isBody ? bodyDist : handleDist)) return;
            if (isBody) (body, bodyDist) = ((tool, part), dist);
            else (handle, handleDist) = ((tool, part), dist);
        }
        foreach (var tool in Shaping)
        {
            switch (tool.Kind)
            {
                case ShapingKind.Pot:
                    float r = PotRadius(tool);
                    Consider(tool, ShapingHandle.Turn, tool.Position + tool.Facing * (r + ShapingTool.PotKnob), ShapingTool.HandleReach);
                    Consider(tool, ShapingHandle.Body, tool.Position - tool.Facing * (r / 2),
                        r + ShapingTool.PotGap + ShapingTool.PotLip);
                    break;
                case ShapingKind.Trellis:
                    var (a, b) = tool.Ends;
                    Consider(tool, ShapingHandle.EndA, a, ShapingTool.HandleReach);
                    Consider(tool, ShapingHandle.EndB, b, ShapingTool.HandleReach);
                    Consider(tool, ShapingHandle.Body, tool.OnLine(point), 6);
                    break;
                case ShapingKind.Well:
                    Consider(tool, ShapingHandle.Body, tool.Position, ShapingTool.WellRadius);
                    break;
            }
        }
        return handle ?? body;
    }

    // The tools' pushes and pulls, before the parts move (DESIGN.md, Shaping the garden):
    //   - each main part linked to a potted one (growing straight out of it, or its parent) is pushed the way the pot
    //     faces (its Lean, summed if it's next to more than one pot), as a force: a stem feels all of it, the heavy core
    //     a thirtieth. Parts further away only follow through their links;
    //   - a well pulls every part towards it, the core and fruit too, alike whatever their weight, like gravity: a free
    //     plant drifts whole to the well and settles round it; one held back (its core in a pot) hangs towards it.
    // (Pots and trellises hold their parts after they move: see Hold.)
    void Shape()
    {
        if (Shaping.Count == 0)
        {
            foreach (var node in Nodes) node.Lean = Vector2.Zero;
            return;
        }
        foreach (var node in Nodes) node.Lean = Vector2.Zero;
        foreach (var node in Nodes)
        {
            if (node.TiedTo is not { Kind: ShapingKind.Pot } pot) continue;
            if (node.Parent != null) node.Parent.Lean += pot.Facing;
            foreach (var branch in node.Branches) branch.Lean += pot.Facing;
        }
        foreach (var node in Nodes)
            if (node.TiedTo == null && !node.Spec.IsFruit) node.Velocity += node.Lean * Tuning.PotPush * node.Spec.InverseMass;

        foreach (var well in Shaping)
        {
            if (well.Kind != ShapingKind.Well) continue;
            foreach (var node in Nodes)
            {
                var d = well.Position - node.Position;
                float dist = d.Length();
                if (dist < 0.5f) continue;
                node.Velocity += d / dist * (Tuning.WellPull * Math.Min(1, dist / Tuning.WellEase));
            }
        }
    }

    // A potted part is drawn to its pot and held there; a part on a trellis is held on its line, free to slide along it.
    void Hold(PlantNode node, ShapingTool tool)
    {
        if (tool.Kind == ShapingKind.Pot)
        {
            node.Position = Vector2.Lerp(node.Position, tool.Position, Tuning.PotFollow);
            node.Velocity = Vector2.Zero;
            return;
        }
        node.Position = tool.OnLine(node.Position);
        var along = tool.Facing;
        node.Velocity = along * Vector2.Dot(node.Velocity, along) * Tuning.TrellisSlide;
    }

    // How many stems, generators and wild flowers there are, counting the buds forming (they hold their slot), for the
    // caps. A wild flower takes a wild flower slot, never its base kind's.
    readonly record struct Counts(int Stems, int Generators, int Wild)
    {
        // With one more of this kind (a bud starting).
        public Counts With(NodeKind kind) => NodeSpecs.All[kind].IsWild ? this with { Wild = Wild + 1 } : kind switch
        {
            NodeKind.Stem => this with { Stems = Stems + 1 },
            NodeKind.Generator => this with { Generators = Generators + 1 },
            _ => this,
        };
    }

    Counts CountKinds()
    {
        int stems = 0, generators = 0, wild = 0;
        void Count(NodeKind kind)
        {
            if (kind == NodeKind.Stem) stems++;
            else if (kind == NodeKind.Generator) generators++;
            else if (NodeSpecs.All[kind].IsWild) wild++;
        }
        foreach (var n in Nodes)
        {
            Count(n.Spec.Kind);
            if (n.FormingBud) Count(n.Forming!.Value);
        }
        return new(stems, generators, wild);
    }

    // Stems and generators are capped; labs and groves only by free links; wild flowers by the wild flower limit (and
    // whether their kind can grow yet: see CanGrowWild).
    bool HasRoomFor(NodeKind kind, Counts counts) => NodeSpecs.All[kind].IsWild
        ? counts.Wild < Capacity.WildFlowers && CanGrowWild(kind)
        : kind switch
        {
            NodeKind.Stem => counts.Stems < Capacity.Stems,
            NodeKind.Generator => counts.Generators < Capacity.Generators,
            _ => true,
        };

    // The kinds of passive branch this part could bud now, with their odds: near the core only stems, further out also
    // generators, labs and groves. (A garden with no grove sends no growth blobs, so it never grows its first one
    // alone: the player plants it.) A stem next to the core takes flowers too once no stem can grow there, so its free link isn't left empty for good
    // when the stem cap is reached. (The core itself only ever grows stems.)
    List<(NodeKind Kind, double Weight)> BranchKinds(PlantNode parent, Counts counts)
    {
        var kinds = new List<(NodeKind, double)>();
        if (!parent.CanBranch) return kinds;
        void Consider((NodeKind, double)[] odds)
        {
            foreach (var (kind, weight) in odds)
                if (HasRoomFor(kind, counts)) kinds.Add((kind, weight));
        }
        Consider(parent.Depth < 2 ? NearOdds : FarOdds);
        if (kinds.Count == 0 && parent.Depth == 1) Consider(FarOdds);
        return kinds;
    }

    static readonly (NodeKind, double)[] NearOdds = [(NodeKind.Stem, 1)];
    static readonly (NodeKind, double)[] FarOdds =
        [(NodeKind.Stem, 0.64), (NodeKind.Generator, 0.14), (NodeKind.Lab, 0.12), (NodeKind.Grove, 0.1)];

    NodeKind Roll(List<(NodeKind Kind, double Weight)> kinds)
    {
        double roll = rng.NextDouble() * kinds.Sum(k => k.Weight);
        foreach (var (kind, weight) in kinds)
            if ((roll -= weight) < 0) return kind;
        return kinds[^1].Kind;
    }

    // ==== WILD FLOWERS ==== \\

    // How many of each capped wild flower kind may grow (DESIGN.md, Wild flowers: only Big Stem, Pulsar and Twin Star,
    // the Evolution kinds): none until its evolution unlocks it (see UnlockWild), +WildUnlockCap each time.
    public readonly Dictionary<NodeKind, int> WildCaps = new();

    // The wild flowers growing and forming, against the limit.
    public int WildCount => CountKinds().Wild;
    public int FreeWildSlots => Math.Max(0, Capacity.WildFlowers - WildCount);

    // The cap on a wild flower kind (null: uncapped, the Start kinds).
    public int? KindCap(NodeKind kind) =>
        NodeSpecs.All[kind].Wild is { Unlock: WildUnlock.Evolution } ? WildCaps.GetValueOrDefault(kind) : null;

    // Whether a wild flower kind can grow now: a Start kind always (within the limit), an Evolution kind while its cap
    // has room.
    public bool CanGrowWild(NodeKind kind) => KindCap(kind) is not { } cap || CountOf(kind) < cap;

    // Its evolution was taken (step 5c calls this as it blooms): the kind may grow wild, up to WildUnlockCap more.
    public void UnlockWild(NodeKind kind)
    {
        if (NodeSpecs.All[kind].Wild is not { Unlock: WildUnlock.Evolution }) return;
        WildCaps[kind] = WildCaps.GetValueOrDefault(kind) + Tuning.WildUnlockCap;
    }

    // Parts of a kind, and buds forming of it.
    int CountOf(NodeKind kind) => Nodes.Count(n => n.Spec.Kind == kind || n.Forming == kind);

    // A new bud may grow wild (DESIGN.md, Wild flowers): the chance is the passive or planted one per free wild flower
    // slot (none free, no chance, and no dice thrown, so a garden without slots grows exactly as before), raised near a
    // Moonflower. If it does, its kind is one of those that can stand in for its base kind (and, for a passive bud, a
    // passive-only kind), alike, among those that can grow now. Returns the bud's kind.
    NodeKind GrowsWild(PlantNode parent, NodeKind kind, bool passive, Counts counts)
    {
        int free = Capacity.WildFlowers - counts.Wild;
        if (free <= 0) return kind;
        double chance = (passive ? Tuning.PassiveWildChance : Tuning.PlantedWildChance) * free * WildBoostAt(parent);
        if (rng.NextDouble() >= chance) return kind;
        var pool = NodeSpecs.Wild.Where(s => (s.Wild!.Base == kind || (passive && s.Wild.PassiveOnly)) && CanGrowWild(s.Kind))
            .Select(s => s.Kind).ToList();
        return pool.Count == 0 ? kind : pool[rng.Next(pool.Count)];
    }

    // The same for tests: what a bud of this kind starting on parent now would be.
    internal NodeKind RollBud(PlantNode parent, NodeKind kind, bool passive) => GrowsWild(parent, kind, passive, CountKinds());

    // How much likelier a bud on this part is to grow wild: each wild flower with a WildBoost (a Moonflower) within
    // Tuning.WildBoostReach links adds its boost.
    float WildBoostAt(PlantNode parent)
    {
        float boost = 1;
        if (!Nodes.Any(n => n.Spec.WildBoost > 0)) return boost;
        var seen = new HashSet<PlantNode> { parent };
        var ring = new List<PlantNode> { parent };
        for (int step = 0; step <= Tuning.WildBoostReach && ring.Count > 0; step++)
        {
            var next = new List<PlantNode>();
            foreach (var n in ring)
            {
                boost += n.Spec.WildBoost;
                if (n.Parent != null && seen.Add(n.Parent)) next.Add(n.Parent);
                foreach (var b in n.Branches)
                    if (seen.Add(b)) next.Add(b);
            }
            ring = next;
        }
        return boost;
    }

    // Dev aid (--wild=KIND) and screenshots: a wild flower of this kind, grown at once on the deepest part with a free
    // link (or on this one), whatever its lock; the limit is raised to fit it. Null if no part has room.
    public PlantNode? GrowWild(NodeKind kind, PlantNode? on = null)
    {
        if (!NodeSpecs.All[kind].IsWild || Nodes.Count >= Tuning.MaxNodes) return null;
        var parent = on ?? Nodes.Where(n => n.CanBranch && !n.Spec.IsFruit).MaxBy(n => n.Depth);
        if (parent == null || !parent.CanBranch) return null;
        if (FreeWildSlots == 0) Capacity = Capacity with { WildFlowers = WildCount + 1 };
        return AddBranch(parent, kind);
    }

    // A wild flower's own life cycle ends it (DESIGN.md, Wild flowers): the one exception to "nothing is destroyed",
    // never neglect. It leaves like a pruned branch, drifting off and fading, and frees its slot, but it's no prune
    // (nothing to undo or count as pruned): the journal and the away note say so.
    void End(PlantNode node)
    {
        Detach(node);
        Tracking.Ended(node.Spec);
    }

    // A new part starts empty and fills from its parent.
    PlantNode AddBranch(PlantNode parent, NodeKind kind, bool emerge = false) =>
        AddNear(NodeSpecs.All[kind], parent, emerge);

    PlantNode AddFruit(PlantNode parent) => AddNear(NodeSpecs.All[parent.Spec.Fruit!.Value], parent);

    // Places a new part just off its parent, heading away from the grandparent (and upward when upright), and the way
    // a pot pushes there, if one does (passive growth respects pots). A bud that formed inside its parent (emerge)
    // starts deep inside it and pops out, its link extending.
    PlantNode AddNear(NodeSpec spec, PlantNode parent, bool emerge = false)
    {
        Vector2 away = parent.Parent == null
            ? (Layout == Layout.Upright ? -Vector2.UnitY : RandomDirection())
            : Vector2.Normalize(parent.Position - parent.Parent.Position + new Vector2(0.001f, 0));
        var lean = parent.TiedTo is { Kind: ShapingKind.Pot } pot ? pot.Facing : Vector2.Zero;
        if (lean != Vector2.Zero) away = Vector2.Normalize(away + lean + new Vector2(0.001f, 0));
        if (Layout == Layout.Upright) away = Vector2.Normalize(away - 0.6f * Vector2.UnitY);
        float angle = (float)(rng.NextDouble() - 0.5) * 1.6f;
        var dir = Vector2.Transform(away, Matrix3x2.CreateRotation(angle));
        float distance = (parent.Spec.Radius + spec.Radius) * (emerge ? Tuning.EmergeStart : 0.8f);
        var node = Add(spec, parent, parent.Position + dir * distance);
        node.Velocity = dir * (emerge ? Tuning.EmergeSpeed : 0.3f);
        return node;
    }

    PlantNode Add(NodeSpec spec, PlantNode? parent, Vector2 position)
    {
        var node = new PlantNode(nextId++, spec, parent, position)
        {
            Rotation = (float)(rng.NextDouble() * Math.Tau - Math.PI),
            Spin = Tuning.SpinSpeed * (rng.Next(2) == 0 ? 1 : -1),
        };
        if (parent == null) node.MaxLinks = Capacity.CoreLinks;
        Nodes.Add(node);
        if (parent != null && !loading) Tracking.Grew(spec, Nodes.Count);
        // A Comet Bloom bursts every kind of blob as it appears.
        if (spec.EmergeBurst > 0 && !loading) node.Blob = spec.EmergeBurst;
        if (!spec.IsFruit && !loading)
        {
            Tracking.Reached(Nodes.Count(n => !n.Spec.IsFruit));
            GainTools(log: true);
        }
        if (parent != null)
        {
            (spec.IsFruit ? parent.Fruits : parent.Branches).Add(node);
            var link = new Link(parent, node);
            Links.Add(link);
            linkToParent[node] = link;
        }
        return node;
    }

    Vector2 RandomDirection()
    {
        double a = rng.NextDouble() * Math.Tau;
        return new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
    }

    // ==== ECONOMY ==== \\

    // One fixed step of the economy (DESIGN.md, Economy), as the original's PlantNodePower:
    //   - the core and generators make power into their own store, in proportion to their fill (none once full);
    //   - every other part pays upkeep in proportion to its fill, so it never quite empties: nothing wilts;
    //   - flowers make blobs in proportion to their fill: labs gene blobs, groves growth blobs, each sent off on its
    //     walk as it's made (see SendGenes, SendGrowth);
    //   - every bud forming grows a little on its own, in proportion to its part's fill (see GrowBuds);
    //   - each part fuller than SendFill sends a clump of power to its emptiest neighbour that's less full than it
    //     (see SendTo). Clumps, not a smooth flow, so the fills never settle: they rock around a moving balance, and
    //     a branch far from the sources runs lower (there is no global pool).
    // Nothing checks whether the garden can afford what grows: that's the player's to judge (prune what drains it).
    // Returns how many parts grew.
    int EconomyStep(double seconds)
    {
        float dt = (float)seconds, hours = (float)(seconds / 3600);
        float stored = 0, production = 0, upkeep = 0; // for the history, as the step starts
        foreach (var node in Nodes)
        {
            if (node.Spec.IsFruit) continue;
            float made = ProductionPerSecond(node), cost = UpkeepPerSecond(node);
            stored += node.Power;
            production += made;
            upkeep += cost;
            node.Power = Math.Max(0, node.Power + (made - cost) * dt);
            node.Blob += BlobsPerHour(node) * hours;
            node.Life += seconds;
            node.Buff = Math.Max(0, node.Buff - dt);
            if (node.Spec.LifeSeconds > 0 && node.Life >= node.Spec.LifeSeconds) ending.Add(node);
        }
        if (ending.Count > 0)
        {
            foreach (var node in ending) End(node);
            ending.Clear();
        }

        // Blobs leave after the loop over the parts, as what they grow joins them. A blob leaves even when nothing
        // will absorb it: it just wanders and fades (pretty to watch). A flower's blobs leave once BlobBurst of them
        // have built up (a Seed Pod's in bursts).
        int grew = GrowBuds(dt);
        sending.Clear();
        foreach (var node in Nodes)
            if (node.Blob >= node.Spec.BlobBurst - 1e-4f) sending.Add(node);
        if (sending.Count > 0)
        {
            var counts = CountKinds();
            foreach (var flower in sending)
            {
                while (flower.Blob >= 1 - 1e-4f) // (a whole blob, give or take rounding)
                {
                    flower.Blob = Math.Max(0, flower.Blob - 1);
                    // A Comet Bloom's burst is every kind of blob in turn.
                    var kind = flower.Spec.Sends == BlobKind.All ? (BlobKind)(1 + (int)MathF.Round(flower.Blob) % 3) : flower.Spec.Sends;
                    grew += Send(flower, kind, ref counts);
                }
            }
        }

        // Every part picks where to send from the fills as they stand, then the clumps move together, so the order
        // of the parts doesn't matter and a clump goes one link a step (the original's travelled). A part feeds its
        // fruit first, then sends on what's left.
        transfers.Clear();
        foreach (var node in Nodes)
        {
            if (node.Spec.IsFruit) continue;
            float fed = 0;
            if (FruitToFeed(node) is { } fruit)
            {
                fed = Math.Min(node.Power, FruitClump(node));
                transfers.Add((node, fruit, fed));
            }
            if (SendTo(node, node.Power - fed) is { } to) transfers.Add((node, to, Clump(node)));
        }
        foreach (var (from, to, clump) in transfers)
        {
            from.Power -= clump;
            to.Power += clump;
            if (!ShowBlobs) continue;
            var link = linkToParent[to.Parent == from ? to : from];
            link.FlowsToTarget = link.Target == to;
            link.FlashIn = (float)(looks.NextDouble() * seconds);
        }
        Tracking.Age += seconds;
        Tracking.History.Record(Tracking.Age, seconds, stored, production, upkeep);
        return grew;
    }

    // Where a part sends a clump of power this step, if anywhere (the original's attemptTransferPower): once it's
    // fuller than SendFill and has a whole clump to spare, to the neighbour (not fruit) with the lowest fill for its
    // weight among those less full than itself (fills past full count as full).
    PlantNode? SendTo(PlantNode node, float spare)
    {
        if (node.Fill <= Tuning.SendFill || spare < Clump(node)) return null;
        float fill = Math.Min(1, node.Fill);
        PlantNode? best = null;
        float bestWeighed = float.MaxValue;
        if (node.Parent != null) Consider(node.Parent);
        foreach (var branch in node.Branches) Consider(branch);
        return best;

        void Consider(PlantNode other)
        {
            float otherFill = other.Fill, weighed = otherFill / other.Spec.PowerWeight;
            if (Math.Min(1, otherFill) >= fill || weighed >= bestWeighed) return;
            best = other;
            bestWeighed = weighed;
        }
    }

    // The clump of power a part sends in one transfer: its transfer rate times the step's length, so the rate holds
    // whatever the step (a longer step sends bigger, rarer clumps).
    public static float Clump(PlantNode node) =>
        node.Spec.PowerTransferPerSecond * (float)Tuning.EconomyStepSeconds;

    // Which fruit a part feeds this step, if any (the original's powerFruits): once it's at least FeedFruitFill full,
    // its emptiest fruit, while that's less full than the part and not yet full. Fruit costs nothing to keep, so it
    // fills up and stays full: it only draws power while it grows in, or after its part ran low.
    static PlantNode? FruitToFeed(PlantNode node)
    {
        if (node.Fruits.Count == 0 || node.Fill < Tuning.FeedFruitFill || node.Power <= 0) return null;
        PlantNode? emptiest = null;
        foreach (var fruit in node.Fruits)
            if (emptiest == null || fruit.Fill < emptiest.Fill) emptiest = fruit;
        float fill = Math.Min(1, emptiest!.Fill);
        return fill < 1 && fill < Math.Min(1, node.Fill) ? emptiest : null;
    }

    // The power a part feeds a fruit in one step: its fruit rate times the step's length.
    public static float FruitClump(PlantNode node) =>
        node.Spec.PowerToFruitPerSecond * (float)Tuning.EconomyStepSeconds;

    // How hard a part works (DESIGN.md: "Everything follows its fill"): in proportion to its fill, up to full.
    public static float Output(PlantNode node) => Math.Clamp(node.Fill, 0, 1);

    // Power a part makes per second now: in proportion to its fill (an Ember's whatever its fill), nothing once full.
    public static float ProductionPerSecond(PlantNode node) =>
        node.Power >= node.Store ? 0 : FullProductionPerSecond(node) * (node.Spec.SteadyOutput ? 1 : Output(node));

    // What the cards show it making: its full rate once full (when it makes nothing, idling, not starved), else
    // what it makes now.
    public static float ShownProductionPerSecond(PlantNode node) =>
        node.Power >= node.Store ? FullProductionPerSecond(node) : ProductionPerSecond(node);

    // What a part makes per second when full (its fruit adding a little each: see FruitBonus; an Ember less as it
    // dims).
    public static float FullProductionPerSecond(PlantNode node) =>
        Math.Max(0, node.Spec.PowerPerSecond) * FruitBonus(node) * Brightness(node) * Buffed(node);

    // A part's own fruit raise its output, each by its FlowerBonus (times the part's FruitBonusScale: a Wild Lab's
    // count double).
    static float FruitBonus(PlantNode node)
    {
        float bonus = 1;
        foreach (var fruit in node.Fruits) bonus += fruit.Spec.FlowerBonus * node.Spec.FruitBonusScale;
        return bonus;
    }

    // How bright a wild flower that dims (an Ember) still is: 1 as it grows, down to EmberDimTo as its life ends.
    // Others: 1.
    public static float Brightness(PlantNode node) => !node.Spec.Dims ? 1
        : 1 - (1 - Tuning.EmberDimTo) * (float)Math.Clamp(node.Life / node.Spec.LifeSeconds, 0, 1);

    // Power a part costs per second now: its full upkeep times its fill (past full, more).
    public static float UpkeepPerSecond(PlantNode node) => FullUpkeepPerSecond(node) * Math.Max(0, node.Fill);

    // What a part costs per second when full: times 1 plus its fruits' FlowerPowerFactor (each leaf takes off a quarter).
    public static float FullUpkeepPerSecond(PlantNode node)
    {
        float upkeep = Math.Max(0, -node.Spec.PowerPerSecond);
        float factor = 1;
        foreach (var fruit in node.Fruits)
            factor += fruit.Spec.FlowerPowerFactor;
        return upkeep * factor;
    }

    // Blobs a flower makes per real hour now: gene blobs from a lab, growth blobs from a grove, in proportion to its
    // fill (their fruit adding a little).
    public static float BlobsPerHour(PlantNode node) => FullBlobsPerHour(node) * Output(node);

    // The same when full.
    public static float FullBlobsPerHour(PlantNode node) => node.Spec.BlobsPerHour * FruitBonus(node) * Buffed(node);

    // A part a Buffer'"'"'s buff blob passed lately makes BuffBonus more.
    static float Buffed(PlantNode node) => node.Buff > 0 ? 1 + Tuning.BuffBonus : 1;

    // ==== BLOBS ==== \\

    // A blob's walk from its flower (DESIGN.md, Economy, Blobs): link by link at random, never straight back unless
    // at a dead end, until a part absorbs it (a gene blob: only the core) or BlobMaxHops links have passed. Returns
    // the part that absorbed it, if one did, and adds the parts it passed to route (from the flower), if given: only
    // a blob that will be shown needs one. Many thousands of blobs leave an hour, so it allocates nothing else.
    // A Resistor stops it dead; the first Amplifier it passes (while canDouble) is noted in Doubled, where a second
    // blob carries on (see SendGenes, Grow), its walk starting there as if it had come from cameFrom, with the hops
    // left.
    PlantNode? Walk(PlantNode from, bool genes, ref Counts counts, List<PlantNode>? route, PlantNode? cameFrom = null,
        int hops = Tuning.BlobMaxHops, bool canDouble = true)
    {
        Doubled = null;
        route?.Add(from);
        var at = from;
        for (int hop = 0; hop < hops; hop++)
        {
            int options = at.Branches.Count + (at.Parent != null ? 1 : 0);
            if (options == 0) return null; // a lone core
            var skip = options > 1 ? cameFrom : null;
            var next = Option(at, rng.Next(skip != null ? options - 1 : options), skip);
            cameFrom = at;
            at = next;
            route?.Add(at);
            if (at.Spec.BlocksBlobs) return null;
            if (genes ? at == Core : Absorbs(at, counts)) return at;
            if (canDouble && Doubled == null && at.Spec.DoublesBlobs) Doubled = (at, cameFrom, hop + 1);
        }
        return null;
    }

    // Where the last walk passed an Amplifier: the part, the one it came from, and the hops it had taken.
    (PlantNode At, PlantNode From, int Hops)? Doubled;

    // The pick-th of a part's branches and then its parent, passing over skip.
    static PlantNode Option(PlantNode at, int pick, PlantNode? skip)
    {
        foreach (var branch in at.Branches)
        {
            if (branch == skip) continue;
            if (pick-- == 0) return branch;
        }
        return at.Parent!;
    }

    // One blob leaves a flower, of this kind. Returns how many parts grew from it (an Amplifier can make it two).
    int Send(PlantNode flower, BlobKind kind, ref Counts counts)
    {
        switch (kind)
        {
            case BlobKind.Genes:
                SendGenes(flower, flower, ref counts, null, Tuning.BlobMaxHops, 0, canDouble: true);
                return 0;
            case BlobKind.Growth:
                return SendGrowth(flower, flower, ref counts, null, Tuning.BlobMaxHops, 0, canDouble: true);
            case BlobKind.Buff:
                SendBuff(flower);
                return 0;
            default:
                return 0;
        }
    }

    // A blob is shown following its route, unless in a catch-up or with too many already in flight.
    List<PlantNode>? RouteIfShown() => ShowBlobs && Motes.Count < Tuning.MaxMotes ? new List<PlantNode>() : null;

    // Shown setting off after delay seconds (an Amplifier's second blob waits for the first to reach it).
    Mote? Show(List<PlantNode>? route, PlantNode flower, float delay = 0)
    {
        if (route == null || route.Count < 2) return null;
        var mote = new Mote(route, flower.Spec.Rgb) { Delay = delay };
        Motes.Add(mote);
        return mote;
    }

    // One gene blob's walk from this lab, with nothing shown or delivered: whether it found the core (tests check
    // GardenStats.GeneReach against it).
    internal bool WalkGene(PlantNode lab)
    {
        var counts = CountKinds();
        return Walk(lab, genes: true, ref counts, null) != null;
    }

    // A lab's gene blob leaves (from the lab, or carries on from an Amplifier that doubled it, its walk shown delay
    // seconds later). Only the core absorbs it; its genes count at the core in this same step, so a catch-up (which
    // runs no physics) and a live session agree, and the blob shown is their journey made visible. A blob that never
    // finds the core fades, its genes lost: labs near the core waste fewer.
    void SendGenes(PlantNode lab, PlantNode from, ref Counts counts, PlantNode? cameFrom, int hops, float delay, bool canDouble)
    {
        var route = RouteIfShown();
        bool found = Walk(from, genes: true, ref counts, route, cameFrom, hops, canDouble) != null;
        var doubled = Doubled;
        if (found) Deliver(Tuning.BlobGenes);
        if (Show(route, lab, delay) is { } mote) mote.Absorbed = found;
        if (doubled is { } d)
            SendGenes(lab, d.At, ref counts, d.From, hops - d.Hops, delay + d.Hops * Tuning.MoteLinkSeconds, canDouble: false);
    }

    // A grove's growth blob leaves (or carries on from an Amplifier, as SendGenes). Where it goes and what it does are
    // settled now, from the garden's own dice, so a catch-up and a live session agree: the part that absorbs it forms
    // more of its fruit or bud, and a part that completes is added now, hidden (its Age below zero) until the blob,
    // shown following the same walk, arrives. Returns how many parts grew. Counts are the step's, kept up to date as
    // buds start.
    int SendGrowth(PlantNode grove, PlantNode from, ref Counts counts, PlantNode? cameFrom, int hops, float delay, bool canDouble)
    {
        var route = RouteIfShown();
        var absorber = Walk(from, genes: false, ref counts, route, cameFrom, hops, canDouble);
        var doubled = Doubled;
        int grew = 0;
        if (absorber == null) Show(route, grove, delay);
        else
        {
            var grown = Feed(absorber, ref counts);
            if (grown != null) grew++;
            if (Show(route, grove, delay) is { } mote)
            {
                mote.Growth = Tuning.BlobGrowth;
                mote.Absorbed = true;
                mote.Completes = grown != null;
                if (grown != null) grown.Age = -delay - (route!.Count - 1) * Tuning.MoteLinkSeconds;
            }
            else absorber.FormingLook = absorber.Growth;
        }
        if (doubled is { } d)
            grew += SendGrowth(grove, d.At, ref counts, d.From, hops - d.Hops, delay + d.Hops * Tuning.MoteLinkSeconds, canDouble: false);
        return grew;
    }

    // A Buffer's buff blob leaves (DESIGN.md, Wild flowers). It wanders as the others do, but nothing absorbs it:
    // every part it passes, for BufferReach links (BufferAmplifiedReach more once it passes an Amplifier), makes
    // BuffBonus more for BuffSeconds. A Resistor stops it. Settled now, as the others are; shown fading where it ends.
    void SendBuff(PlantNode buffer)
    {
        var route = RouteIfShown();
        route?.Add(buffer);
        PlantNode? cameFrom = null;
        var at = buffer;
        int reach = Tuning.BufferReach;
        bool amplified = false;
        for (int hop = 0; hop < reach; hop++)
        {
            int options = at.Branches.Count + (at.Parent != null ? 1 : 0);
            if (options == 0) break;
            var skip = options > 1 ? cameFrom : null;
            var next = Option(at, rng.Next(skip != null ? options - 1 : options), skip);
            cameFrom = at;
            at = next;
            route?.Add(at);
            if (at.Spec.BlocksBlobs) break;
            at.Buff = Tuning.BuffSeconds;
            if (!amplified && at.Spec.DoublesBlobs)
            {
                amplified = true;
                reach += Tuning.BufferAmplifiedReach;
            }
        }
        Show(route, buffer);
    }

    // A part with something forming always absorbs a growth blob; one with room to start a fruit or a bud does
    // AbsorbChance of the time; a full one never does.
    bool Absorbs(PlantNode node, Counts counts)
    {
        if (node.Forming != null) return true;
        return (CanStartFruit(node) || CanBud(node, counts)) && rng.NextDouble() < Tuning.AbsorbChance;
    }

    // While a prune can be undone, nothing new starts on the pruned branch's parent, and no bud anywhere (it would
    // take a cap slot the undo needs). Passive buds wait for PassiveGrowth too.
    bool CanStartFruit(PlantNode node) =>
        node.CanFruit && Nodes.Count < Tuning.MaxNodes && lastPrune?.Nodes[0].Parent != node;

    // Whether BranchKinds has anything for this part, without listing them: the core grows only stems, any other part
    // a lab at least (labs are uncapped).
    bool CanBud(PlantNode node, Counts counts) =>
        PassiveGrowth && lastPrune == null && Nodes.Count < Tuning.MaxNodes && node.CanBranch
        && (node.Depth >= 1 || HasRoomFor(NodeKind.Stem, counts));

    // A growth blob absorbed: a part with nothing forming starts something (a bud BudChance of the time where a
    // branch could grow, its kind rolled now; else a fruit), then the blob's growth is added, and what's forming
    // completes once it has grown enough. Returns the part that grew, if one did.
    PlantNode? Feed(PlantNode node, ref Counts counts)
    {
        if (node.Forming == null)
        {
            bool bud = CanBud(node, counts) && (!CanStartFruit(node) || rng.NextDouble() < Tuning.BudChance);
            node.Forming = bud ? GrowsWild(node, Roll(BranchKinds(node, counts)), passive: true, counts) : node.Spec.Fruit;
            if (bud)
            {
                counts = counts.With(node.Forming!.Value);
                Tracking.PassiveBuds++;
                Tracking.First("passive", "First passive bud started");
            }
            node.Growth = 0;
            node.FormingLook = 0;
        }
        node.Growth += Tuning.BlobGrowth;
        if (node.Growth < (node.FormingFruit ? Tuning.FruitGrowth : Tuning.BudGrowth) - 1e-4f) return null;
        var kind = node.Forming!.Value;
        node.Forming = null;
        node.Growth = 0;
        return NodeSpecs.All[kind].IsFruit ? AddFruit(node) : AddBranch(node, kind, emerge: true);
    }

    // Every bud forming, passive or the player's, grows on its own (DESIGN.md, Growth and pruning): a whole bud in
    // BudSelfGrowSeconds while its part is full, slower as the part runs lower; a grove bud FirstGroveSpeed times as
    // fast while the garden has no grove. Fruit doesn't: only growth blobs grow it. Returns how many buds completed.
    int GrowBuds(float dt)
    {
        budding.Clear();
        bool groveless = !Nodes.Any(n => n.Spec.Kind == NodeKind.Grove);
        foreach (var node in Nodes)
        {
            if (!node.FormingBud) continue;
            float growth = Tuning.BudGrowth / Tuning.BudSelfGrowSeconds * Output(node) * dt;
            if (groveless && node.Forming == NodeKind.Grove) growth *= Tuning.FirstGroveSpeed;
            node.Growth += growth;
            node.FormingLook = Math.Min(node.Growth, node.FormingLook + growth);
            if (node.Growth >= Tuning.BudGrowth - 1e-4f) budding.Add(node);
        }
        // The new parts join after the loop over the parts.
        foreach (var node in budding)
        {
            var kind = node.Forming!.Value;
            node.Forming = null;
            node.Growth = 0;
            node.FormingLook = 0;
            AddBranch(node, kind, emerge: true);
        }
        return budding.Count;
    }

    // ==== GENES AND UPGRADES ==== \\

    // Genes reach the core, to be spent.
    public void Deliver(double genes)
    {
        Genes += genes;
        GeneBalance += genes;
        Tracking.History.AddGenes(genes);
    }

    // A track's next upgrade and its cost (null: every one bought).
    public Upgrade? Next(Track track)
    {
        switch (track)
        {
            case Track.Stems:
                return StemsBought < Tuning.StemCosts.Length ? new(track, UpgradeKind.Stem, Tuning.StemCosts[StemsBought]) : null;
            case Track.Generators:
                return GeneratorsBought < Tuning.GeneratorCosts.Length
                    ? new(track, UpgradeKind.Generator, Tuning.GeneratorCosts[GeneratorsBought]) : null;
            default:
                if (CoreBought >= Tuning.CoreCosts.Length) return null;
                var (kind, cost) = Tuning.CoreCosts[CoreBought];
                return new(track, kind, cost);
        }
    }

    // The cheapest next upgrade across the three tracks (null: every one bought).
    public Upgrade? Cheapest => Enum.GetValues<Track>().Select(Next).Where(u => u != null).MinBy(u => u!.Cost);

    // Buys a track's next upgrade from the balance, if it covers the cost: the caps rise at once. A core link or the
    // Hub is a moment in the journal (stems, generators and the core's counters aren't). Returns whether it bought.
    public bool Buy(Track track)
    {
        if (Next(track) is not { } upgrade || !Spend(upgrade.Cost)) return false;
        switch (track)
        {
            case Track.Stems: StemsBought++; break;
            case Track.Generators: GeneratorsBought++; break;
            default: CoreBought++; break;
        }
        Capacity = Capacity.Raise(upgrade.Kind);
        if (upgrade.Kind == UpgradeKind.CoreLink)
            Tracking.Log(JournalKind.Purchase, $"{Capitalised(Ordinal(Capacity.CoreLinks))} core link bought");
        if (upgrade.Kind == UpgradeKind.Hub) Tracking.Log(JournalKind.Purchase, "Hub bought");
        return true;
    }

    // The shaping tools come from the garden's size (DESIGN.md, Upgrade tracks): raises their caps to what its size
    // allows (never lowering them: the --shaping dev aid may have raised them past it), each tool gained a moment in
    // the journal when log is on.
    void GainTools(bool log)
    {
        int size = Tracking.MostMainNodes;
        var (pots, trellises, wells) = Tuning.ToolsAtSize(size);
        var gained = new List<ShapingKind>();
        if (trellises > Capacity.Trellises) gained.Add(ShapingKind.Trellis);
        if (wells > Capacity.Wells) gained.Add(ShapingKind.Well);
        if (pots > Capacity.Pots) gained.Add(ShapingKind.Pot);
        if (gained.Count == 0) return;
        Capacity = Capacity with
        {
            Pots = Math.Max(Capacity.Pots, pots),
            Trellises = Math.Max(Capacity.Trellises, trellises),
            Wells = Math.Max(Capacity.Wells, wells),
        };
        if (!log) return;
        foreach (var kind in gained) Tracking.Log(JournalKind.Tool, ToolGainedText(kind, Capacity.Of(kind), size));
    }

    // A shaping tool gained, in the journal: "Trellis gained at garden size 15", "Second Gravity Pot gained at garden
    // size 25".
    public static string ToolGainedText(ShapingKind kind, int cap, int size)
    {
        string name = cap > 1 ? $"{Capitalised(Ordinal(cap))} {ToolName(kind)}" : ToolName(kind);
        return $"{name} gained at garden size {size}";
    }

    // Whether the balance covers a cost.
    public bool CanSpend(double genes) => genes >= 0 && GeneBalance >= genes;

    // Spends genes from the balance, if it covers them (genes earned stay as they were). Returns whether it did.
    public bool Spend(double genes)
    {
        if (!CanSpend(genes)) return false;
        GeneBalance -= genes;
        return true;
    }

    static readonly string[] Ordinals = ["", "first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth"];

    // "second", or past the words "11th", "12th", "23rd".
    static string Ordinal(int n) => n < Ordinals.Length ? Ordinals[n]
        : n + ((n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });

    static string Capitalised(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    public static string ToolName(ShapingKind kind) => kind switch
    {
        ShapingKind.Pot => "Gravity Pot",
        ShapingKind.Trellis => "Trellis",
        _ => "Gravity Well",
    };

    // The whole garden's power per second now: what it makes, and what its parts cost.
    public (float Production, float Upkeep) PowerBalance()
    {
        float production = 0, upkeep = 0;
        foreach (var node in Nodes)
        {
            production += ProductionPerSecond(node);
            upkeep += UpkeepPerSecond(node);
        }
        return (production, upkeep);
    }

    // The same with every part full: whether the garden can keep itself full (DESIGN.md, Economy, Overload).
    public (float Production, float Upkeep) FullPowerBalance()
    {
        float production = 0, upkeep = 0;
        foreach (var node in Nodes)
        {
            production += FullProductionPerSecond(node);
            upkeep += FullUpkeepPerSecond(node);
        }
        return (production, upkeep);
    }

    public float TotalPower() => Nodes.Sum(n => n.Power);

    // Shows every part as it is now, with no easing (after a load or a catch-up, or a dev change to the power).
    public void SettleLooks()
    {
        foreach (var node in Nodes)
        {
            node.FillLook = node.Fill;
            node.FormingLook = node.Growth;
        }
    }

    // ==== PHYSICS ==== \\

    // One 60 Hz tick: springs, repulsion, light and wind, then movement. Also moves the motes and fades glows,
    // so everything that animates runs on the same fixed tick.
    public void Step()
    {
        const float dt = 1f / Tuning.TicksPerSecond;
        Time += dt;
        if (lastPrune != null && (UndoSecondsLeft -= dt) <= 0) ForgetPrune();
        foreach (var node in Nodes) node.PreviousPosition = node.Position;

        // Links pull only when stretched, as in the TS game; the repulsion below keeps parts apart.
        foreach (var link in Links) Pull(link);

        // A held part is pulled to the pointer by its weight, as in the TS game: light parts follow briskly, heavy
        // ones like the core lumber after it. Everything else follows only through the links.
        if (Held is { } held)
        {
            held.Velocity *= Tuning.PullFriction;
            var d = held.Position - HoldTarget;
            if (d.LengthSquared() > Tuning.PullMinDistanceSquared) held.Velocity -= Tuning.PullRatio * d * held.Spec.InverseMass;
        }

        Repel();
        DriftCut(dt);
        Shape();

        foreach (var node in Nodes)
        {
            if (node.TiedTo is { Kind: ShapingKind.Pot } pot)
            {
                Turn(node);
                Hold(node, pot);
                continue;
            }
            if (node.Fixed)
            {
                Turn(node);
                node.Velocity = Vector2.Zero;
                continue;
            }
            Move(node);
            if (node.TiedTo is { } trellis) Hold(node, trellis);
        }
        foreach (var cut in Cut)
            foreach (var node in cut.Nodes) Move(node);

        Speed = 0;
        foreach (var node in Nodes.Concat(Cut.SelectMany(c => c.Nodes)))
            Speed = Math.Max(Speed, Vector2.Distance(node.Position, node.PreviousPosition));

        UpdateMotes(dt);
    }

    // A link pulls its two ends together when stretched (a pinned end holds).
    static void Pull(Link link)
    {
        var d = link.Origin.Position - link.Target.Position;
        float dist = d.Length();
        if (dist < link.Length) return;
        float mult = Tuning.Elasticity * link.Spring * (link.Length - dist) / dist;
        if (!link.Origin.Fixed) link.Origin.Velocity += mult * d * link.Origin.Spec.InverseMass;
        if (!link.Target.Fixed) link.Target.Velocity -= mult * d * link.Target.Spec.InverseMass;
    }

    static void Turn(PlantNode node)
    {
        node.Rotation += node.Spin;
        if (node.Rotation > MathF.PI) node.Rotation -= MathF.Tau;
        else if (node.Rotation < -MathF.PI) node.Rotation += MathF.Tau;
    }

    // Light, wind, the borders, then damping and movement.
    void Move(PlantNode node)
    {
        Turn(node);
        if (node != Core) ApplyLightAndWind(node);
        if (Borders is var (min, max))
        {
            var r = new Vector2(node.Spec.Radius);
            var inside = Vector2.Clamp(node.Position, min + r, max - r);
            node.Velocity += (inside - node.Position) * Tuning.EdgeBounce;
        }

        node.Velocity *= Tuning.Damp;
        if (Math.Abs(node.Velocity.X) > Tuning.MinVelocity || Math.Abs(node.Velocity.Y) > Tuning.MinVelocity)
            node.Position += node.Velocity;
        else
            node.Velocity = Vector2.Zero;
    }

    // A cut branch is the plant's force-directed physics with every link in it gone (and its pins no longer holding):
    // its parts push each other apart, and the plant's main parts and they push each other as usual, so they drift
    // off on their own. They go after CutFadeSeconds. A branch is a handful of parts, gone in seconds, so every pair
    // is pushed exactly (fruit on the plant takes no part).
    void DriftCut(float dt)
    {
        for (int i = Cut.Count - 1; i >= 0; i--)
        {
            var cut = Cut[i];
            if ((cut.Seconds += dt) >= Tuning.CutFadeSeconds)
            {
                Cut.RemoveAt(i);
                continue;
            }
            var parts = cut.Nodes;
            foreach (var node in parts) node.PreviousPosition = node.Position;
            for (int a = 0; a < parts.Count; a++)
            {
                for (int b = a + 1; b < parts.Count; b++) Push(parts[a], parts[b], parts[a].Spec.Force, parts[b].Spec.Force);
                foreach (var node in Nodes)
                    if (!node.Spec.IsFruit) Push(parts[a], node, parts[a].Spec.Force, node.Spec.Force);
            }
        }
    }

    // Parts push each other apart by force * force / distance squared, as in the TS game. Checking every pair grows
    // with the square of the plant (220 parts: 24,000 pairs a tick; 1000: half a million), so the parts are sorted
    // into a grid of RepelCell-sized cells. Parts in the same or neighbouring cells push each other exactly; a
    // farther cell pushes as one, its parts' forces summed at their (force-weighted) centre. The faint far pushes
    // still add up, and they're what spreads a big plant out, so they can't just be dropped. Far-reaching parts
    // (the core, force 30) push every part exactly.
    //
    // The far push changes slowly (distant parts barely move relative to each other in a tick), so each part keeps
    // it (FarPush) and applies it every tick, and it's worked out afresh for a share of the cells each tick, in
    // turn: every cell every FarPushEvery ticks, spread evenly so no tick is heavier than the rest.
    //
    // With FruitBorrowsPush, fruit (two thirds of a grown plant, light and weak) stays out of the grid, which makes
    // a fruit-heavy plant's push about 4x cheaper, and nearly as tidy. Both ways the fruit's push goes through its
    // parent:
    //   - a part with fruit pushes the rest of the plant with its fruit's force added to its own (PushForce);
    //   - the fruit gets the push its parent got from the rest of the plant and the core (RepelPush, FarPush),
    //     scaled from the parent's force and mass to the fruit's: what it would have felt, near enough, being close.
    // Only its parent and its sibling fruit push it directly (and it pushes them back).
    void Repel()
    {
        int turn = (int)(ticks++ % Tuning.FarPushEvery);
        bool borrow = FruitBorrowsPush;
        foreach (var node in Nodes)
        {
            node.PushForce = node.Spec.Force;
            node.RepelPush = Vector2.Zero;
            if (!borrow) continue;
            if (node.Spec.IsFruit) node.FarPush = Vector2.Zero; // fruit gets no far push in this mode
            foreach (var fruit in node.Fruits) node.PushForce += fruit.Spec.Force;
        }

        foreach (var cell in cells.Values) cell.Nodes.Clear();
        if (cells.Count > Nodes.Count * 4) cells.Clear(); // the plant has moved on; drop the empty cells
        farReaching.Clear();
        foreach (var node in Nodes)
        {
            if (borrow && node.Spec.IsFruit) continue;
            if (node.Spec.Force >= Tuning.FarReachForce)
            {
                farReaching.Add(node);
                continue;
            }
            var key = CellOf(node.Position);
            if (!cells.TryGetValue(key, out var cell)) cells[key] = cell = new();
            cell.Nodes.Add(node);
        }

        occupied.Clear();
        foreach (var (key, cell) in cells)
        {
            if (cell.Nodes.Count == 0) continue;
            float force = 0;
            var weighted = Vector2.Zero;
            foreach (var n in cell.Nodes)
            {
                force += n.PushForce;
                weighted += n.Position * n.PushForce;
            }
            cell.Force = force;
            cell.Centre = weighted / force;
            occupied.Add((key, cell));
        }

        foreach (var (key, cell) in occupied)
        {
            // Near: each pair once, within the cell and with the four neighbours ahead of it (the other four see it).
            var list = cell.Nodes;
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++) Push(list[i], list[j]);
            foreach (var (dx, dy) in ForwardNeighbours)
            {
                if (!cells.TryGetValue((key.X + dx, key.Y + dy), out var other)) continue;
                foreach (var a in list)
                    foreach (var b in other.Nodes) Push(a, b);
            }

            // Far, on this cell's turn: every cell beyond the neighbours pushes this cell's parts as one. (That
            // cell's parts get this cell's push the same way, on their turn.)
            if (TurnOf(key) != turn) continue;
            foreach (var a in list) a.FarPush = Vector2.Zero;
            foreach (var (otherKey, other) in occupied)
            {
                if (Math.Abs(otherKey.X - key.X) <= 1 && Math.Abs(otherKey.Y - key.Y) <= 1) continue;
                foreach (var a in list)
                {
                    var d = Floor(a.Position - other.Centre);
                    float dist2 = d.LengthSquared();
                    a.FarPush += a.PushForce * other.Force / dist2 / MathF.Sqrt(dist2) * d * a.Spec.InverseMass;
                }
            }
        }
        foreach (var node in Nodes) node.Velocity += node.FarPush;

        for (int i = 0; i < farReaching.Count; i++)
        {
            var big = farReaching[i];
            foreach (var node in Nodes)
            {
                if (node == big || (node.Spec.Force >= Tuning.FarReachForce && farReaching.IndexOf(node) <= i)) continue;
                if (borrow && node.Spec.IsFruit) continue; // it gets the core's push through its parent
                Push(big, node);
            }
        }

        if (!borrow) return;
        foreach (var parent in Nodes)
        {
            var fruits = parent.Fruits;
            if (fruits.Count == 0) continue;
            // The push the parent got from the rest of the plant, as a field: per unit of its force and mass.
            var field = (parent.RepelPush + parent.FarPush) * parent.Spec.Mass / parent.PushForce;
            for (int i = 0; i < fruits.Count; i++)
            {
                var fruit = fruits[i];
                fruit.Velocity += field * fruit.Spec.Force * fruit.Spec.InverseMass;
                Push(fruit, parent, fruit.Spec.Force, parent.Spec.Force);
                for (int j = i + 1; j < fruits.Count; j++) Push(fruit, fruits[j], fruit.Spec.Force, fruits[j].Spec.Force);
            }
        }
    }

    sealed class Cell
    {
        public readonly List<PlantNode> Nodes = new();
        public float Force;
        public Vector2 Centre;
    }

    static readonly (int X, int Y)[] ForwardNeighbours = [(1, 0), (1, 1), (0, 1), (-1, 1)];

    // Which tick of the FarPushEvery a cell's far push is worked out on: scattered, so neighbouring cells differ.
    static int TurnOf((int X, int Y) key) =>
        (int)((uint)(key.X * 73856093 ^ key.Y * 19349663) % Tuning.FarPushEvery);

    static (int X, int Y) CellOf(Vector2 p) =>
        ((int)MathF.Floor(p.X / Tuning.RepelCell), (int)MathF.Floor(p.Y / Tuning.RepelCell));

    // The 2 px floor on each axis stops overlapping parts from flinging each other away.
    static Vector2 Floor(Vector2 d) =>
        new(d.X > 0 ? Math.Max(2, d.X) : Math.Min(-2, d.X), d.Y > 0 ? Math.Max(2, d.Y) : Math.Min(-2, d.Y));

    // Two parts push each other, with any fruit they carry counted in their force (only with FruitBorrowsPush).
    static void Push(PlantNode a, PlantNode b) => Push(a, b, a.PushForce, b.PushForce);

    // Each side's push is also noted in RepelPush, for its fruit to borrow.
    static void Push(PlantNode a, PlantNode b, float forceA, float forceB)
    {
        var d = Floor(a.Position - b.Position);
        float dist2 = d.LengthSquared();
        float mult = forceA * forceB / dist2 / MathF.Sqrt(dist2);
        var pushA = mult * d * a.Spec.InverseMass;
        var pushB = mult * d * b.Spec.InverseMass;
        a.Velocity += pushA;
        a.RepelPush += pushA;
        b.Velocity -= pushB;
        b.RepelPush -= pushB;
    }

    // Light, wind and the ground are all measured from the core, wherever it's been planted.
    void ApplyLightAndWind(PlantNode node)
    {
        var p = node.Position - Core.Position;
        float t = (float)Time;
        // A slow breath with two faster gusts over it; the phase shifts with height so the sway ripples up the plant.
        float gust = 0.5f * MathF.Sin(t * 0.11f) + 0.7f * MathF.Sin(t * 0.37f + p.Y * 0.004f)
            + 0.3f * MathF.Sin(t * 0.93f + node.Id * 0.7f);

        if (Layout == Layout.Upright)
        {
            node.Velocity.Y -= node.Spec.IsFruit ? Tuning.UpwardFruit : Tuning.Upward;
            float height = Math.Clamp(-p.Y / Tuning.WindHeight, 0, 1);
            node.Velocity.X += Tuning.WindStrength * height * gust;
            // The ground sits at the bottom of the core.
            float ground = Core.Spec.Radius - node.Spec.Radius;
            if (p.Y > ground) node.Velocity.Y += Tuning.GroundBounce * (ground - p.Y);
        }
        else
        {
            float dist = p.Length();
            if (dist < 1) return;
            float reach = Math.Clamp(dist / Tuning.WindHeight, 0, 1);
            var tangent = new Vector2(-p.Y, p.X) / dist;
            node.Velocity += tangent * (Tuning.WindStrength * reach * gust);
        }
    }

    // ==== MOTES AND FLASHES ==== \\

    // Power shows as flashes, not motes: as in the original game, each clump of power sent lights its link (at a
    // random moment in its economy step, so the links don't flash in step) and the light fades over about 0.3 s: a
    // busy trunk pulses, a quiet twig now and then. Motes are the blobs that do travel: genes from the labs (see SendGenes)
    // and growth from the groves (see SendGrowth).
    void UpdateMotes(float dt)
    {
        foreach (var node in Nodes)
        {
            node.Age += dt;
            node.Glow = Math.Max(0, node.Glow - Tuning.GlowFadePerSecond * dt);

            // As the original's PlantNodeView.adjustIntensity: a share of the way to the fill each tick, quicker
            // filling than emptying.
            float fill = node.Fill;
            node.FillLook += (fill - node.FillLook) * (fill > node.FillLook ? Tuning.FillLookRise : Tuning.FillLookFall);
        }
        foreach (var link in Links)
        {
            link.Flash = Math.Max(0, link.Flash - Tuning.LinkFlashFadePerSecond * dt);
            link.FlashAge += dt;
            if (link.FlashIn < 0 || (link.FlashIn -= dt) >= 0) continue;
            link.FlashIn = -1;
            // The flash is as bright as the part the power reached is full, so power trickling into a low branch
            // glimmers and only a full one flashes brightly.
            var reached = link.FlowsToTarget ? link.Target : link.Origin;
            link.Flash = link.FlashStrength = Tuning.FlashPeak * Math.Clamp(reached.Fill, 0, 1);
            link.FlashAge = 0;
        }

        for (int i = Motes.Count - 1; i >= 0; i--)
        {
            var mote = Motes[i];
            if (mote.Delay > 0)
            {
                mote.Delay -= dt;
                continue;
            }
            mote.T += dt / Tuning.MoteLinkSeconds;
            if (mote.T < 1) continue;

            var arrived = mote.To;
            var route = mote.Route;
            var next = ++mote.Leg + 1 < route.Count ? route[mote.Leg + 1] : null;
            if (next == null)
            {
                // A growth blob's work shows as it arrives: what's forming grows, or the part it completed appears
                // (see SendGrowth) and the silhouette goes.
                if (mote.Completes) arrived.FormingLook = 0;
                else if (mote.Growth > 0) arrived.FormingLook = Math.Min(arrived.Growth, arrived.FormingLook + mote.Growth);
                // A blob that's absorbed flashes its part in its own colour; one that fades unabsorbed just goes.
                if (mote.Absorbed)
                {
                    arrived.Glow = 1;
                    arrived.GlowRgb = mote.Rgb;
                }
                Motes.RemoveAt(i);
                continue;
            }
            mote.From = arrived;
            mote.To = next;
            mote.T -= 1;
        }
    }

    // ==== QUERIES ==== \\

    // The part under the pointer. Fruit never counts: it can't be hovered, tapped or dragged, and the pointer
    // passes through it (as in the TS game, whose pointer skipped fruit).
    public PlantNode? NodeAt(Vector2 point, float slop)
    {
        PlantNode? best = null;
        float bestDist = float.MaxValue;
        foreach (var node in Nodes)
        {
            if (node.Spec.IsFruit) continue;
            float d = Vector2.Distance(node.Position, point);
            if (d < node.Spec.Radius + slop && d < bestDist)
            {
                best = node;
                bestDist = d;
            }
        }
        return best;
    }

    public (Vector2 Min, Vector2 Max) Bounds()
    {
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var node in Nodes)
        {
            var r = new Vector2(node.Spec.Radius);
            min = Vector2.Min(min, node.Position - r);
            max = Vector2.Max(max, node.Position + r);
        }
        return (min, max);
    }

    // ==== SAVES ==== \\

    public GardenSave ToSave() => new(
        Layout, EconomyClock, Capacity,
        Nodes.Select(n => new NodeSave(n.Id, n.Spec.Kind, n.Parent?.Id ?? -1, n.Position.X, n.Position.Y,
            n.Power, n.Blob, n.Forming, n.Growth, n.Life, n.Charge)).ToList(),
        Genes,
        Shaping.Select(t => new ShapingSave(t.Kind, t.Position.X, t.Position.Y, t.Angle, t.Length,
            Nodes.Where(n => n.TiedTo == t).Select(n => n.Id).ToList())).ToList(),
        Tracking.ToSave(), GeneBalance, new ResearchSave(StemsBought, GeneratorsBought, CoreBought), new(WildCaps));

    // Parts are saved in the order they grew, so a parent always comes before its children.
    public static Garden FromSave(GardenSave save, int? seed = null)
    {
        var garden = new Garden(save.Layout, seed)
        {
            EconomyClock = save.EconomyClock,
            Capacity = save.Capacity,
            Genes = save.Genes,
            GeneBalance = save.GeneBalance,
            Tracking = Tracking.FromSave(save.Tracking),
            loading = true,
        };
        if (save.Research is { } research)
            (garden.StemsBought, garden.GeneratorsBought, garden.CoreBought) = (research.Stems, research.Generators, research.Core);
        else garden.ReadOldTrack();
        foreach (var (kind, cap) in save.WildCaps ?? []) garden.WildCaps[kind] = cap;
        garden.GainTools(log: false);
        var byId = new Dictionary<int, PlantNode>();
        foreach (var n in save.Nodes)
        {
            var parent = n.ParentId < 0 ? null : byId[n.ParentId];
            var node = garden.Add(NodeSpecs.All[n.Kind], parent, new Vector2(n.X, n.Y));
            node.Age = 10;
            node.Fixed = parent == null && garden.Planted;
            node.Power = n.Power;
            node.Blob = n.Blob;
            node.Forming = n.Forming;
            node.Growth = n.Growth;
            node.Life = n.Life;
            node.Charge = n.Charge;
            byId[n.Id] = node;
        }
        foreach (var s in save.Shaping ?? [])
        {
            var tool = new ShapingTool(s.Kind, new Vector2(s.X, s.Y), s.Angle, s.Length);
            garden.Shaping.Add(tool);
            foreach (var id in s.Tied)
                if (byId.TryGetValue(id, out var node)) node.TiedTo = tool;
        }
        garden.loading = false;
        garden.SettleLooks();
        return garden;
    }

    // A version 9 save from before the tracks (step 5a) raised its caps on the old unlock track: they're kept, read as
    // the stem and generator upgrades bought, and a second core link as the core track bought up to it (so its wild
    // flower slot comes too). Its seedling slot is the start's.
    void ReadOldTrack()
    {
        StemsBought = Math.Clamp(Capacity.Stems - Tuning.StartStemCap, 0, Tuning.StemCosts.Length);
        GeneratorsBought = Math.Clamp(Capacity.Generators - Tuning.StartGeneratorCap, 0, Tuning.GeneratorCosts.Length);
        CoreBought = Capacity.CoreLinks > Tuning.StartCoreLinks ? 2 : 0;
        Capacity = Capacity with
        {
            WildFlowers = Tuning.StartWildFlowers + (CoreBought > 0 ? 1 : 0),
            Seedlings = Tuning.StartSeedlings,
            Hubs = 0,
        };
    }
}

// The limits on growth (DESIGN.md, Capacity): how many stems and generators the garden may have, how many links the
// core has, and how many of each shaping tool may be placed. Labs and other flowers are uncapped; free links and power
// limit them. The wild flower limit, seedling slots and Hubs are counters for now (their mechanics come in 5c, 5d and
// the Hub's step). Upgrades bought raise these, and the garden's size the shaping tools.
public sealed record Capacity(int Stems, int Generators, int CoreLinks, int Pots = Tuning.StartPotCap, int Trellises = 0,
    int Wells = 0, int WildFlowers = Tuning.StartWildFlowers, int Seedlings = Tuning.StartSeedlings, int Hubs = 0)
{
    public static readonly Capacity Start = new(Tuning.StartStemCap, Tuning.StartGeneratorCap, Tuning.StartCoreLinks);
    public static readonly Capacity Unlimited = new(int.MaxValue, int.MaxValue, 2, 3, 3, 3); // stress tests (--parts)

    // These limits with an upgrade's +1 (never past int.MaxValue, so an unlimited garden stays so).
    public Capacity Raise(UpgradeKind kind) => kind switch
    {
        UpgradeKind.Stem => this with { Stems = Add(Stems, 1) },
        UpgradeKind.Generator => this with { Generators = Add(Generators, 1) },
        UpgradeKind.CoreLink => this with { CoreLinks = Add(CoreLinks, 1) },
        UpgradeKind.WildFlower => this with { WildFlowers = Add(WildFlowers, 1) },
        UpgradeKind.Seedling => this with { Seedlings = Add(Seedlings, 1) },
        _ => this with { Hubs = Add(Hubs, 1) },
    };

    public int Of(ShapingKind kind) => kind switch
    {
        ShapingKind.Pot => Pots,
        ShapingKind.Trellis => Trellises,
        _ => Wells,
    };

    static int Add(int a, int b) => (int)Math.Min(int.MaxValue, (long)a + b);
}

// The three upgrade tracks of the Research panel (DESIGN.md, Upgrade tracks), and what an upgrade raises by 1.
public enum Track { Stems, Generators, Core }

public enum UpgradeKind { Stem, Generator, WildFlower, CoreLink, Seedling, Hub }

// A track's next upgrade: what it raises and its cost in genes.
public sealed record Upgrade(Track Track, UpgradeKind Kind, double Cost)
{
    // As the Research panel and the garden card name it: "+1 stem", "Hub".
    public string Name => Kind switch
    {
        UpgradeKind.Stem => "+1 stem",
        UpgradeKind.Generator => "+1 generator",
        UpgradeKind.WildFlower => "+1 wild flower",
        UpgradeKind.CoreLink => "+1 core link",
        UpgradeKind.Seedling => "+1 seedling slot",
        _ => "Hub",
    };

    // What it gives, the hint behind the Research panel's (i) (Jeremy's wording for the first three, Oct 30).
    public string Does => Kind switch
    {
        UpgradeKind.Stem => "Increases the total number of stems you can add to your garden.",
        UpgradeKind.Generator => "Increases the total number of generators you can add to your garden.",
        UpgradeKind.WildFlower => "Lets special, rare wild flowers grow instead of normal stems or flowers.",
        UpgradeKind.CoreLink => "Lets one more branch grow straight from the core.",
        UpgradeKind.Seedling => "Lets you plant one more seedling, and so choose one more evolution.",
        _ => "Unlocks the Hub: plant it by hand, and it catches genes to boost each kind of part.",
    };
}

// How many of each track's upgrades were bought.
public sealed record ResearchSave(int Stems, int Generators, int Core);

// (Saves from before step 3d also carry Pinned, from when parts could be pinned; it's ignored.)
// (Life and Charge since step 5d: a wild flower's life cycle.)
public sealed record NodeSave(int Id, NodeKind Kind, int ParentId, float X, float Y, float Power,
    float Blob = 0, NodeKind? Forming = null, float Growth = 0, double Life = 0, float Charge = 0);

// (WildCaps since step 5d: the capped wild flower kinds unlocked, see Garden.WildCaps.)
public sealed record GardenSave(Layout Layout, double EconomyClock, Capacity Capacity, List<NodeSave> Nodes,
    double Genes = 0, List<ShapingSave>? Shaping = null, TrackingSave? Tracking = null, double GeneBalance = 0,
    ResearchSave? Research = null, Dictionary<NodeKind, int>? WildCaps = null);
