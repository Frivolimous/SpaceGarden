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

    // The opening's tutorial step (see Tutorial), or null: none, or over. A new garden starts one with StartTutorial;
    // an older save has none. Until the lab is planted, growth blobs start no passive buds, so the stem's free links
    // wait for the grove, stem and lab it asks for.
    public TutorialStep? Tutorial { get; private set; }

    public void StartTutorial() => Tutorial = TutorialStep.Welcome;

    // Passes every step the garden has done (not one that waits for the player: see ActTutorial); returns whether the
    // step changed.
    public bool AdvanceTutorial()
    {
        var was = Tutorial;
        while (Tutorial is { } step && !global::SpaceGarden.Core.Tutorial.ByAct(step)
            && global::SpaceGarden.Core.Tutorial.Done(this, step))
            Tutorial = step + 1;
        return Tutorial != was;
    }

    // The player did what the step waits for (Continue; Research opened): the next step, or after the last, the end.
    // Returns whether it was the step's act.
    public bool ActTutorial(TutorialStep step)
    {
        if (Tutorial != step || !global::SpaceGarden.Core.Tutorial.ByAct(step)) return false;
        Tutorial = step == TutorialStep.Done ? null : step + 1;
        AdvanceTutorial();
        return true;
    }

    // Whether the tutorial's step lets the player do this now (see Tutorial.Allows).
    public bool TutorialAllows(TutorialAction action, NodeKind? kind = null, ShapingKind? shaping = null) =>
        global::SpaceGarden.Core.Tutorial.Allows(Tutorial, action, kind, shaping);

    // How much faster buds and fruit grow: Tuning.TutorialGrowthSpeed while the tutorial runs, until the lab is out.
    float TutorialGrowth => Tutorial != null && !Nodes.Any(n => IsBudOf(n.Spec.Kind, NodeKind.Lab)) ? Tuning.TutorialGrowthSpeed : 1;

    bool TutorialHoldsBuds => Tutorial is { } t && t <= TutorialStep.Lab;

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
    readonly List<PlantNode> blooming = new(); // seedlings that bloom this economy step
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
        garden.Tracking.Log(JournalKind.Planted, "A new garden began");
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
        // (A seedling never grows wild: it blooms into one. Nor does the Hub.)
        node.Forming = NodeSpecs.All[kind].IsWild || kind is NodeKind.Seedling or NodeKind.Hub ? kind
            : GrowsWild(node, kind, passive: false, CountKinds());
        node.Growth = 0;
        node.FormingLook = 0;
        return true;
    }

    // The player's say over a bud forming in a part (Jeremy, Nov 3), passive or planted: cancel it (its growth lost, its
    // link and slots free again), or change it to another kind, which starts over as a new bud would (and may grow
    // wild). Returns whether it did.
    public bool CancelBud(PlantNode node)
    {
        if (node.Spec.IsFruit && node.Parent != null) node = node.Parent;
        if (!node.FormingBud) return false;
        node.Forming = null;
        node.Growth = 0;
        node.FormingLook = 0;
        return true;
    }

    public bool ChangeBud(PlantNode node, NodeKind kind)
    {
        if (node.Spec.IsFruit && node.Parent != null) node = node.Parent;
        return CanChangeBud(node, kind) && CancelBud(node) && SproutFrom(node, kind);
    }

    // Whether the bud forming here could become this kind: another kind than it is (or stands in for, if wild), with
    // room for it once this bud's own room is free.
    public bool CanChangeBud(PlantNode node, NodeKind kind)
    {
        if (node.Spec.IsFruit && node.Parent != null) node = node.Parent;
        if (node.Forming is not { } forming || !node.FormingBud || IsBudOf(forming, kind)) return false;
        node.Forming = null; // (as if cancelled, just to ask)
        bool can = CanSproutFrom(node, kind);
        node.Forming = forming;
        return can;
    }

    // Whether a bud forming as this kind is one of that kind: the kind itself, or a wild flower standing in for it.
    public static bool IsBudOf(NodeKind forming, NodeKind kind) =>
        forming == kind || NodeSpecs.All[forming].Wild?.Base == kind;

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
        if (removed.Any(n => n.IsHub)) UpdateBoosts(); // its levels go dormant
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
            if (root.Spec.IsFruit ? parent.Fruits.Count >= parent.MaxFruits : !parent.CanBranch) return false;
            // A seedling comes back with its slot (its evolution's, if it was waiting for it and no other seedling has
            // taken it up since).
            if (nodes.Any(n => n.Evolution is { } e && (!Evolved.Contains(e) || Nodes.Any(o => o.Evolution == e))))
                return false;
            int seedlings = nodes.Count(n => n.IsSeedling || n.Forming == NodeKind.Seedling);
            if (seedlings > 0 && SeedlingsUsed + seedlings > Capacity.Seedlings) return false;
            // A Hub comes back only if no other has been planted since.
            int hubs = nodes.Count(n => n.IsHub || n.Forming == NodeKind.Hub);
            if (hubs > 0 && CountOf(NodeKind.Hub) + hubs > Capacity.Hubs) return false;
            var counts = CountKinds();
            var (stems, generators, wild) = (counts.Stems, counts.Generators, counts.Wild);
            stems += nodes.Count(n => SlotOf(n.Spec.Kind) == NodeKind.Stem);
            generators += nodes.Count(n => SlotOf(n.Spec.Kind) == NodeKind.Generator);
            wild += nodes.Count(n => n.Spec.IsWild);
            return stems <= StemCap && generators <= Capacity.Generators && wild <= Capacity.WildFlowers
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
        if (nodes.Any(n => n.IsHub)) UpdateBoosts(); // its levels wake
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

    // After a change of size (Jeremy, Nov 3): each shaping tool goes where the widget puts it (keeping its distance from
    // the screen's edges); each part tied to one locks onto it at once (a pot's part to the pot, a trellis's parts moving
    // with it); if no part is tied, the core goes to coreTo (its own distance from the edges kept). Every other part
    // moves as the anchored part nearest it along the plant does, so the plant keeps its shape round its anchors. Cut
    // branches still drifting move as the core does.
    public void Reanchor(IReadOnlyDictionary<ShapingTool, Vector2> toolTo, Vector2 coreTo)
    {
        var moved = new Dictionary<PlantNode, Vector2>();
        var queue = new Queue<PlantNode>();
        foreach (var tool in Shaping)
        {
            var to = toolTo.TryGetValue(tool, out var p) ? p : tool.Position;
            var by = to - tool.Position;
            tool.Position = to;
            foreach (var node in Nodes.Where(n => n.TiedTo == tool))
            {
                moved[node] = tool.Kind == ShapingKind.Pot ? to - node.Position : by;
                queue.Enqueue(node);
            }
        }
        if (moved.Count == 0)
        {
            moved[Core] = coreTo - Core.Position;
            queue.Enqueue(Core);
        }
        // Outwards from the anchors along the links: each part takes the move of the anchor it's reached from first.
        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            foreach (var next in at.Neighbours())
                if (moved.TryAdd(next, moved[at])) queue.Enqueue(next);
        }
        foreach (var (node, by) in moved)
        {
            node.Position += by;
            node.PreviousPosition += by;
        }
        var coreBy = moved.GetValueOrDefault(Core);
        foreach (var node in Cut.SelectMany(c => c.Nodes))
        {
            node.Position += coreBy;
            node.PreviousPosition += coreBy;
        }
        HoldTarget += coreBy;
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
        if (tool.Kind is ShapingKind.Well or ShapingKind.Sway || node.Spec.IsFruit || !Shaping.Contains(tool)) return false;
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
                case ShapingKind.Sway:
                    Consider(tool, ShapingHandle.Turn, tool.Position + tool.Facing * (ShapingTool.DialRadius + ShapingTool.DialKnob),
                        ShapingTool.HandleReach);
                    Consider(tool, ShapingHandle.Body, tool.Position, ShapingTool.DialRadius + 2);
                    break;
            }
        }
        return handle ?? body;
    }

    // The tools' pushes and pulls, before the parts move (DESIGN.md, Shaping the garden):
    //   - each main part linked to a potted one (growing straight out of it, or its parent) is pushed the way the pot
    //     faces (its Lean, summed if it's next to more than one pot), as a force: a stem feels all of it, the heavy core
    //     a thirtieth. Parts further away only follow through their links;
    //   - a well (each part's nearest, if there are several) pulls every part towards it, the core and fruit too, alike
    //     whatever their weight, like gravity: a free
    //     plant drifts whole to the well and settles round it; one held back (its core in a pot) hangs towards it.
    // (Pots and trellises hold their parts after they move: see Hold.)
    void Shape()
    {
        swayScale = SwayScale;
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

        // With two or more wells, each part feels only its nearest (Jeremy, Nov 3): a potted plant's branches lean each
        // to their own well, rather than equal pulls cancelling out.
        wells.Clear();
        foreach (var tool in Shaping) if (tool.Kind == ShapingKind.Well) wells.Add(tool);
        if (wells.Count == 0) return;
        foreach (var node in Nodes)
        {
            var well = wells[0];
            float best = Vector2.DistanceSquared(well.Position, node.Position);
            for (int i = 1; i < wells.Count; i++)
            {
                float d2 = Vector2.DistanceSquared(wells[i].Position, node.Position);
                if (d2 < best) (well, best) = (wells[i], d2);
            }
            var d = well.Position - node.Position;
            float dist = d.Length();
            if (dist < 0.5f) continue;
            node.Velocity += d / dist * (Tuning.WellPull * Math.Min(1, dist / Tuning.WellEase));
        }
    }

    // How much the wind sways the plant: 1, scaled by each sway controller's dial (they multiply).
    public float SwayScale
    {
        get
        {
            float scale = 1;
            foreach (var tool in Shaping) if (tool.Kind == ShapingKind.Sway) scale *= tool.Sway;
            return scale;
        }
    }

    float swayScale = 1; // SwayScale, worked out once a tick (see Shape)
    readonly List<ShapingTool> wells = new(); // (Shape's, kept to save making one a tick)

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
    // caps. A wild flower takes a wild flower slot, and a stem or generator kind (but the Amplifier and the Resistor) its
    // base kind's slot too (see SlotOf).
    readonly record struct Counts(int Stems, int Generators, int Wild)
    {
        // With one more of this kind (a bud starting).
        public Counts With(NodeKind kind)
        {
            var counts = NodeSpecs.All[kind].IsWild ? this with { Wild = Wild + 1 } : this;
            return SlotOf(kind) switch
            {
                NodeKind.Stem => counts with { Stems = Stems + 1 },
                NodeKind.Generator => counts with { Generators = Generators + 1 },
                _ => counts,
            };
        }
    }

    // The capped kind whose slot a kind takes: a basic kind its own, a wild flower its base's if it takes one
    // (WildSpec.BaseSlot: a Tendril a stem's, a Pulsar a generator's), else none.
    internal static NodeKind? SlotOf(NodeKind kind) => NodeSpecs.All[kind].Wild is { } wild ? (wild.BaseSlot ? wild.Base : null) : kind;

    Counts CountKinds()
    {
        int stems = 0, generators = 0, wild = 0;
        void Count(NodeKind kind)
        {
            var slot = SlotOf(kind);
            if (slot == NodeKind.Stem) stems++;
            else if (slot == NodeKind.Generator) generators++;
            if (NodeSpecs.All[kind].IsWild) wild++;
        }
        foreach (var n in Nodes)
        {
            Count(n.Spec.Kind);
            if (n.FormingBud) Count(n.Forming!.Value);
        }
        return new(stems, generators, wild);
    }

    // Stems and generators are capped; labs and groves only by free links; wild flowers by the wild flower limit,
    // whether their kind can grow yet (see CanGrowWild), and their base's cap if they take its slot.
    bool HasRoomFor(NodeKind kind, Counts counts)
    {
        if (kind == NodeKind.Seedling) return SeedlingsUsed < Capacity.Seedlings;
        if (kind == NodeKind.Hub) return CountOf(NodeKind.Hub) < Capacity.Hubs; // one at a time, once bought
        if (NodeSpecs.All[kind].IsWild && (counts.Wild >= Capacity.WildFlowers || !CanGrowWild(kind))) return false;
        return SlotOf(kind) switch
        {
            NodeKind.Stem => counts.Stems < StemCap,
            NodeKind.Generator => counts.Generators < Capacity.Generators,
            _ => true,
        };
    }

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

    // ==== WILD FLOWERS ==== \

    // The Evolution kinds unlocked (none are Evolution kinds since Nov 3; kept for later ones): they join the pool once their evolution is taken
    // (see UnlockWild).
    public readonly HashSet<NodeKind> WildUnlocked = new();
    // Caps on single wild flower kinds, kept in data though none is set for now (DESIGN.md, Wild flowers: no per-kind
    // caps): a kind with no entry is uncapped.
    public readonly Dictionary<NodeKind, int> WildCaps = new();

    // The wild flowers growing and forming, against the limit.
    public int WildCount => CountKinds().Wild;
    public int FreeWildSlots => Math.Max(0, Capacity.WildFlowers - WildCount);

    // The cap on a wild flower kind (null: uncapped).
    public int? KindCap(NodeKind kind) => WildCaps.TryGetValue(kind, out var cap) ? cap : null;

    // Whether a wild flower kind can grow now: a Start kind always, an Evolution kind once unlocked; and within its
    // cap, if it has one.
    public bool CanGrowWild(NodeKind kind) =>
        NodeSpecs.All[kind].Wild is { } wild && !NodeSpecs.All[kind].Retired && (wild.Unlock == WildUnlock.Start || WildUnlocked.Contains(kind))
        && (KindCap(kind) is not { } cap || CountOf(kind) < cap);

    // Its evolution was taken (step 5c calls this as it blooms): the kind joins the pool.
    public void UnlockWild(NodeKind kind)
    {
        if (NodeSpecs.All[kind].Wild is { Unlock: WildUnlock.Evolution }) WildUnlocked.Add(kind);
    }

    // Parts of a kind, and buds forming of it.
    int CountOf(NodeKind kind) => Nodes.Count(n => n.Spec.Kind == kind || n.Forming == kind);

    // A new bud may grow wild (DESIGN.md, Wild flowers): the chance is the passive or planted one while a wild flower
    // slot is free (none free, no chance, and no dice thrown, so a garden without slots grows exactly as before), raised
    // near a Moonflower. If it does, its kind is one of those that can stand in for its base kind, among those that can grow
    // now; a passive bud may also become a passive-only kind, but leans to its base's own (Tuning.PassiveBaseShare).
    // Alike within each group. Returns the bud's kind.
    NodeKind GrowsWild(PlantNode parent, NodeKind kind, bool passive, Counts counts, float times = 1)
    {
        int free = Capacity.WildFlowers - counts.Wild;
        if (free <= 0) return kind;
        double chance = (passive ? Tuning.PassiveWildChance : Tuning.PlantedWildChance) * WildBoostAt(parent) * times;
        if (rng.NextDouble() >= chance) return kind;
        var own = NodeSpecs.Wild.Where(s => s.Wild!.Base == kind && CanGrowWild(s.Kind)).Select(s => s.Kind).ToList();
        var passiveOnly = passive
            ? NodeSpecs.Wild.Where(s => s.Wild!.PassiveOnly && CanGrowWild(s.Kind)).Select(s => s.Kind).ToList()
            : [];
        if (own.Count == 0 && passiveOnly.Count == 0) return kind;
        var pool = own.Count == 0 || (passiveOnly.Count > 0 && rng.NextDouble() >= Tuning.PassiveBaseShare) ? passiveOnly : own;
        return pool[rng.Next(pool.Count)];
    }

    // The same for tests: what a bud of this kind starting on parent now would be.
    internal NodeKind RollBud(PlantNode parent, NodeKind kind, bool passive) => GrowsWild(parent, kind, passive, CountKinds());

    // How much likelier a bud on this part is to grow wild: each wild flower with a WildBoost (a Moonflower) within
    // Tuning.MoonflowerReach links adds its boost.
    float WildBoostAt(PlantNode parent)
    {
        float boost = 1;
        if (!Nodes.Any(n => n.Spec.WildBoost > 0)) return boost;
        foreach (var n in Within(parent, MoonflowerReach)) boost += n.Spec.WildBoost;
        return boost;
    }

    // The main parts within so many links of this one, itself included.
    static IEnumerable<PlantNode> Within(PlantNode from, int links)
    {
        var seen = new HashSet<PlantNode> { from };
        var ring = new List<PlantNode> { from };
        for (int step = 0; step <= links && ring.Count > 0; step++)
        {
            var next = new List<PlantNode>();
            foreach (var n in ring)
            {
                yield return n;
                if (n.Parent != null && seen.Add(n.Parent)) next.Add(n.Parent);
                foreach (var b in n.Branches)
                    if (seen.Add(b)) next.Add(b);
            }
            ring = next;
        }
    }

    // How far a Moonflower reaches: MoonflowerReach links, HubMoonflowerReach with its Hub milestone.
    int MoonflowerReach => HubAt(NodeKind.Moonflower, 3) ? Tuning.HubMoonflowerReach : Tuning.MoonflowerReach;

    // Each economy step: the wild flowers within MoonflowerReach links of a Moonflower make MoonlitBonus more.
    void MarkMoonlit()
    {
        bool any = false;
        foreach (var n in Nodes)
        {
            n.Moonlit = false;
            n.MoonlitBonus = Tuning.MoonlitBonus + hubMoonlit;
            any |= n.Spec.WildBoost > 0;
        }
        if (!any) return;
        foreach (var moonflower in Nodes.Where(n => n.Spec.WildBoost > 0).ToList())
            foreach (var n in Within(moonflower, MoonflowerReach))
                if (n.Spec.IsWild && n.Spec.TakesBuffs) n.Moonlit = true; // (only those that use a boost: Jeremy, Nov 3)
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
            Boost = boosts.GetValueOrDefault(spec.Kind, Boost.None),
        };
        node.MaxLinks = parent == null ? Capacity.CoreLinks : spec.MaxLinks + node.Boost.Links;
        Nodes.Add(node);
        // A new seedling takes up an evolution left waiting by one pruned before it bloomed.
        if (spec.Kind == NodeKind.Seedling && !loading) node.Evolution = Waiting.FirstOrDefault();
        if (parent != null && !loading) Tracking.Grew(spec, Nodes.Count);
        // A Comet Bloom bursts every kind of blob as it appears.
        if (spec.EmergeBurst > 0 && !loading) node.Blob = spec.EmergeBurst;
        // A new Hub wakes the levels bought on the last one.
        if (spec.Kind == NodeKind.Hub && !loading) UpdateBoosts();
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
        MarkMoonlit();
        MarkNebulous();
        CometActive = Nodes.Any(IsActive);
        foreach (var node in Nodes)
        {
            if (node.Spec.IsFruit)
            {
                // A glow sends buff blobs, as a small Buffer.
                node.Blob += BlobsPerHour(node) * hours;
                continue;
            }
            float made = ProductionPerSecond(node), cost = UpkeepPerSecond(node);
            if (node.HoldsGardenPower) stored += node.Power;
            production += made;
            upkeep += cost;
            node.Life += seconds;
            node.Power = Math.Max(0, node.Power + (made - cost) * dt);
            node.Blob += BlobsPerHour(node) * hours;
            node.Buff = Math.Max(0, node.Buff - dt);
            if (node.Spec.LifeSeconds > 0 && node.Life >= LifeOf(node)) ending.Add(node);
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
                    // A Comet Bloom's burst: gene and growth blobs, mixed as a lab's and a grove's are.
                    var kind = flower.Spec.Sends == BlobKind.All ? CometBlob((int)MathF.Round(flower.Blob)) : flower.Spec.Sends;
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
            if (node.Spec.BurstShare > 0)
            {
                // A Pulsar: nothing until it holds its BurstFill, then its BurstShare of its store at once (Nov 3).
                if (node.Power >= MakeCap(node) - 1e-3f && SendTo(node, node.Power - fed, burst: true) is { } to)
                    transfers.Add((node, to, Math.Min(node.Power - fed, node.Store * node.Spec.BurstShare)));
            }
            // (A part sending in TransferSplits shares, a Tendril, sends its first share now, the rest below.)
            else if (SendTo(node, node.Power - fed) is { } to) transfers.Add((node, to, Clump(node) / node.Spec.TransferSplits));
        }
        MoveClumps(seconds);
        // The parts that send more than once a step send again, picking afresh from the fills as they now stand.
        for (int split = 1; split < maxSplits; split++)
        {
            transfers.Clear();
            foreach (var node in Nodes)
                if (node.Spec.TransferSplits > split && SendTo(node, node.Power) is { } to)
                    transfers.Add((node, to, Clump(node) / node.Spec.TransferSplits));
            MoveClumps(seconds);
        }
        CheckSeedlings();
        if (trackTraffic) ExpireTraffic();
        Tracking.Age += seconds;
        Tracking.History.Record(Tracking.Age, seconds, stored, production, upkeep);
        return grew;
    }

    static readonly int maxSplits = NodeSpecs.All.Values.Max(s => s.TransferSplits);

    // The transfers picked move together, each lighting its link (a flash at a random moment in the step).
    void MoveClumps(double seconds)
    {
        foreach (var (from, to, clump) in transfers)
        {
            from.Power -= clump;
            to.Power += clump * to.SeedBoost; // (power reaching a seedling near a Nebula Bloom counts double)
            if (!ShowBlobs) continue;
            var link = linkToParent[to.Parent == from ? to : from];
            link.FlowsToTarget = link.Target == to;
            link.FlashIn = (float)(looks.NextDouble() * seconds);
        }
    }

    // Which blob a Comet Bloom's burst sends, by how many are left: gene and growth blobs in the share a lab's and a
    // grove's blob rates make them.
    static BlobKind CometBlob(int left)
    {
        int genes = (int)NodeSpecs.All[NodeKind.Lab].BlobsPerHour, growth = (int)NodeSpecs.All[NodeKind.Grove].BlobsPerHour;
        return left % (genes + growth) < genes ? BlobKind.Genes : BlobKind.Growth;
    }

    // Where a part sends a clump of power this step, if anywhere (the original's attemptTransferPower): once it's
    // fuller than SendFill and has a whole clump to spare, to the neighbour (not fruit) with the lowest fill for its
    // weight among those less full than itself (fills past full count as full; an Ember's counted against its PowerCap,
    // so it isn't fed past it and sends on what's over it). A part with no transfer rate (a seedling) sends nothing. A
    // Pulsar's burst (see EconomyStep) needs only somewhere to go.
    PlantNode? SendTo(PlantNode node, float spare, bool burst = false)
    {
        if (!burst && (FlowFill(node) <= Tuning.SendFill || Clump(node) <= 0 || spare < Clump(node))) return null;
        float fill = Math.Min(1, FlowFill(node));
        PlantNode? best = null;
        float bestWeighed = float.MaxValue;
        if (node.Parent != null) Consider(node.Parent);
        foreach (var branch in node.Branches) Consider(branch);
        return best;

        void Consider(PlantNode other)
        {
            float otherFill = FlowFill(other), weighed = otherFill / other.Spec.PowerWeight;
            if (Math.Min(1, otherFill) >= fill || weighed >= bestWeighed) return;
            best = other;
            bestWeighed = weighed;
        }
    }

    // The clump of power a part sends in one transfer: its rate now times the step's length, so the rate holds
    // whatever the step (a longer step sends bigger, rarer clumps).
    public static float Clump(PlantNode node) => SendsPerSecond(node) * (float)Tuning.EconomyStepSeconds;

    // How fast a part sends power on now: its transfer rate times its fill, up to full (Jeremy, Nov 3: a stem sending
    // 30/s sends 15/s at half full), as everything follows its fill.
    public static float SendsPerSecond(PlantNode node) =>
        TransferPerSecond(node) * (Tuning.TransferFollowsFill ? Math.Clamp(FlowFill(node), 0, 1) : 1);

    // How fast a part can send power on: its kind's rate, raised by evolutions (Long Stemmed, Twin Stars, Second Path).
    public static float TransferPerSecond(PlantNode node) =>
        Math.Max(node.Spec.PowerTransferPerSecond, LeafGen(node)) * node.Boost.Transfer;

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

    // Power a part makes per second now: in proportion to its fill, nothing once full.
    public static float ProductionPerSecond(PlantNode node) =>
        node.Power >= MakeCap(node) ? 0 : FullProductionPerSecond(node) * PowerOutput(node);

    // How much a part makes power up to: its PowerCap, or a bursting part's (a Pulsar's) BurstFill of it.
    public static float MakeCap(PlantNode node) => PowerCap(node) * node.Spec.BurstFill;

    // The most power a part can hold: its store, but for one that dims (an Ember) its store times its Brightness, so
    // as its life runs out its fill, and with it its look and what it makes, fall in proportion (Jeremy, Nov 1).
    public static float PowerCap(PlantNode node) => node.Spec.Dims ? node.Store * Brightness(node) : node.Store;

    // A part's fill against its PowerCap, for where power flows (past it counts as full).
    static float FlowFill(PlantNode node) => PowerCap(node) is var cap && cap > 0 ? node.Power / cap : node.Power > 0 ? 2 : 1;

    // A power maker's share of its full rate: its Output, or with a floor (the generators' Hub milestone) that share of
    // its full rate whatever its fill, the rest following its fill (50-100%).
    static float PowerOutput(PlantNode node) =>
        node.Boost.OutputFloor + (1 - node.Boost.OutputFloor) * Output(node);

    // What the cards show it making: its full rate once full (when it makes nothing, idling, not starved), else
    // what it makes now.
    public static float ShownProductionPerSecond(PlantNode node) =>
        node.Power >= MakeCap(node) ? FullProductionPerSecond(node) : ProductionPerSecond(node);

    // What a part makes per second when full (its fruit adding a little each: see FruitBonus; evolutions more: see
    // Boosts). An Ember's rate stays: it makes less as it dims because it can hold less (see PowerCap).
    public static float FullProductionPerSecond(PlantNode node) =>
        (Math.Max(0, node.Spec.PowerPerSecond) * FruitBonus(node) + LeafGen(node)) * Boosted(node) * node.Boost.Gen;

    // The Lichen's Hub milestone: once every leaf it can hold is out, power it makes a second (and sends on at).
    static float LeafGen(PlantNode node) =>
        node.Boost.LeafGen > 0 && node.Fruits.Count(f => f.Spec.Kind == NodeKind.Leaf) >= node.MaxFruits ? node.Boost.LeafGen : 0;

    // A part's own fruit raise its output, each by its FlowerBonus (times the part's FruitBonusScale: a Wild Lab's
    // count double).
    static float FruitBonus(PlantNode node)
    {
        float bonus = 1;
        foreach (var fruit in node.Fruits)
        {
            bonus += fruit.Spec.FlowerBonus * node.Spec.FruitBonusScale;
            if (fruit.Spec.Kind == NodeKind.Leaf) bonus += node.Boost.LeafBonus; // (a Lichen's, with its Hub milestone)
        }
        return bonus;
    }

    // How much of its store a wild flower that dims (an Ember) can still hold: 1 as it grows, falling in a straight line
    // to 0 as its life ends (see PowerCap). Others: 1.
    public static float Brightness(PlantNode node)
    {
        if (!node.Spec.Dims) return 1;
        // (A Comet Bloom keeps full while active, then fades over the rest of its life.)
        double active = ActiveOf(node), life = LifeOf(node);
        if (node.Life <= active) return 1;
        return 1 - (float)Math.Clamp((node.Life - active) / Math.Max(1, life - active), 0, 1);
    }

    // Whether a part is in its active time (a Comet Bloom's first CometActiveSeconds).
    public static bool IsActive(PlantNode node) => node.Spec.ActiveSeconds > 0 && node.Life < ActiveOf(node);

    // Whether a Comet Bloom is active: every gene and growth blob is then worth CometBlobValue times as much, and
    // buffs last CometBuffTime times as long (Jeremy, Nov 3). Worked out each economy step.
    public bool CometActive { get; private set; }

    // How long a wild flower that ends itself lives: its spec's, times its Hub milestone's (an Ember's longer, a Comet
    // Bloom's shorter).
    // (A Comet Bloom: its active time, longer with its Hub path, then its fading time, shorter with its milestone.)
    public static double LifeOf(PlantNode node) =>
        ActiveOf(node) + (node.Spec.LifeSeconds - node.Spec.ActiveSeconds) * node.Boost.Life;

    // How long a part stays active after it appears (a Comet Bloom), with its Hub path's added seconds.
    public static double ActiveOf(PlantNode node) => node.Spec.ActiveSeconds <= 0 ? 0 : node.Spec.ActiveSeconds + node.Boost.Active;

    // Power a part costs per second now: its full upkeep times its fill (past full, more).
    public static float UpkeepPerSecond(PlantNode node) => FullUpkeepPerSecond(node) * Math.Max(0, node.Fill);

    // What a part costs per second when full: times 1 plus its fruits' FlowerPowerFactor (each leaf takes off a quarter),
    // and any evolution's change (Rapid Evolution's labs).
    public static float FullUpkeepPerSecond(PlantNode node)
    {
        float upkeep = Math.Max(0, -node.Spec.PowerPerSecond) * node.Boost.Upkeep;
        float factor = 1;
        foreach (var fruit in node.Fruits)
            factor += fruit.Spec.FlowerPowerFactor;
        return upkeep * factor;
    }

    // Blobs a flower makes per real hour now: gene blobs from a lab, growth blobs from a grove, in proportion to its
    // fill (their fruit adding a little).
    public static float BlobsPerHour(PlantNode node) => FullBlobsPerHour(node) * Output(node);

    // The same when full.
    public static float FullBlobsPerHour(PlantNode node) =>
        node.Spec.BlobsPerHour * FruitBonus(node) * Boosted(node) * node.Boost.Blobs;

    // A part a Buffer'"'"'s buff blob passed lately makes BuffBonus more.
    static float Boosted(PlantNode node) => (node.Buff > 0 ? 1 + node.BuffBonus : 1) * (node.Moonlit ? 1 + node.MoonlitBonus : 1);

    // ==== BLOBS ==== \\

    // A blob's walk from its flower (DESIGN.md, Economy, Blobs): link by link at random, never straight back unless
    // at a dead end, until a part absorbs it (a gene blob: only the core) or BlobMaxHops links have passed. Returns
    // the part that absorbed it, if one did, and adds the parts it passed to route (from the flower), if given: only
    // a blob that will be shown needs one. Many thousands of blobs leave an hour, so it allocates nothing else.
    // A Resistor is a closed door from both sides: a blob never steps onto it. An Amplifier it passes doubles its value
    // AmplifyChance of the time, once a blob at most (Jeremy, Nov 3: x2 value, no longer a second blob; a triple with
    // its Hub milestone): noted in Amplified, which SendGenes and SendGrowth multiply by.
    PlantNode? Walk(PlantNode from, bool genes, ref Counts counts, List<PlantNode>? route, PlantNode? cameFrom = null,
        int hops = Tuning.BlobMaxHops)
    {
        Amplified = 1;
        route?.Add(from);
        var at = from;
        for (int hop = 0; hop < hops; hop++)
        {
            if (StepFrom(at, cameFrom, genes ? BlobKind.Genes : BlobKind.Growth) is not { } next)
                return null; // a lone core, or walled in by Resistors
            cameFrom = at;
            at = next;
            route?.Add(at);
            if (TrackTraffic) CountTraffic(cameFrom, at, genes ? BlobKind.Genes : BlobKind.Growth);
            if (genes ? CatchesGenes(at) : Absorbs(at, counts)) return at;
            if (Amplified == 1 && at.Spec.DoublesBlobs)
            {
                // (With the Amplifier's Hub milestone, HubTripleChance of the time it's a triple, within that chance.)
                double roll = rng.NextDouble();
                if (roll < AmplifyChance) Amplified = HubAt(NodeKind.Amplifier, 3) && roll < Tuning.HubTripleChance ? 3 : 2;
            }
        }
        return null;
    }

    // Blob traffic (build mode's blob-path view, Jeremy, Nov 3): while TrackTraffic, every link a gene or growth blob
    // steps along counts one (keyed by the link's outer part and the blob's kind: genes, growth or buff) for
    // Tuning.TrafficSeconds of growth time, then drops out (Jeremy, Nov 3: each step expires, rather than a decay), so it
    // shows where blobs went in that window. Off, nothing is counted (and what was is dropped).
    readonly Dictionary<(PlantNode Outer, BlobKind Kind), Queue<double>> Traffic = new();

    // How many blob steps of this kind took the link to this part in the window.
    public int TrafficOf(PlantNode outer, BlobKind kind) => Traffic.TryGetValue((outer, kind), out var steps) ? steps.Count : 0;

    // The most any link had of this kind in the window (0: none).
    public int BusiestTraffic(BlobKind kind)
    {
        int most = 0;
        foreach (var ((_, k), steps) in Traffic) if (k == kind) most = Math.Max(most, steps.Count);
        return most;
    }
    bool trackTraffic;
    public bool TrackTraffic
    {
        get => trackTraffic;
        set
        {
            trackTraffic = value;
            if (!value) Traffic.Clear();
        }
    }

    void CountTraffic(PlantNode a, PlantNode b, BlobKind kind)
    {
        var key = (b.Parent == a ? b : a, kind);
        if (!Traffic.TryGetValue(key, out var steps)) Traffic[key] = steps = new Queue<double>();
        steps.Enqueue(Tracking.Age);
    }

    // Drops the steps older than the window, and the links pruned since.
    void ExpireTraffic()
    {
        if (Traffic.Count == 0) return;
        double since = Tracking.Age - Tuning.TrafficSeconds;
        foreach (var key in Traffic.Keys.ToList())
        {
            var steps = Traffic[key];
            while (steps.Count > 0 && steps.Peek() < since) steps.Dequeue();
            if (steps.Count == 0 || !linkToParent.ContainsKey(key.Outer)) Traffic.Remove(key);
        }
    }

    // What the last walk's blob is worth for its Amplifier: 1, or 2 (3 tripled) once one amplified it.
    float Amplified = 1;

    // How often an Amplifier doubles a blob: AmplifyChance, plus its Hub levels' share.
    float AmplifyChance => Tuning.AmplifyChance + amplifyBonus;

    // One link of a blob's walk, from at (having come from cameFrom): a link at random, never into a Resistor, and
    // never straight back unless at a dead end. With Smart Particles, SmartChance of the steps, never into a dead end
    // either, unless the blob could be taken in there (see Wanders), or there's nowhere else to go. Null if there's
    // nowhere to go.
    PlantNode? StepFrom(PlantNode at, PlantNode? cameFrom, BlobKind kind)
    {
        bool smart = SmartAt(at, kind) && (SmartChance >= 1 || rng.NextDouble() < SmartChance);
        int open = Open(at, null, kind, smart);
        if (open == 0) return null;
        var skip = open > 1 ? cameFrom : null;
        return Option(at, rng.Next(skip != null ? Open(at, skip, kind, smart) : open), skip, kind, smart);
    }

    // Whether Smart Particles can steer a blob at this part: bloomed, and with somewhere to go that isn't a dead end.
    public bool SmartAt(PlantNode at, BlobKind kind) => SmartChance > 0 && Open(at, null, kind, true) > 0;

    // Whether a blob at one part may step onto a neighbour: never a Resistor; with smart on, never a dead end (a part
    // with no other link a blob could take) that wouldn't take it in: a gene blob's only taker is the core, a growth
    // blob's a part with something forming or room to start something, and a buff blob's a part not buffed already.
    public bool Wanders(PlantNode at, PlantNode to, BlobKind kind, bool smart)
    {
        if (to.Spec.BlocksBlobs) return false;
        if (!smart || !DeadEnd(to, at)) return true;
        return kind switch
        {
            BlobKind.Genes => CatchesGenes(to),
            BlobKind.Growth => to.Forming != null || CanStartFruit(to) || (PassiveGrowth && !TutorialHoldsBuds && to.CanBranch),
            _ => to.Spec.TakesBuffs && to.Buff < Tuning.BuffRefreshSeconds, // (a dead end that would take it)
        };
    }

    // A part with nowhere for a blob to go on but back where it came from.
    static bool DeadEnd(PlantNode part, PlantNode from)
    {
        foreach (var branch in part.Branches)
            if (branch != from && !branch.Spec.BlocksBlobs) return false;
        return part.Parent is not { } parent || parent == from || parent.Spec.BlocksBlobs;
    }

    // How many links a blob could take from a part, passing over skip and any Resistor (or dead end: see Wanders).
    int Open(PlantNode at, PlantNode? skip, BlobKind kind, bool smart)
    {
        int open = 0;
        foreach (var branch in at.Branches)
            if (branch != skip && Wanders(at, branch, kind, smart)) open++;
        if (at.Parent is { } parent && parent != skip && Wanders(at, parent, kind, smart)) open++;
        return open;
    }

    // The pick-th of those: a part's branches and then its parent.
    PlantNode Option(PlantNode at, int pick, PlantNode? skip, BlobKind kind, bool smart)
    {
        foreach (var branch in at.Branches)
        {
            if (branch == skip || !Wanders(at, branch, kind, smart)) continue;
            if (pick-- == 0) return branch;
        }
        return at.Parent!;
    }

    // One blob leaves a flower, of this kind. Returns how many parts grew from it (0 or 1).
    int Send(PlantNode flower, BlobKind kind, ref Counts counts)
    {
        switch (kind)
        {
            case BlobKind.Genes:
                SendGenes(flower, ref counts, (int)MathF.Round(Tuning.BlobMaxHops * flower.Boost.Reach));
                return 0;
            case BlobKind.Growth:
                return SendGrowth(flower, ref counts);
            case BlobKind.Buff:
                SendBuff(flower);
                return 0;
            default:
                return 0;
        }
    }

    // A blob is shown following its route, unless in a catch-up or with too many already in flight.
    List<PlantNode>? RouteIfShown() => ShowBlobs && Motes.Count < Tuning.MaxMotes ? new List<PlantNode>() : null;

    Mote? Show(List<PlantNode>? route, PlantNode flower)
    {
        if (route == null || route.Count < 2) return null;
        var mote = new Mote(route, flower.Spec.Rgb) { Speed = flower.Boost.Reach };
        Motes.Add(mote);
        return mote;
    }

    // One gene blob's walk from this lab, with nothing shown or delivered: whether it found the core (tests check
    // GardenStats.GeneReach against it).
    internal bool WalkGene(PlantNode lab)
    {
        var counts = CountKinds();
        return Walk(lab, genes: true, ref counts, null) == Core;
    }

    // The parts that take gene blobs in: the core, and the Hub (into its own pool) until every path is complete; then
    // gene blobs pass through it as through any part.
    public bool CatchesGenes(PlantNode part) => part == Core || (part.IsHub && !HubComplete);

    // What a blob leaving this flower carries, as a share of a basic one (BlobGenes, BlobGrowth): its kind's BlobValue
    // (a Lumber Grove's 0.2 growth, a Wild Lab's 2 genes), doubled while a Comet Bloom is active (Jeremy, Nov 3).
    float BlobValue(PlantNode flower) => ValueOf(flower) * (CometActive ? Tuning.CometBlobValue : 1);

    // The share of a faded gene blob's genes the core still gets: the lab's Hub milestones (Nov 3), every gene blob's.
    public float FadedGenes => HubAt(NodeKind.Lab, 5) ? Tuning.HubFadedGenes5 : HubAt(NodeKind.Lab, 3) ? Tuning.HubFadedGenes3 : 0;

    // What a flower's blob carries as a share of a basic one, but for a Comet Bloom: its kind's, times its evolutions'
    // (Rapid Evolution).
    public static float ValueOf(PlantNode flower) => flower.Spec.BlobValue * flower.Boost.Value;

    // Resonant Frequency's addition to every buff's bonus (worked out in UpdateBoosts).
    public float ResonantBonus { get; private set; }

    // A lab's gene blob leaves. Only the core absorbs it, or the Hub; its genes (its value, times an Amplifier's) count
    // at the core (or in the Hub's pool) in this same step, so a catch-up (which runs no physics) and a live session
    // agree, and the blob shown is their journey made visible. A blob that never finds either fades, its genes lost:
    // labs near the core waste fewer.
    void SendGenes(PlantNode lab, ref Counts counts, int hops)
    {
        var route = RouteIfShown();
        var catcher = Walk(lab, genes: true, ref counts, route, null, hops);
        float genes = Tuning.BlobGenes * BlobValue(lab) * Amplified;
        if (catcher == Core) Deliver(genes);
        else if (catcher != null) CatchAtHub(genes);
        else if (FadedGenes > 0) Deliver(genes * FadedGenes); // (the lab's Hub milestones)
        if (Show(route, lab) is { } mote) mote.Absorbed = catcher != null;
    }

    // A grove's growth blob leaves. Where it goes and what it does are settled now, from the garden's own dice, so a
    // catch-up and a live session agree: the part that absorbs it forms more of its fruit or bud (by the blob's growth:
    // its value times an Amplifier's), and a part that completes is added now, hidden (its Age below zero) until the
    // blob, shown following the same walk, arrives. Returns how many parts grew. Counts are the step's, kept up to date
    // as buds start.
    int SendGrowth(PlantNode grove, ref Counts counts)
    {
        var route = RouteIfShown();
        var absorber = Walk(grove, genes: false, ref counts, route);
        if (absorber == null)
        {
            Show(route, grove);
            return 0;
        }
        float growth = Tuning.BlobGrowth * BlobValue(grove) * Amplified;
        float wildTimes = grove.Spec.Kind == NodeKind.SeedPod && HubAt(NodeKind.SeedPod, 3) ? Tuning.HubSeedPodWild : 1;
        var grown = Feed(absorber, ref counts, growth, wildTimes);
        if (Show(route, grove) is { } mote)
        {
            mote.Growth = growth;
            mote.Absorbed = true;
            mote.Completes = grown != null;
            if (grown != null) grown.Age = -(route!.Count - 1) * Tuning.MoteLinkSeconds;
        }
        else absorber.FormingLook = absorber.Growth;
        return grown != null ? 1 : 0;
    }

    // A buff blob leaves a Buffer, or a glow fruit (DESIGN.md, Wild flowers). It wanders as the others do, for up to
    // BufferReach links (BufferAmplifiedReach more once it passes an Amplifier), and the first part on its way that
    // takes it is buffed and uses it up (Jeremy, Nov 4): a part that uses a buff and has none, or less than
    // BuffRefreshSeconds of one left. Parts that don't use a buff, or are buffed for longer, pass it on. A glow's
    // starts on the part it hangs from. Settled now, as the others are; shown flashing the part it buffed, or fading
    // where it ends unused.
    void SendBuff(PlantNode from)
    {
        var route = RouteIfShown();
        route?.Add(from);
        PlantNode? cameFrom = null;
        var at = from;
        bool used = false;
        if (from.Spec.IsFruit && from.Parent is { } parent)
        {
            at = parent;
            route?.Add(at);
            used = TakesBuff(at);
        }
        int reach = Tuning.BufferReach;
        bool amplified = false;
        for (int hop = 0; hop < reach && !used; hop++)
        {
            if (StepFrom(at, cameFrom, BlobKind.Buff) is not { } next) break;
            cameFrom = at;
            at = next;
            route?.Add(at);
            if (TrackTraffic) CountTraffic(cameFrom, at, BlobKind.Buff);
            used = TakesBuff(at);
            if (!amplified && at.Spec.DoublesBlobs)
            {
                amplified = true;
                reach += Tuning.BufferAmplifiedReach;
            }
        }
        if (Show(route, from) is { } mote) mote.Absorbed = used;
    }

    // Whether a buff blob here is used up: a part that uses a buff, with none or less than BuffRefreshSeconds of one
    // left, is buffed by it.
    bool TakesBuff(PlantNode part)
    {
        if (!part.Spec.TakesBuffs || part.Buff >= Tuning.BuffRefreshSeconds) return false;
        Buffs(part);
        return true;
    }

    // A buff blob passes a part that uses it: for BuffSeconds (HubBuffSeconds more with the Buffer's milestone) it
    // makes BuffBonus more, plus Resonant Frequency's and the Buffer's Hub path's, all added (Jeremy, Nov 3).
    // For tests: as if a buff blob were used up on this part; and whether one arriving here would be.
    internal void BuffForTest(PlantNode part) => Buffs(part);
    internal bool BuffMoteForTest(PlantNode part) => TakesBuff(part);

    void Buffs(PlantNode part)
    {
        if (!part.Spec.TakesBuffs) return; // (it passes untouched: Jeremy, Nov 3)
        part.Buff = (Tuning.BuffSeconds + (HubAt(NodeKind.Buffer, 3) ? Tuning.HubBuffSeconds : 0))
            * (CometActive ? Tuning.CometBuffTime : 1);
        // (They add up, in points of the output: 150% base, +50 a Resonant Frequency level, +10 a Buffer path level.)
        part.BuffBonus = Tuning.BuffBonus + ResonantBonus + hubBuffStrength;
    }

    // A part with something forming always absorbs a growth blob; one with room to start a fruit or a bud does
    // AbsorbChance of the time; a full one never does.
    bool Absorbs(PlantNode node, Counts counts)
    {
        if (node.Forming != null) return true;
        return (CanStartFruit(node) || CanBud(node, counts)) && rng.NextDouble() < Tuning.AbsorbChance;
    }

    // Growth doesn't wait for an undo: a fruit or bud that takes the room a prune freed just ends the chance to undo it
    // (see CanUndoPrune), as a planted bud does. Passive buds wait for PassiveGrowth.
    bool CanStartFruit(PlantNode node) => node.CanFruit && Nodes.Count < Tuning.MaxNodes;

    // Whether BranchKinds has anything for this part, without listing them: the core grows only stems, any other part
    // a lab at least (labs are uncapped).
    bool CanBud(PlantNode node, Counts counts) =>
        PassiveGrowth && !TutorialHoldsBuds && Nodes.Count < Tuning.MaxNodes && node.CanBranch
        && (node.Depth >= 1 || HasRoomFor(NodeKind.Stem, counts));

    // A growth blob absorbed: a part with nothing forming starts something (a bud BudChance of the time where a
    // branch could grow, its kind rolled now; else a fruit), then the blob's growth is added, and what's forming
    // completes once it has grown enough. Returns the part that grew, if one did.
    // (wildTimes: how much likelier a bud the blob starts is to grow wild: a Seed Pod's, with its Hub milestone.)
    PlantNode? Feed(PlantNode node, ref Counts counts, float growth, float wildTimes = 1)
    {
        if (node.Forming == null)
        {
            bool bud = CanBud(node, counts) && (!CanStartFruit(node) || rng.NextDouble() < Tuning.BudChance);
            node.Forming = bud ? GrowsWild(node, Roll(BranchKinds(node, counts)), passive: true, counts, wildTimes) : node.Spec.Fruit;
            if (bud)
            {
                counts = counts.With(node.Forming!.Value);
                Tracking.PassiveBuds++;
                Tracking.First("passive", "First passive bud started");
            }
            node.Growth = 0;
            node.FormingLook = 0;
        }
        node.Growth += growth * TutorialGrowth;
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
            float growth = Tuning.BudGrowth / Tuning.BudSelfGrowSeconds * Output(node) * dt * node.Boost.BudGrow;
            if (groveless && node.Forming == NodeKind.Grove) growth *= Tuning.FirstGroveSpeed;
            growth *= TutorialGrowth;
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

    // ==== SEEDLINGS AND EVOLUTIONS ==== \\
    // DESIGN.md, Seedlings and evolutions. A seedling is planted by hand (never passively) where a seedling slot is
    // free. It only takes power in, from its neighbours (no upkeep, no transfer of its own). Full, it waits for the
    // player to choose one of the evolutions it offers; that raises its store to the evolution's target, and full again
    // it blooms: the effect applies for good and the seedling becomes the evolution's wild flower (the Almanac lists
    // the evolutions from Evolved). A slot stays used once its evolution is chosen, so each slot is one evolution:
    // the slots are counted from Evolved.

    // The evolutions chosen, in the order chosen, bloomed or still growing: the garden's record of them (the
    // Almanac's list) and its seedling slots used.
    public readonly List<Evolution> Evolved = new();
    // Smart Particles' level: how often a blob's step avoids a dead end (see Wanders): 0 until it blooms.
    public float SmartChance { get; private set; }
    // The bloomed evolutions' effects on each basic kind (see UpdateBoosts): every part of the kind carries its Boost.
    readonly Dictionary<NodeKind, Boost> boosts = new();

    public int Blooms => Evolved.Count(e => e.Bloomed);

    // Evolutions chosen whose seedling was pruned before it bloomed: the next seedling to emerge takes one up, starting
    // empty (see Add).
    public IEnumerable<Evolution> Waiting => Evolved.Where(e => !e.Bloomed && !Nodes.Any(n => n.Evolution == e));

    // Seedling slots used: every evolution chosen, every seedling yet to choose, and every
    // seedling bud forming; less the evolutions waiting, whose slots the next seedlings planted take up.
    public int SeedlingsUsed
    {
        get
        {
            int used = Evolved.Count - Waiting.Count();
            foreach (var n in Nodes)
            {
                if (n.IsSeedling && n.Evolution == null) used++;
                if (n.Forming == NodeKind.Seedling) used++;
            }
            return used;
        }
    }

    public int FreeSeedlingSlots => Math.Max(0, Capacity.Seedlings - SeedlingsUsed);

    // How many times an evolution has been chosen (bloomed or not): its next level is one more.
    public int Taken(EvolutionKind kind) => Evolved.Count(e => e.Kind == kind);

    // The target the next evolution chosen will have: FirstBloom for the garden's first, BloomGrowth times as much for
    // each one chosen before it (1,000, 5,000, 25,000 ...).
    public double NextTarget => Tuning.FirstBloom * Math.Pow(Tuning.BloomGrowth, Evolved.Count);

    // The evolutions a seedling can offer now: each up to its repeat limit (counting those still growing), and none a
    // second time until the garden has RepeatsAfter evolutions; a Late one (Long Stemmed) not at all until then.
    public List<EvolutionKind> Offerable()
    {
        bool repeats = Evolved.Count >= Tuning.RepeatsAfter;
        return Enum.GetValues<EvolutionKind>().Where(k =>
        {
            var spec = Evolutions.All[k];
            int taken = Taken(k);
            return !spec.Retired && taken < spec.MaxLevel && (repeats || (taken == 0 && !spec.Late));
        }).ToList();
    }

    // Up to ChoiceCount of them, at random (the garden's own dice, so a catch-up rolls the same).
    List<EvolutionKind> RollOffers()
    {
        var pool = Offerable();
        var offers = new List<EvolutionKind>();
        while (offers.Count < Tuning.ChoiceCount && pool.Count > 0)
        {
            int i = rng.Next(pool.Count);
            offers.Add(pool[i]);
            pool.RemoveAt(i);
        }
        return offers;
    }

    // Each economy step: the seedlings within NebulaReach links of a Nebula Bloom take in power at its SeedlingBoost.
    void MarkNebulous()
    {
        bool any = false;
        foreach (var n in Nodes)
        {
            if (n.IsSeedling) n.SeedBoost = 1;
            any |= n.Spec.SeedlingBoost > 0;
        }
        if (!any) return;
        foreach (var nebula in Nodes.Where(n => n.Spec.SeedlingBoost > 0).ToList())
            foreach (var n in Within(nebula, Tuning.NebulaReach))
                if (n.IsSeedling) n.SeedBoost = Math.Max(n.SeedBoost, HubAt(NodeKind.NebulaBloom, 3) ? Tuning.HubNebulaFill : nebula.Spec.SeedlingBoost);
    }

    // After each economy step's transfers: a seedling that's full (its power at its store) rolls the evolutions it offers,
    // the first time, and waits for the player's choice; one full of its chosen evolution's target blooms.
    void CheckSeedlings()
    {
        foreach (var node in Nodes)
        {
            if (!node.IsSeedling || node.Power < node.Store - 1e-3f) continue;
            if (node.Evolution != null) blooming.Add(node);
            else node.Offers ??= RollOffers();
        }
        if (blooming.Count == 0) return;
        foreach (var node in blooming) Bloom(node);
        blooming.Clear();
    }

    // The player picks one of a waiting seedling's offers (DESIGN.md, Tending commands: "Select evolution"): its store
    // rises to NextTarget (so it fills on from where it is), and the journal notes it. Returns whether it was chosen.
    public bool Choose(PlantNode seedling, EvolutionKind kind)
    {
        if (!seedling.Choosing || !seedling.Offers!.Contains(kind) || !Nodes.Contains(seedling)
            || Taken(kind) >= Evolutions.All[kind].MaxLevel)
            return false;
        var evolution = new Evolution { Kind = kind, Level = Taken(kind) + 1, Target = NextTarget, ChosenAt = Tracking.Age };
        Evolved.Add(evolution);
        seedling.Evolution = evolution;
        seedling.Offers = null;
        Tracking.Log(JournalKind.Chosen, $"Evolution chosen: {evolution.Name}");
        return true;
    }


    // The flowers that bloomed since the view last looked, for its flash (it empties this; a catch-up shows none).
    public readonly List<PlantNode> JustBloomed = new();

    // A seedling full of its evolution's target blooms (see Apply): it becomes the evolution's wild flower where it
    // stands (full, from the power it held).
    void Bloom(PlantNode seedling)
    {
        var evolution = seedling.Evolution!;
        Apply(evolution);
        Tracking.Log(JournalKind.Bloomed, BloomedText(evolution));
        var flower = Replace(seedling, evolution.Spec.Becomes);
        if (ShowBlobs) JustBloomed.Add(flower);
    }

    // "Second Path bloomed into a Tendril".
    static string BloomedText(Evolution evolution)
    {
        string flower = NodeSpecs.All[evolution.Spec.Becomes].Name;
        return $"{evolution.Name} bloomed into {Tracking.Article(flower).ToLowerInvariant()} {flower}";
    }

    // An evolution blooms: its effect applies now and for good. Its caps rise at once (a core link, generators, the
    // wild flower limit), plus 1 on the wild flower limit for its flower, and 1 on its flower's base cap where the
    // flower takes that slot too (a Tendril a stem's, an Ember a generator's), so the flower costs nothing; an unlock
    // evolution's kind joins the pool; its multipliers join the boosts.
    void Apply(Evolution evolution)
    {
        evolution.Bloomed = true;
        evolution.BloomedAt = Tracking.Age;
        var effect = evolution.Effect;
        var becomes = evolution.Spec.Becomes;
        var caps = Capacity with
        {
            CoreLinks = Capacity.Add(Capacity.CoreLinks, effect.CoreLinks),
            Generators = Capacity.Add(Capacity.Generators, effect.Generators),
            WildFlowers = Capacity.Add(Capacity.WildFlowers, effect.WildFlowers + 1),
        };
        Capacity = SlotOf(becomes) switch
        {
            NodeKind.Stem => caps with { Stems = Capacity.Add(caps.Stems, 1) },
            NodeKind.Generator => caps with { Generators = Capacity.Add(caps.Generators, 1) },
            _ => caps,
        };
        if (evolution.Spec.Unlocks) UnlockWild(becomes);
        UpdateBoosts();
    }

    // Respec (Jeremy, Nov 3): a bloomed evolution taken back. Its effect and the caps it raised go (Apply in reverse;
    // the garden may be left over a cap, and stays so until the player prunes), an unlock goes if no other bloomed
    // evolution of its kind remains, and it leaves the record, freeing its seedling slot for a new seedling (whose
    // bloom target falls back a step, as the record is shorter). Later levels of its kind step down a level. Its
    // flower stays as it is. Returns whether it was taken back.
    public bool Respec(Evolution evolution)
    {
        if (!evolution.Bloomed || !Evolved.Contains(evolution)) return false;
        Evolved.Remove(evolution);
        var effect = evolution.Effect;
        var becomes = evolution.Spec.Becomes;
        var caps = Capacity with
        {
            CoreLinks = Capacity.Less(Capacity.CoreLinks, effect.CoreLinks),
            Generators = Capacity.Less(Capacity.Generators, effect.Generators),
            WildFlowers = Capacity.Less(Capacity.WildFlowers, effect.WildFlowers + 1),
        };
        Capacity = SlotOf(becomes) switch
        {
            NodeKind.Stem => caps with { Stems = Capacity.Less(caps.Stems, 1) },
            NodeKind.Generator => caps with { Generators = Capacity.Less(caps.Generators, 1) },
            _ => caps,
        };
        if (evolution.Spec.Unlocks && !Evolved.Any(e => e.Bloomed && e.Spec.Unlocks && e.Spec.Becomes == becomes))
            WildUnlocked.Remove(becomes);
        int level = 1;
        foreach (var e in Evolved.Where(e => e.Kind == evolution.Kind)) e.Level = level++;
        Tracking.Log(JournalKind.Respec, $"Evolution taken back: {evolution.Name}");
        UpdateBoosts();
        return true;
    }

    // A basic kind and the wild flowers grown from it (a lab's: the Wild Lab and the Lichen).
    static IEnumerable<NodeKind> Family(NodeKind basic) =>
        NodeSpecs.All.Values.Where(s => s.Kind == basic || s.Wild?.Base == basic).Select(s => s.Kind);

    // The bloomed evolutions' effects on each basic kind (the lab's and the generator's on their wild flowers too):
    // each evolution at its bloomed level (its levels adding up: see EvolutionLevel.At), different evolutions
    // multiplied together; and Smart Particles' chance. Then, while a Hub is planted, its levels (see The Hub): every
    // path's shares a level, summed by kind and axis (a wild flower's family too), multiplied in, and the milestones
    // reached. The Hub's own upkeep rises with every level bought, dormant or not. Every part takes its kind's, and its
    // links beyond its spec's.
    void UpdateBoosts()
    {
        boosts.Clear();
        SmartChance = 0;
        ResonantBonus = 0;
        void Raise(NodeKind kind, Func<Boost, Boost> by) => boosts[kind] = by(boosts.GetValueOrDefault(kind, Boost.None));
        foreach (var bloomed in Evolved.Where(e => e.Bloomed).GroupBy(e => e.Kind))
        {
            var x = Evolutions.All[bloomed.Key].Effect.At(bloomed.Count());
            Raise(NodeKind.Core, b => b with
            {
                Gen = b.Gen * x.CoreGen, Transfer = b.Transfer * x.CoreTransfer, Store = b.Store * x.CoreStore,
            });
            Raise(NodeKind.Stem, b => b with { Fruits = b.Fruits + x.StemFruits });
            // (Jeremy's table, Nov 3: which kinds each reaches.)
            foreach (var stem in new[] { NodeKind.Stem, NodeKind.BigStem, NodeKind.Tendril })
                Raise(stem, b => b with { Transfer = b.Transfer * x.StemTransfer, Store = b.Store * x.StemStore });
            foreach (var lab in Family(NodeKind.Lab))
                Raise(lab, b => b with { Value = b.Value * x.LabBlobSize, Upkeep = b.Upkeep * x.LabUpkeep });
            foreach (var generator in Family(NodeKind.Generator))
                Raise(generator, b => b with
                {
                    Gen = b.Gen * x.GeneratorGen, Transfer = b.Transfer * x.AllGeneratorTransfer
                        * (generator == NodeKind.Pulsar ? 1 : x.GeneratorTransfer),
                });
            foreach (var kind in new[] { NodeKind.Pulsar, NodeKind.TwinStar, NodeKind.Ember })
                Raise(kind, b => b with { Gen = b.Gen * x.WildEffect });
            foreach (var kind in new[] { NodeKind.LumberGrove, NodeKind.SeedPod, NodeKind.WildLab, NodeKind.Lichen, NodeKind.Buffer })
                Raise(kind, b => b with { Blobs = b.Blobs * x.WildEffect });
            SmartChance = Math.Max(SmartChance, x.SmartChance);
            ResonantBonus += x.BuffBonus;
        }
        hubAwake = HubPlanted;
        HubComplete = Hubs.Paths.All(p => HubLevel(p.Kind) >= p.MaxLevel);
        amplifyBonus = 0;
        hubBuffStrength = 0;
        hubMoonlit = 0;
        if (hubAwake)
        {
            var shares = new Dictionary<(NodeKind Kind, HubAxis Axis), float>();
            foreach (var path in Hubs.Paths)
            {
                int level = HubLevel(path.Kind);
                if (level <= 0) continue;
                foreach (var e in path.Each) shares[(e.Kind, e.Axis)] = shares.GetValueOrDefault((e.Kind, e.Axis)) + e.Share * level;
                foreach (var m in path.Milestones.Where(m => m.Level <= level))
                    Raise(path.Kind, b => b with
                    {
                        Fruits = b.Fruits + m.Fruits, Links = b.Links + m.Links, Transfer = b.Transfer * m.Transfer,
                        Upkeep = b.Upkeep * m.Upkeep, Store = b.Store * m.Store, Life = b.Life * m.Life,
                        OutputFloor = Math.Max(b.OutputFloor, m.OutputFloor), Reach = b.Reach * m.Reach,
                        LeafBonus = b.LeafBonus + m.LeafBonus, BudGrow = b.BudGrow * m.BudGrow, StoreAdd = b.StoreAdd + m.StoreAdd,
                        LeafGen = b.LeafGen + m.LeafGen,
                    });
            }
            foreach (var ((kind, axis), share) in shares)
            {
                int whole = (int)MathF.Round(share);
                switch (axis)
                {
                    case HubAxis.Gen: Raise(kind, b => b with { Gen = b.Gen * (1 + share) }); break;
                    case HubAxis.Blobs: Raise(kind, b => b with { Blobs = b.Blobs * (1 + share) }); break;
                    case HubAxis.Transfer: Raise(kind, b => b with { Transfer = b.Transfer * (1 + share) }); break;
                    case HubAxis.Upkeep: Raise(kind, b => b with { Upkeep = b.Upkeep * Math.Max(0, 1 + share) }); break;
                    case HubAxis.Fruits: Raise(kind, b => b with { Fruits = b.Fruits + whole }); break;
                    case HubAxis.Links: Raise(kind, b => b with { Links = b.Links + whole }); break;
                    case HubAxis.Chance: amplifyBonus += share; break;
                    case HubAxis.BuffStrength: hubBuffStrength += share; break;
                    case HubAxis.Moonlit: hubMoonlit += share; break;
                    case HubAxis.Active: Raise(kind, b => b with { Active = b.Active + share }); break;
                }
            }
        }
        // (The Hub's own milestone cuts what each level adds.)
        float added = HubLevelsBought * Tuning.HubUpkeepPerLevel * (HubAt(NodeKind.Hub, 3) ? Tuning.HubUpkeepCut : 1);
        if (added > 0) Raise(NodeKind.Hub, b => b with { Upkeep = b.Upkeep * (1 + added / Tuning.HubUpkeep) });
        foreach (var node in Nodes)
        {
            node.Boost = boosts.GetValueOrDefault(node.Spec.Kind, Boost.None);
            if (node.Parent != null) node.MaxLinks = node.Spec.MaxLinks + node.Boost.Links;
        }
    }

    // Swaps a part for one of another kind where it stands (a seedling blooming): the same parent, place, motion,
    // power (up to the new store) and tie. (A seedling has nothing growing from it to carry over.)
    PlantNode Replace(PlantNode old, NodeKind kind)
    {
        var parent = old.Parent!;
        parent.Branches.Remove(old);
        Nodes.Remove(old);
        Links.RemoveAll(l => l.Target == old);
        linkToParent.Remove(old);
        Motes.RemoveAll(m => m.Route.Contains(old));
        if (Held == old) LetGo();
        var node = Add(NodeSpecs.All[kind], parent, old.Position);
        node.PreviousPosition = old.PreviousPosition;
        node.Velocity = old.Velocity;
        node.Rotation = old.Rotation;
        node.Power = Math.Min(old.Power, node.Store);
        node.FillLook = old.FillLook;
        node.TiedTo = old.TiedTo;
        return node;
    }

    // Dev aid (--seedling) and screenshots: a seedling grown at once on the deepest part with a free link, a slot added
    // if none is free. Null if no part has room.
    public PlantNode? PlantSeedling()
    {
        var parent = Nodes.Where(n => n.CanBranch && !n.Spec.IsFruit).MaxBy(n => n.Depth);
        if (parent == null || Nodes.Count >= Tuning.MaxNodes) return null;
        if (FreeSeedlingSlots == 0) Capacity = Capacity with { Seedlings = SeedlingsUsed + 1 };
        return AddBranch(parent, NodeKind.Seedling);
    }

    // Dev aid (--seedfill=N): a seedling's power set to N (up to its store): full, it offers its evolutions, or with one
    // chosen it blooms now.
    public void FillSeedling(PlantNode seedling, float power)
    {
        seedling.Power = Math.Min(power, seedling.Store);
        CheckSeedlings();
    }

    // Dev aid (--evolve=NAME): an evolution chosen and bloomed at once, as a seedling's would be (its effect and
    // caps, a slot added if none is free), its flower grown on the deepest part with a free link, if one has. Returns
    // the evolution, or null if it's at its repeat limit.
    public Evolution? EvolveNow(EvolutionKind kind)
    {
        if (Taken(kind) >= Evolutions.All[kind].MaxLevel) return null;
        if (FreeSeedlingSlots == 0) Capacity = Capacity with { Seedlings = SeedlingsUsed + 1 };
        var evolution = new Evolution { Kind = kind, Level = Taken(kind) + 1, Target = NextTarget, ChosenAt = Tracking.Age };
        Evolved.Add(evolution);
        Tracking.Log(JournalKind.Chosen, $"Evolution chosen: {evolution.Name}");
        Apply(evolution);
        Tracking.Log(JournalKind.Bloomed, BloomedText(evolution));
        if (Nodes.Where(n => n.CanBranch && !n.Spec.IsFruit).MaxBy(n => n.Depth) is { } parent && Nodes.Count < Tuning.MaxNodes)
            AddBranch(parent, evolution.Spec.Becomes);
        return evolution;
    }

    // ==== THE HUB ==== \\
    // DESIGN.md, The Hub. Bought in the core track (Capacity.Hubs), it's planted by hand from a part's toolbar, one at a
    // time. It catches gene blobs as the core does, into its own pool (they never reach the gene balance), which turns
    // into hub points: 1 every so many genes, more each time (NextPointCost). Its panel spends them on a level of a
    // kind's path (Hubs.Paths), a kind found in the garden: each level raises the kind's main effect, and the Hub's
    // upkeep. Pruned, its levels go dormant (their effects off, see UpdateBoosts) until a Hub is planted again; they're
    // kept, as are its pool and points.

    // Genes in the pool not yet a point; points to spend; points earned and genes caught over the garden's life.
    public double HubPool;
    public int HubPoints, HubPointsEarned;
    public double HubGenes;
    // The levels bought on each kind's path.
    public readonly Dictionary<NodeKind, int> HubLevels = new();

    public PlantNode? Hub => Nodes.FirstOrDefault(n => n.IsHub);
    // Whether the levels work now: a Hub is planted (a bud forming doesn't count).
    public bool HubPlanted => Nodes.Any(n => n.IsHub);
    public int HubLevelsBought => HubLevels.Values.Sum();
    public int HubLevel(NodeKind kind) => HubLevels.GetValueOrDefault(kind);
    // Whether a Hub can be planted now: bought, and none planted or forming.
    public bool CanPlantHub => CountOf(NodeKind.Hub) < Capacity.Hubs;
    // Every path at its top level: gene blobs pass through the Hub, as through any part (worked out in UpdateBoosts).
    public bool HubComplete { get; private set; }
    // A Hub planted as the boosts were last worked out, and its levels' share of the Amplifier's chance.
    bool hubAwake;
    float amplifyBonus;
    // The Buffer's and the Moonflower's paths' shares, a level each (Nov 3): every buff's bonus, and the Moonflower's.
    float hubBuffStrength, hubMoonlit;

    // Whether a path's level (a milestone) works now: bought, and a Hub planted.
    public bool HubAt(NodeKind kind, int level) => hubAwake && HubLevel(kind) >= level;

    // The stem cap, with the stems' level 5 milestone (HubStemCap more while it works).
    public int StemCap => HubAt(NodeKind.Stem, 5) ? Capacity.Add(Capacity.Stems, Tuning.HubStemCap) : Capacity.Stems;

    // The caps as they work now (the stem cap with its Hub milestone), for the garden card.
    public Capacity Caps => Capacity with { Stems = StemCap };

    // The genes the next point takes: HubFirstPoint, then HubPointStep more for each point earned before.
    public double NextPointCost => Tuning.HubFirstPoint + Tuning.HubPointStep * HubPointsEarned;

    // Gene blobs the Hub caught: into its pool, and a point for each NextPointCost the pool holds.
    internal void CatchAtHub(double genes)
    {
        HubGenes += genes;
        HubPool += genes;
        while (HubPool >= NextPointCost)
        {
            HubPool -= NextPointCost;
            HubPoints++;
            HubPointsEarned++;
        }
    }

    // Whether a kind's next level can be bought now: a Hub planted, the kind found in the garden, below the top level,
    // and the points to cover it.
    public bool CanBuyHubLevel(NodeKind kind) =>
        HubPlanted && Hubs.HasPath(kind) && Tracking.Discovered.ContainsKey(kind) && HubLevel(kind) < Hubs.Path(kind).MaxLevel
        && HubPoints >= Hubs.Path(kind).Cost(HubLevel(kind) + 1);

    // Whether the Hub's points cover any kind's next level.
    public bool HubLevelAffordable => Hubs.Paths.Any(p => CanBuyHubLevel(p.Kind));

    // Buys a kind's next level with the Hub's points: its effect, and the Hub's upkeep, rise at once. Returns whether it
    // bought.
    public bool BuyHubLevel(NodeKind kind)
    {
        if (!CanBuyHubLevel(kind)) return false;
        int level = HubLevel(kind) + 1;
        HubPoints -= Hubs.Path(kind).Cost(level);
        HubLevels[kind] = level;
        UpdateBoosts();
        return true;
    }

    // Respec (Jeremy, Nov 3): every path back to level 0, its points refunded but HubRespecCost (a level's worth: the
    // cheapest level costs 1). Returns whether anything was bought to take back.
    public bool RespecHub()
    {
        int spent = Hubs.Paths.Sum(p => Enumerable.Range(1, HubLevel(p.Kind)).Sum(p.Cost));
        if (spent == 0) return false;
        HubLevels.Clear();
        HubPoints += Math.Max(0, spent - Tuning.HubRespecCost);
        Tracking.Log(JournalKind.Respec, "The Hub's levels were reset");
        UpdateBoosts();
        return true;
    }

    // Dev aid (--hub) and screenshots: a Hub grown at once on the deepest part with a free link, a Hub slot added if
    // none is free (as if bought). Null if one is planted already or no part has room.
    public PlantNode? PlantHub()
    {
        if (HubPlanted || Nodes.Count >= Tuning.MaxNodes) return null;
        var parent = Nodes.Where(n => n.CanBranch && !n.Spec.IsFruit && n.Spec.MaxLinks > 1).MaxBy(n => n.Depth);
        if (parent == null) return null;
        if (CountOf(NodeKind.Hub) >= Capacity.Hubs) Capacity = Capacity with { Hubs = CountOf(NodeKind.Hub) + 1 };
        return AddBranch(parent, NodeKind.Hub);
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

    // Whether the balance covers the cheapest next upgrade: one waits in Research.
    public bool UpgradeAffordable => Cheapest is { } next && GeneBalance >= next.Cost;

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
        int sways = Tuning.SwaysAtSize(size);
        var gained = new List<ShapingKind>();
        if (sways > Capacity.Sways) gained.Add(ShapingKind.Sway);
        if (trellises > Capacity.Trellises) gained.Add(ShapingKind.Trellis);
        if (wells > Capacity.Wells) gained.Add(ShapingKind.Well);
        if (pots > Capacity.Pots) gained.Add(ShapingKind.Pot);
        if (gained.Count == 0) return;
        Capacity = Capacity with
        {
            Pots = Math.Max(Capacity.Pots, pots),
            Trellises = Math.Max(Capacity.Trellises, trellises),
            Wells = Math.Max(Capacity.Wells, wells),
            Sways = Math.Max(Capacity.Sways, sways),
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
        ShapingKind.Sway => "Sway Controller",
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
        // Sway controllers scale it (see SwayScale).
        float gust = (0.5f * MathF.Sin(t * 0.11f) + 0.7f * MathF.Sin(t * 0.37f + p.Y * 0.004f)
            + 0.3f * MathF.Sin(t * 0.93f + node.Id * 0.7f)) * swayScale;

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
            mote.T += dt * mote.Speed / Tuning.MoteLinkSeconds;
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
            n.Power, n.Blob, n.Forming, n.Growth, n.Life,
            n.Evolution is { } e ? Evolved.IndexOf(e) : -1, n.Offers?.ToList())).ToList(),
        Genes,
        Shaping.Select(t => new ShapingSave(t.Kind, t.Position.X, t.Position.Y, t.Angle, t.Length,
            Nodes.Where(n => n.TiedTo == t).Select(n => n.Id).ToList())).ToList(),
        Tracking.ToSave(), GeneBalance, new ResearchSave(StemsBought, GeneratorsBought, CoreBought), WildUnlocked.ToList(), new(WildCaps),
        Evolved.Select(e => new EvolutionSave(e.Kind, e.Level, e.Target, e.Bloomed, e.BloomedAt, e.ChosenAt)).ToList(),
        new HubSave(HubPool, HubPoints, HubPointsEarned, HubGenes, new(HubLevels)), Tutorial);

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
            Tutorial = save.Tutorial,
            loading = true,
        };
        if (save.Research is { } research)
            (garden.StemsBought, garden.GeneratorsBought, garden.CoreBought) = (research.Stems, research.Generators, research.Core);
        else garden.ReadOldTrack();
        garden.WildUnlocked.UnionWith(save.WildUnlocked ?? []);
        foreach (var (kind, cap) in save.WildCaps ?? []) garden.WildCaps[kind] = cap;
        // (Their caps are in the saved Capacity already: only the boosts are worked out again.)
        foreach (var e in save.Evolutions ?? [])
            garden.Evolved.Add(new Evolution
            {
                Kind = e.Kind, Level = e.Level, Target = e.Target, Bloomed = e.Bloomed, BloomedAt = e.BloomedAt,
                ChosenAt = e.ChosenAt,
            });
        if (save.Hub is { } hub)
        {
            (garden.HubPool, garden.HubPoints, garden.HubPointsEarned, garden.HubGenes) = (hub.Pool, hub.Points, hub.Earned, hub.Genes);
            foreach (var (kind, level) in hub.Levels) garden.HubLevels[kind] = level;
        }
        garden.UpdateBoosts();
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
            node.Offers = n.Offers?.ToList();
            if (n.Evolution >= 0 && n.Evolution < garden.Evolved.Count) node.Evolution = garden.Evolved[n.Evolution];
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
        if (garden.HubPlanted) garden.UpdateBoosts(); // its levels work: they needed the Hub in the garden
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
// limit them. The wild flower limit (5d), seedling slots (5c) and Hubs (one planted at a time once bought) have their
// mechanics. Upgrades bought and evolutions bloomed raise these, and the garden's size the shaping tools.
public sealed record Capacity(int Stems, int Generators, int CoreLinks, int Pots = Tuning.StartPotCap, int Trellises = 0,
    int Wells = 0, int WildFlowers = Tuning.StartWildFlowers, int Seedlings = Tuning.StartSeedlings, int Hubs = 0,
    int Sways = 0)
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
        ShapingKind.Sway => Sways,
        _ => Wells,
    };

    internal static int Add(int a, int b) => (int)Math.Min(int.MaxValue, (long)a + b);
    // Takes b off (an unlimited cap stays so), never below 0.
    internal static int Less(int a, int b) => a == int.MaxValue ? a : Math.Max(0, a - b);
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
        UpgradeKind.WildFlower => "+1 wildflower slot",
        UpgradeKind.CoreLink => "+1 core link",
        UpgradeKind.Seedling => "+1 seedling slot",
        _ => "Hub",
    };

    // What it gives, the hint behind the Research panel's (i) (Jeremy's wording for the first three, Oct 30).
    public string Does => Kind switch
    {
        UpgradeKind.Stem => "Increases the total number of stems.",
        UpgradeKind.Generator => "Increases the total number of generators.",
        UpgradeKind.WildFlower => "Wildflowers are rare, stronger kinds of flower. With a slot free, any new bud may grow into one instead.",
        UpgradeKind.CoreLink => "Lets one more branch grow straight from the core.",
        UpgradeKind.Seedling => "Each seedling unlocks a single powerful evolution.",
        _ => "The Hub takes in genes for a different unlock path.",
    };
}

// How many of each track's upgrades were bought.
public sealed record ResearchSave(int Stems, int Generators, int Core);

// (Saves from before step 3d also carry Pinned, from when parts could be pinned; it's ignored.)
// (Life since step 5d: a wild flower's life cycle; saves from 5d to Nov 1 also carry a Pulsar's Charge, ignored.
// Evolution and Offers since 5c: a seedling's evolution (an index into GardenSave.Evolutions, or -1), which sets its
// store, and what it offers.)
public sealed record NodeSave(int Id, NodeKind Kind, int ParentId, float X, float Y, float Power,
    float Blob = 0, NodeKind? Forming = null, float Growth = 0, double Life = 0,
    int Evolution = -1, List<EvolutionKind>? Offers = null);

// (WildUnlocked and WildCaps since step 5d: the Evolution kinds unlocked, and any per-kind caps; see Garden.WildUnlocked.
// Evolutions since 5c: those chosen, in order; see Garden.Evolved. Hub since the Hub's step: its pool, points and
// levels. Tutorial since Nov 3: the opening tutorial's step, null when there's none.)
public sealed record GardenSave(Layout Layout, double EconomyClock, Capacity Capacity, List<NodeSave> Nodes,
    double Genes = 0, List<ShapingSave>? Shaping = null, TrackingSave? Tracking = null, double GeneBalance = 0,
    ResearchSave? Research = null, List<NodeKind>? WildUnlocked = null, Dictionary<NodeKind, int>? WildCaps = null,
    List<EvolutionSave>? Evolutions = null, HubSave? Hub = null, TutorialStep? Tutorial = null);
