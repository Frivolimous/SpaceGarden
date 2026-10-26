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
// economy (power, upkeep, wilting) and growth on them. Speeding growth up (or catching up time away) never changes
// how the plant moves.
public sealed class Garden
{
    public readonly List<PlantNode> Nodes = new();
    public readonly List<Link> Links = new();
    public readonly List<Mote> Motes = new();
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

    // Genes that have reached the core over the garden's life, and how many steps of the unlock track they've passed
    // (each one raised Capacity once). The view compares Unlocks with what it has shown, to say what's new.
    public double Genes;
    public int Unlocks;
    public UnlockStep? NextUnlock => Unlocks < Tuning.UnlockTrack.Length ? Tuning.UnlockTrack[Unlocks] : null;

    // Off, blobs still do their work but aren't shown travelling (a catch-up, which draws nothing).
    public bool ShowBlobs = true;

    // Off, growth blobs still grow fruit but start no wild buds (tests, and later a "hold growth" choice).
    public bool WildGrowth = true;

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
    long ticks;
    int nextId;

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
    // branch from the core while it has a free link, else fruit FruitChance of the time, or a wild branch.
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

    // A bud the player asked for (DESIGN.md: "Manual buds cost nothing"): a branch of the chosen kind, grown at once,
    // if the part (or the part a fruit hangs from) has a free link and the caps have room. Never fruit: only growth
    // blobs grow it. Groves grow only this way.
    public PlantNode? SproutFrom(PlantNode node, NodeKind kind)
    {
        if (node.Spec.IsFruit && node.Parent != null) node = node.Parent;
        return CanSproutFrom(node, kind) ? AddBranch(node, kind) : null;
    }

    public bool CanSproutFrom(PlantNode node, NodeKind kind)
    {
        if (node.Spec.IsFruit && node.Parent != null) node = node.Parent;
        var spec = NodeSpecs.All[kind];
        return !spec.IsFruit && kind != NodeKind.Core && node.CanBranch && Nodes.Count < Tuning.MaxNodes
            && HasRoomFor(kind, CountKinds());
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

    // Cuts a part off with everything that grew from it; returns what fell. The core can't be pruned.
    public List<PlantNode> Prune(PlantNode node)
    {
        if (node.Parent == null) return new();
        var removed = new List<PlantNode>();
        void Collect(PlantNode n)
        {
            removed.Add(n);
            foreach (var child in n.Neighbours().Where(c => c.Parent == n)) Collect(child);
        }
        Collect(node);
        var gone = removed.ToHashSet();

        (node.Spec.IsFruit ? node.Parent.Fruits : node.Parent.Branches).Remove(node);
        Nodes.RemoveAll(gone.Contains);
        Links.RemoveAll(l => gone.Contains(l.Target));
        Motes.RemoveAll(m => m.Route.Any(gone.Contains));
        foreach (var n in removed) linkToParent.Remove(n);
        if (Held != null && gone.Contains(Held)) LetGo();
        lastPrune = (removed, node.Parent.Position);
        UndoSecondsLeft = Tuning.UndoPruneSeconds;
        return removed;
    }

    // The last prune, while it can still be undone (see Tuning.UndoPruneSeconds): what fell, and where its parent
    // was then. Only the last one: a new prune replaces it.
    (List<PlantNode> Nodes, Vector2 ParentAt)? lastPrune;
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
            var (stems, generators) = (counts.Stems, counts.Generators);
            stems +=nodes.Count(n => n.Spec.Kind == NodeKind.Stem);
            generators += nodes.Count(n => n.Spec.Kind == NodeKind.Generator);
            return stems <= Capacity.Stems && generators <= Capacity.Generators;
        }
    }

    // Puts the last prune back as it was (power, pins, fruit and all), moved along with its parent if the plant has
    // drifted since, and growing in again. Returns what came back, or null if it can't (see CanUndoPrune).
    public List<PlantNode>? UndoPrune()
    {
        if (!CanUndoPrune) return null;
        var (nodes, parentAt) = lastPrune!.Value;
        ForgetPrune();
        var root = nodes[0];
        var shift = root.Parent!.Position - parentAt;
        (root.Spec.IsFruit ? root.Parent.Fruits : root.Parent.Branches).Add(root);
        // Cut in growth order (a part before what grew from it), so they go back in that order.
        foreach (var node in nodes)
        {
            node.Position += shift;
            node.PreviousPosition = node.Position;
            node.Velocity = Vector2.Zero;
            node.Age = 0;
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

    // A pinned part holds still where it is (the core always is).
    public void TogglePin(PlantNode node)
    {
        if (node.Parent == null) return;
        node.Fixed = !node.Fixed;
        node.Velocity = Vector2.Zero;
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
        foreach (var node in Nodes)
        {
            node.Position += offset;
            node.PreviousPosition += offset;
        }
        HoldTarget += offset;
    }

    // How many stems and generators there are, counting the buds forming (they hold their slot), for the caps.
    readonly record struct Counts(int Stems, int Generators)
    {
        // With one more of this kind (a bud starting).
        public Counts With(NodeKind kind) => kind switch
        {
            NodeKind.Stem => this with { Stems = Stems + 1 },
            NodeKind.Generator => this with { Generators = Generators + 1 },
            _ => this,
        };
    }

    Counts CountKinds()
    {
        int stems = 0, generators = 0;
        void Count(NodeKind kind)
        {
            if (kind == NodeKind.Stem) stems++;
            else if (kind == NodeKind.Generator) generators++;
        }
        foreach (var n in Nodes)
        {
            Count(n.Spec.Kind);
            if (n.FormingBud) Count(n.Forming!.Value);
        }
        return new(stems, generators);
    }

    // Stems and generators are capped; labs and groves only by free links.
    bool HasRoomFor(NodeKind kind, Counts counts) => kind switch
    {
        NodeKind.Stem => counts.Stems < Capacity.Stems,
        NodeKind.Generator => counts.Generators < Capacity.Generators,
        _ => true,
    };

    // The kinds of wild branch this part could bud now, with their odds: near the core only stems, further out also
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

    PlantNode AddBranch(PlantNode parent, NodeKind kind, bool emerge = false)
    {
        var node = AddNear(NodeSpecs.All[kind], parent, emerge);
        node.Power = Tuning.SproutPower;
        return node;
    }

    PlantNode AddFruit(PlantNode parent) => AddNear(NodeSpecs.All[parent.Spec.Fruit!.Value], parent);

    // Places a new part just off its parent, heading away from the grandparent (and upward when upright). A bud that
    // formed inside its parent (emerge) starts deep inside it and pops out, its link extending.
    PlantNode AddNear(NodeSpec spec, PlantNode parent, bool emerge = false)
    {
        Vector2 away = parent.Parent == null
            ? (Layout == Layout.Upright ? -Vector2.UnitY : RandomDirection())
            : Vector2.Normalize(parent.Position - parent.Parent.Position + new Vector2(0.001f, 0));
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

    // One fixed step of the economy (DESIGN.md, Economy):
    //   - the core and generators make power into their own store (anything past full is lost);
    //   - every other part pays its upkeep from its own store. One that can't wilts and stops draining, and recovers
    //     once the power flowing in has filled it back to RecoverFill. Wilting never removes anything;
    //   - flowers make power or blobs in proportion to their fill (none while wilting): labs gene blobs, groves
    //     growth blobs, each sent off on its walk as it's made (see SendGenes, SendGrowth);
    //   - power flows along each link downhill, from the fuller part to the emptier, so it spreads out from the
    //     sources and a branch far from them runs low first (there is no global pool).
    // Nothing checks whether the garden can afford what grows: that's the player's to judge (prune what drains it).
    // Returns how many parts grew.
    int EconomyStep(double seconds)
    {
        float hours = (float)(seconds / 3600);
        foreach (var node in Nodes)
        {
            if (node.Spec.IsFruit) continue;
            node.Power = Math.Min(node.Store, node.Power + ProductionPerHour(node) * hours);
            if (node.Wilting && node.Fill >= Tuning.RecoverFill) node.Wilting = false;
            if (node.Wilting) continue;
            float upkeep = UpkeepPerHour(node) * hours;
            if (node.Power >= upkeep) node.Power -= upkeep;
            else
            {
                node.Power = 0;
                node.Wilting = true;
                continue;
            }
            node.Blob += BlobsPerHour(node) * hours;
        }

        // Blobs leave after the loop over the parts, as what they grow joins them. A blob leaves even when nothing
        // will absorb it: it just wanders and fades (pretty to watch).
        int grew = 0;
        sending.Clear();
        foreach (var node in Nodes)
            if (node.Blob >= 1 - 1e-4f) sending.Add(node);
        if (sending.Count > 0)
        {
            var counts = CountKinds();
            foreach (var flower in sending)
            {
                while (flower.Blob >= 1 - 1e-4f) // (a whole blob, give or take rounding)
                {
                    flower.Blob = Math.Max(0, flower.Blob - 1);
                    if (flower.Spec.Kind == NodeKind.Lab) SendGenes(flower, ref counts);
                    else if (SendGrowth(flower, ref counts) != null) grew++;
                }
            }
        }

        float flow = Tuning.PowerFlowPerHour * hours;
        foreach (var link in Links)
        {
            link.FlowPerHour = 0;
            if (link.Target.Spec.IsFruit) continue;
            var (a, b) = link.Origin.Fill >= link.Target.Fill ? (link.Origin, link.Target) : (link.Target, link.Origin);
            float diff = a.Fill - b.Fill;
            if (diff <= 0) continue;
            // Never more than evens out their fill, so a long step can't overshoot.
            float amount = Math.Min(flow * diff, diff / (1 / a.Store + 1 / b.Store));
            a.Power -= amount;
            b.Power += amount;
            link.FlowPerHour = amount / hours;
            link.FlowsToTarget = b == link.Target;
        }
        return grew;
    }

    // How hard a flower works (DESIGN.md: "Flower output follows power"): from OutputFloor when empty up to full
    // when full, and nothing while wilting. (With no floor a generator among drained parts dies away: power flows
    // out of it faster than its own falling output can make it.)
    public static float Output(PlantNode node) =>
        node.Wilting ? 0 : Tuning.OutputFloor + (1 - Tuning.OutputFloor) * Math.Clamp(node.Fill, 0, 1);

    // Power a part makes per real hour now: the core always its full rate, a generator in proportion to its fill
    // (its fruit adding a little, up to FruitBonusMax).
    public static float ProductionPerHour(PlantNode node) => node.Spec.Kind switch
    {
        NodeKind.Core => Tuning.CorePowerPerHour,
        NodeKind.Generator => Tuning.GeneratorPowerPerHour * FruitBonus(node) * Output(node),
        _ => 0,
    };

    // A flower's own fruit raise its output a little each, up to FruitBonusMax.
    static float FruitBonus(PlantNode node) =>
        1 + Math.Min(Tuning.FruitBonusMax, node.Fruits.Count * Tuning.FruitBonusEach);

    // Power a part costs per real hour while it isn't wilting. Each leaf halves a stem's.
    public static float UpkeepPerHour(PlantNode node) => node.Spec.Kind == NodeKind.Stem
        ? Tuning.StemUpkeepPerHour * MathF.Pow(Tuning.LeafUpkeepFactor, node.Fruits.Count(f => f.Spec.Kind == NodeKind.Leaf))
        : KindUpkeepPerHour(node.Spec.Kind);

    // What a new part of this kind costs per hour (no fruit yet).
    public static float KindUpkeepPerHour(NodeKind kind) => kind switch
    {
        NodeKind.Stem => Tuning.StemUpkeepPerHour,
        NodeKind.Lab => Tuning.LabUpkeepPerHour,
        NodeKind.Grove => Tuning.GroveUpkeepPerHour,
        _ => 0,
    };

    // Blobs a flower makes per real hour now: gene blobs from a lab, growth blobs from a grove, in proportion to its
    // fill (their fruit adding a little).
    public static float BlobsPerHour(PlantNode node) => node.Spec.Kind switch
    {
        NodeKind.Lab => Tuning.LabBlobsPerHour * FruitBonus(node) * Output(node),
        NodeKind.Grove => Tuning.GroveBlobsPerHour * FruitBonus(node) * Output(node),
        _ => 0,
    };

    // ==== BLOBS ==== \\

    // A blob's walk from its flower (DESIGN.md, Economy, Blobs): link by link at random, never straight back unless
    // at a dead end, until a part absorbs it (a gene blob: only the core) or BlobMaxHops links have passed. Returns
    // the part that absorbed it, if one did, and adds the parts it passed to route (from the flower), if given: only
    // a blob that will be shown needs one. Many thousands of blobs leave an hour, so it allocates nothing else.
    PlantNode? Walk(PlantNode from, bool genes, ref Counts counts, List<PlantNode>? route)
    {
        route?.Add(from);
        PlantNode? cameFrom = null;
        var at = from;
        for (int hop = 0; hop < Tuning.BlobMaxHops; hop++)
        {
            int options = at.Branches.Count + (at.Parent != null ? 1 : 0);
            if (options == 0) return null; // a lone core
            var skip = options > 1 ? cameFrom : null;
            var next = Option(at, rng.Next(skip != null ? options - 1 : options), skip);
            cameFrom = at;
            at = next;
            route?.Add(at);
            if (genes ? at == Core : Absorbs(at, counts)) return at;
        }
        return null;
    }

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

    // A blob is shown following its route, unless in a catch-up or with too many already in flight.
    List<PlantNode>? RouteIfShown() => ShowBlobs && Motes.Count < Tuning.MaxMotes ? new List<PlantNode>() : null;

    Mote? Show(List<PlantNode>? route, PlantNode flower)
    {
        if (route == null || route.Count < 2) return null;
        var mote = new Mote(route, flower.Spec.Rgb);
        Motes.Add(mote);
        return mote;
    }

    // A lab's gene blob leaves. Only the core absorbs it; its genes count at the core in this same step, so a
    // catch-up (which runs no physics) and a live session agree, and the blob shown is their journey made visible.
    // A blob that never finds the core fades, its genes lost: labs near the core waste fewer.
    void SendGenes(PlantNode lab, ref Counts counts)
    {
        var route = RouteIfShown();
        if (Walk(lab, genes: true, ref counts, route) != null) Deliver(Tuning.BlobGenes);
        Show(route, lab);
    }

    // A grove's growth blob leaves. Where it goes and what it does are settled now, from the garden's own dice, so a
    // catch-up and a live session agree: the part that absorbs it forms more of its fruit or bud, and a part that
    // completes is added now, hidden (its Age below zero) until the blob, shown following the same walk, arrives.
    // Returns the part that grew, if one did. Counts are the step's, kept up to date as buds start.
    PlantNode? SendGrowth(PlantNode grove, ref Counts counts)
    {
        var route = RouteIfShown();
        var absorber = Walk(grove, genes: false, ref counts, route);
        if (absorber == null)
        {
            Show(route, grove);
            return null;
        }
        var grown = Feed(absorber, ref counts);
        if (Show(route, grove) is { } mote)
        {
            mote.Growth = Tuning.BlobGrowth;
            mote.Completes = grown != null;
            if (grown != null) grown.Age = -(route!.Count - 1) * Tuning.MoteLinkSeconds;
        }
        else absorber.FormingLook = absorber.Growth;
        return grown;
    }

    // A part with something forming always absorbs a growth blob; one with room to start a fruit or a bud does
    // AbsorbChance of the time; a full one never does.
    bool Absorbs(PlantNode node, Counts counts)
    {
        if (node.Forming != null) return true;
        return (CanStartFruit(node) || CanBud(node, counts)) && rng.NextDouble() < Tuning.AbsorbChance;
    }

    // While a prune can be undone, nothing new starts on the pruned branch's parent, and no bud anywhere (it would
    // take a cap slot the undo needs). Wild buds wait for WildGrowth too.
    bool CanStartFruit(PlantNode node) =>
        node.CanFruit && Nodes.Count < Tuning.MaxNodes && lastPrune?.Nodes[0].Parent != node;

    // Whether BranchKinds has anything for this part, without listing them: the core grows only stems, any other part
    // a lab at least (labs are uncapped).
    bool CanBud(PlantNode node, Counts counts) =>
        WildGrowth && lastPrune == null && Nodes.Count < Tuning.MaxNodes && node.CanBranch
        && (node.Depth >= 1 || HasRoomFor(NodeKind.Stem, counts));

    // A growth blob absorbed: a part with nothing forming starts something (a bud BudChance of the time where a
    // branch could grow, its kind rolled now; else a fruit), then the blob's growth is added, and what's forming
    // completes once it has grown enough. Returns the part that grew, if one did.
    PlantNode? Feed(PlantNode node, ref Counts counts)
    {
        if (node.Forming == null)
        {
            bool bud = CanBud(node, counts) && (!CanStartFruit(node) || rng.NextDouble() < Tuning.BudChance);
            node.Forming = bud ? Roll(BranchKinds(node, counts)) : node.Spec.Fruit;
            if (bud) counts = counts.With(node.Forming!.Value);
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

    // ==== GENES AND UNLOCKS ==== \\

    // Genes reach the core: each threshold of the unlock track they pass raises the caps, in order.
    public void Deliver(double genes)
    {
        Genes += genes;
        while (NextUnlock is { } next && Genes >= next.Genes)
        {
            Capacity = Capacity.Raise(next);
            Unlocks++;
        }
    }

    // The whole garden's power per hour: what it makes, and what its parts that aren't wilting cost.
    public (float Production, float Upkeep) PowerBalance()
    {
        float production = 0, upkeep = 0;
        foreach (var node in Nodes)
        {
            production += ProductionPerHour(node);
            if (!node.Wilting) upkeep += UpkeepPerHour(node);
        }
        return (production, upkeep);
    }

    public float TotalPower() => Nodes.Sum(n => n.Power);

    // Fruit looks as its parent does.
    static float WiltTarget(PlantNode node) => (node.Spec.IsFruit ? node.Parent!.Wilting : node.Wilting) ? 1 : 0;

    // Shows every part as it is now, with no easing (after a load or a catch-up, or a dev change to the power).
    public void SettleLooks()
    {
        foreach (var node in Nodes)
        {
            node.WiltLook = WiltTarget(node);
            node.FillLook = Math.Min(1, node.Spec.IsFruit ? node.Parent!.Fill : node.Fill);
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
        foreach (var link in Links)
        {
            var d = link.Origin.Position - link.Target.Position;
            float dist = d.Length();
            if (dist < link.Length) continue;
            float mult = Tuning.Elasticity * (link.Length - dist) / dist;
            if (!link.Origin.Fixed) link.Origin.Velocity += mult * d * link.Origin.Spec.InverseMass;
            if (!link.Target.Fixed) link.Target.Velocity -= mult * d * link.Target.Spec.InverseMass;
        }

        // A held part is pulled to the pointer by its weight, as in the TS game: light parts follow briskly, heavy
        // ones like the core lumber after it (a pinned one is carried, moving its pin). Everything else follows only
        // through the links.
        if (Held is { } held)
        {
            if (held.Fixed)
                held.Position = Vector2.Lerp(held.Position, HoldTarget, Tuning.PinnedFollow);
            else
            {
                held.Velocity *= Tuning.PullFriction;
                var d = held.Position - HoldTarget;
                if (d.LengthSquared() > Tuning.PullMinDistanceSquared) held.Velocity -= Tuning.PullRatio * d * held.Spec.InverseMass;
            }
        }

        Repel();

        foreach (var node in Nodes)
        {
            node.Rotation += node.Spin;
            if (node.Rotation > MathF.PI) node.Rotation -= MathF.Tau;
            else if (node.Rotation < -MathF.PI) node.Rotation += MathF.Tau;
            if (node.Fixed)
            {
                node.Velocity = Vector2.Zero;
                continue;
            }

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

        Speed = 0;
        foreach (var node in Nodes) Speed = Math.Max(Speed, Vector2.Distance(node.Position, node.PreviousPosition));

        UpdateMotes(dt);
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

    // Power shows as flashes, not motes: as in the original game, where each power transfer lit its link at once
    // and the light faded over about 0.3 s. Here power flows smoothly, so a link flashes as often as power runs
    // along it (FlashesPerPowerHour per unit per hour, at most MaxFlashesPerSecond): a busy trunk pulses, a quiet
    // twig now and then, a full branch not at all. The rate is per real second whatever the growth speed, so the
    // plant looks the same at 1x and 60x. Motes are the blobs that do travel: genes from the labs (see SendGenes)
    // and growth from the groves (see SendGrowth).
    void UpdateMotes(float dt)
    {
        foreach (var node in Nodes)
        {
            node.Age += dt;
            node.Glow = Math.Max(0, node.Glow - Tuning.GlowFadePerSecond * dt);
            float ease = Tuning.WiltLookPerSecond * dt;
            node.WiltLook = Math.Clamp(WiltTarget(node), node.WiltLook - ease, node.WiltLook + ease);

            // As the original's PlantNodeView.adjustIntensity: a share of the way to the fill each tick, quicker
            // filling than emptying. Fruit shows its parent's.
            float fill = Math.Min(1, node.Spec.IsFruit ? node.Parent!.Fill : node.Fill);
            node.FillLook += (fill - node.FillLook) * (fill > node.FillLook ? Tuning.FillLookRise : Tuning.FillLookFall);
            node.BounceSpeed -= (Tuning.BounceStiffness * node.Bounce + Tuning.BounceDamping * node.BounceSpeed) * dt;
            node.Bounce = Math.Clamp(node.Bounce + node.BounceSpeed * dt, -Tuning.BounceMax, Tuning.BounceMax);
        }
        foreach (var link in Links)
        {
            link.Flash = Math.Max(0, link.Flash - Tuning.LinkFlashFadePerSecond * dt);
            link.FlashPhase += dt * Math.Min(Tuning.MaxFlashesPerSecond, link.FlowPerHour * Tuning.FlashesPerPowerHour);
            if (link.FlashPhase < 1) continue;
            // The flash is as bright as the part the power reached is full, so power trickling into a low branch
            // glimmers and only a full one flashes brightly. That part bounces (with its fruit, which share its look).
            var reached = link.FlowsToTarget ? link.Target : link.Origin;
            link.Flash = Tuning.FlashPeak * Math.Clamp(reached.Fill, 0, 1);
            foreach (var part in reached.Fruits.Prepend(reached)) part.BounceSpeed += Tuning.BounceKick;
            // A little jitter (the original's ±10%), so links carrying the same flow drift out of step.
            link.FlashPhase = (float)(rng.NextDouble() - 0.5) * 0.2f;
        }

        for (int i = Motes.Count - 1; i >= 0; i--)
        {
            var mote = Motes[i];
            mote.T += dt / Tuning.MoteLinkSeconds;
            if (mote.T < 1) continue;

            var arrived = mote.To;
            arrived.Glow = 1;
            if (LinkBetween(mote.From, arrived) is { } link) link.Flash = 1;
            var route = mote.Route;
            var next = ++mote.Leg + 1 < route.Count ? route[mote.Leg + 1] : null;
            if (next == null)
            {
                // A growth blob's work shows as it arrives: what's forming grows, or the part it completed appears
                // (see SendGrowth) and the silhouette goes.
                if (mote.Completes) arrived.FormingLook = 0;
                else if (mote.Growth > 0) arrived.FormingLook = Math.Min(arrived.Growth, arrived.FormingLook + mote.Growth);
                Motes.RemoveAt(i);
                continue;
            }
            mote.From = arrived;
            mote.To = next;
            mote.T -= 1;
        }
    }

    Link? LinkBetween(PlantNode a, PlantNode b) =>
        b.Parent == a ? linkToParent[b] : a.Parent == b ? linkToParent[a] : null;

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
            n.Parent != null && n.Fixed, n.Power, n.Wilting, n.Blob, n.Forming, n.Growth)).ToList(),
        Genes, Unlocks);

    // Parts are saved in the order they grew, so a parent always comes before its children.
    public static Garden FromSave(GardenSave save, int? seed = null)
    {
        var garden = new Garden(save.Layout, seed)
        {
            EconomyClock = save.EconomyClock,
            Capacity = save.Capacity,
            Genes = save.Genes,
            Unlocks = save.Unlocks,
        };
        var byId = new Dictionary<int, PlantNode>();
        foreach (var n in save.Nodes)
        {
            var parent = n.ParentId < 0 ? null : byId[n.ParentId];
            var node = garden.Add(NodeSpecs.All[n.Kind], parent, new Vector2(n.X, n.Y));
            node.Age = 10;
            node.Fixed = (parent == null && garden.Planted) || n.Pinned;
            node.Power = n.Power;
            node.Wilting = n.Wilting;
            node.Blob = n.Blob;
            node.Forming = n.Forming;
            node.Growth = n.Growth;
            byId[n.Id] = node;
        }
        garden.SettleLooks();
        return garden;
    }
}

// The limits on growth (DESIGN.md, Capacity): how many stems and generators the garden may have, and how many links
// the core has. Labs and other flowers are uncapped; free links and power limit them. The unlock track raises these.
public sealed record Capacity(int Stems, int Generators, int CoreLinks)
{
    public static readonly Capacity Start = new(Tuning.StartStemCap, Tuning.StartGeneratorCap, Tuning.StartCoreLinks);
    public static readonly Capacity Unlimited = new(int.MaxValue, int.MaxValue, 2); // stress tests (--parts)

    // These limits with an unlock's raises added (never past int.MaxValue, so an unlimited garden stays so).
    public Capacity Raise(UnlockStep step) => this with
    {
        Stems = Add(Stems, step.Stems),
        Generators = Add(Generators, step.Generators),
        CoreLinks = Add(CoreLinks, step.CoreLinks),
    };

    static int Add(int a, int b) => (int)Math.Min(int.MaxValue, (long)a + b);
}

// One step of the linear unlock track: once Genes have reached the core over the garden's life, the caps rise by
// these. Only stems, generators and core links unlock (DESIGN.md: groves and labs are limited by slots and power).
public sealed record UnlockStep(double Genes, int Stems = 0, int Generators = 0, int CoreLinks = 0);

public sealed record NodeSave(int Id, NodeKind Kind, int ParentId, float X, float Y, bool Pinned, float Power, bool Wilting,
    float Blob = 0, NodeKind? Forming = null, float Growth = 0);

public sealed record GardenSave(Layout Layout, double EconomyClock, Capacity Capacity, List<NodeSave> Nodes,
    double Genes = 0, int Unlocks = 0);
