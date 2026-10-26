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

    // Seconds of motion (drives the wind); real seconds of growth not yet spent on a sprout; and real seconds not
    // yet run through the economy (less than one EconomyStepSeconds).
    public double Time;
    public double GrowthClock;
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

    // Off, gene blobs still deliver their genes but aren't shown travelling (a catch-up, which draws nothing).
    public bool ShowBlobs = true;

    // Off, the economy still runs but nothing sprouts by itself (tests, and later a "hold growth" choice).
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

    // Spends real seconds on the economy and growth, in fixed steps; returns how many parts sprouted. However the
    // time is chunked (a frame at a time, or three days at once on return), the same steps run.
    public int Grow(double seconds)
    {
        EconomyClock += seconds;
        int sprouted = 0;
        while (EconomyClock >= Tuning.EconomyStepSeconds)
        {
            EconomyClock -= Tuning.EconomyStepSeconds;
            sprouted += EconomyStep(Tuning.EconomyStepSeconds);
        }
        return sprouted;
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

    // A new part somewhere on the plant, for free (the first stem, stress tests), or null if there's no room.
    public PlantNode? Sprout() => Sprout(wild: false);

    // A sprout the player asked for by clicking a part: a branch if it has room, else a fruit. Manual buds cost no
    // power (DESIGN.md: "Manual buds cost nothing") but keep within capacity.
    public PlantNode? SproutFrom(PlantNode node)
    {
        if (node.Spec.IsFruit && node.Parent != null) node = node.Parent;
        if (Nodes.Count >= Tuning.MaxNodes) return null;
        var counts = CountKinds();
        if (BranchKinds(node, counts).Count > 0) return AddBranch(node, counts);
        if (node.CanFruit) return AddFruit(node);
        return null;
    }

    // A sprout of a chosen kind (a branch, or that part's own fruit), if there's room for it; free, like any bud.
    public PlantNode? SproutFrom(PlantNode node, NodeKind kind)
    {
        if (Nodes.Count >= Tuning.MaxNodes) return null;
        var spec = NodeSpecs.All[kind];
        if (spec.IsFruit) return node.Spec.Fruit == kind && node.CanFruit ? AddFruit(node) : null;
        if (kind == NodeKind.Core || !node.CanBranch || !HasRoomFor(kind, CountKinds())) return null;
        var sprout = AddNear(spec, node);
        sprout.Power = Tuning.SproutCost;
        return sprout;
    }

    public bool CanSproutFrom(PlantNode node)
    {
        if (node.Spec.IsFruit && node.Parent != null) node = node.Parent;
        return Nodes.Count < Tuning.MaxNodes && (node.CanFruit || BranchKinds(node, CountKinds()).Count > 0);
    }

    // Wild growth sprouts only from parts with power to spare, and the parent pays for it; a free sprout can grow
    // anywhere with room.
    PlantNode? Sprout(bool wild)
    {
        if (Nodes.Count >= Tuning.MaxNodes) return null;
        var counts = CountKinds();
        // Wild growth also keeps within what the whole garden can afford (see WildSpare); a free sprout needn't.
        var spare = wild ? WildSpare() : Spare.Unlimited;
        bool HasPower(PlantNode n, float cost) => !wild || (!n.Wilting && n.Fill >= Tuning.GrowFill && n.Power >= cost);
        var branchable = Nodes.Where(n => BranchKinds(n, counts, spare).Count > 0 && HasPower(n, Tuning.SproutCost)).ToList();
        var fruitable = Nodes.Where(n => n.CanFruit && HasPower(n, Tuning.FruitSproutCost)).ToList();
        // A free core link (a new one from an unlock) is filled first, before the other stems take the new stem cap
        // and leave it bare: the unlock's point is a new branch from the core.
        bool fruit = false;
        PlantNode parent = Core;
        if (!branchable.Contains(Core))
        {
            fruit = branchable.Count == 0 || (fruitable.Count > 0 && rng.NextDouble() < Tuning.FruitChance);
            if (fruit && fruitable.Count == 0) return null;
            parent = Pick(fruit ? fruitable : branchable);
        }
        if (wild) parent.Power -= fruit ? Tuning.FruitSproutCost : Tuning.SproutCost;
        return fruit ? AddFruit(parent) : AddBranch(parent, counts, spare);
    }

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
        Motes.RemoveAll(m => gone.Contains(m.From) || gone.Contains(m.To));
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
            var (stems, generators) = CountKinds();
            stems += nodes.Count(n => n.Spec.Kind == NodeKind.Stem);
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

    void ForgetPrune()
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

    // How many stems and generators there are, for the caps.
    (int Stems, int Generators) CountKinds()
    {
        int stems = 0, generators = 0;
        foreach (var n in Nodes)
        {
            if (n.Spec.Kind == NodeKind.Stem) stems++;
            else if (n.Spec.Kind == NodeKind.Generator) generators++;
        }
        return (stems, generators);
    }

    // Stems and generators are capped; labs only by free links and power.
    bool HasRoomFor(NodeKind kind, (int Stems, int Generators) counts) => kind switch
    {
        NodeKind.Stem => counts.Stems < Capacity.Stems,
        NodeKind.Generator => counts.Generators < Capacity.Generators,
        _ => true,
    };

    // The kinds of branch this part could grow now, with their odds: near the core only stems, further out also
    // generators and labs. A stem next to the core takes flowers too once no stem can grow there, so its free link
    // isn't left empty for good when the stem cap is reached. (The core itself only ever grows stems.) Only kinds
    // the garden can afford (see WildSpare) count.
    List<(NodeKind Kind, double Weight)> BranchKinds(PlantNode parent, (int Stems, int Generators) counts,
        Spare? spare = null)
    {
        var kinds = new List<(NodeKind, double)>();
        if (!parent.CanBranch) return kinds;
        void Consider((NodeKind, double)[] odds)
        {
            foreach (var (kind, weight) in odds)
                if (HasRoomFor(kind, counts) && (spare ?? Spare.Unlimited).Affords(kind)) kinds.Add((kind, weight));
        }
        Consider(parent.Depth < 2 ? NearOdds : FarOdds);
        if (kinds.Count == 0 && parent.Depth == 1) Consider(FarOdds);
        return kinds;
    }

    static readonly (NodeKind, double)[] NearOdds = [(NodeKind.Stem, 1)];
    static readonly (NodeKind, double)[] FarOdds = [(NodeKind.Stem, 0.74), (NodeKind.Generator, 0.14), (NodeKind.Lab, 0.12)];

    // How much more upkeep an hour wild growth may add: what the garden makes, less what all its parts cost when
    // none are wilting (wilting ones will want paying again), less a reserve. Labs keep a bigger one (a share of the
    // production), so they never take the power that stems need to reach new room for generators; without it a
    // garden fills with labs and stops growing. Never below zero, so a generator (no upkeep) can always grow.
    public Spare WildSpare()
    {
        float production = 0, upkeep = 0;
        foreach (var node in Nodes)
        {
            production += ProductionPerHour(node);
            upkeep += UpkeepPerHour(node);
        }
        float spare = production - upkeep;
        return new(Math.Max(0, spare - Tuning.WildSpareReserve), Math.Max(0, spare - Tuning.LabSpareShare * production));
    }

    public readonly record struct Spare(float Any, float Lab)
    {
        public static readonly Spare Unlimited = new(float.MaxValue, float.MaxValue);
        public bool Affords(NodeKind kind) => KindUpkeepPerHour(kind) <= (kind == NodeKind.Lab ? Lab : Any);
    }

    PlantNode AddBranch(PlantNode parent, (int Stems, int Generators) counts, Spare? spare = null)
    {
        var kinds = BranchKinds(parent, counts, spare);
        double roll = rng.NextDouble() * kinds.Sum(k => k.Weight);
        var kind = kinds[^1].Kind;
        foreach (var (k, weight) in kinds)
        {
            roll -= weight;
            if (roll >= 0) continue;
            kind = k;
            break;
        }
        var node = AddNear(NodeSpecs.All[kind], parent);
        node.Power = Tuning.SproutCost; // what it was grown with (paid by the parent for wild growth)
        return node;
    }

    PlantNode AddFruit(PlantNode parent) => AddNear(NodeSpecs.All[parent.Spec.Fruit!.Value], parent);

    // Places a new part just off its parent, heading away from the grandparent (and upward when upright).
    PlantNode AddNear(NodeSpec spec, PlantNode parent)
    {
        Vector2 away = parent.Parent == null
            ? (Layout == Layout.Upright ? -Vector2.UnitY : RandomDirection())
            : Vector2.Normalize(parent.Position - parent.Parent.Position + new Vector2(0.001f, 0));
        if (Layout == Layout.Upright) away = Vector2.Normalize(away - 0.6f * Vector2.UnitY);
        float angle = (float)(rng.NextDouble() - 0.5) * 1.6f;
        var dir = Vector2.Transform(away, Matrix3x2.CreateRotation(angle));
        float distance = (parent.Spec.Radius + spec.Radius) * 0.8f;
        var node = Add(spec, parent, parent.Position + dir * distance);
        node.Velocity = dir * 0.3f;
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

    // One fixed step of the economy (DESIGN.md, Economy), then growth:
    //   - the core and generators make power into their own store (anything past full is lost);
    //   - every other part pays its upkeep from its own store. One that can't wilts and stops draining, and recovers
    //     once the power flowing in has filled it back to RecoverFill. Wilting never removes anything;
    //   - a lab that paid makes genes, sent to the core a blob at a time; genes reaching the core raise
    //     the caps along the unlock track (see Deliver);
    //   - power flows along each link downhill, from the fuller part to the emptier, so it spreads out from the
    //     sources and a branch far from them runs low first (there is no global pool);
    //   - every SproutEverySeconds a wild sprout grows where there's power to spare; if there's none, growth waits.
    // Returns how many parts sprouted.
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
            float genes = GenesPerHour(node);
            if (genes <= 0) continue;
            node.Genes += genes * hours;
            while (node.Genes >= Tuning.GeneBlobSize - 1e-4f) // (a whole blob, give or take rounding)
            {
                node.Genes = Math.Max(0, node.Genes - Tuning.GeneBlobSize);
                SendGenes(node, Tuning.GeneBlobSize);
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

        if (!WildGrowth) return 0;
        GrowthClock += seconds;
        if (GrowthClock < Tuning.SproutEverySeconds) return 0;
        // While a prune can be undone, wild growth waits, so it can't take the room the undo needs.
        if (lastPrune != null || Sprout(wild: true) == null)
        {
            GrowthClock = Tuning.SproutEverySeconds; // no power to spare, or no room: try again next step
            return 0;
        }
        GrowthClock -= Tuning.SproutEverySeconds;
        return 1;
    }

    // Power a part makes per real hour. A generator's fruit adds a little, up to FruitBonusMax.
    public static float ProductionPerHour(PlantNode node) => node.Spec.Kind switch
    {
        NodeKind.Core => Tuning.CorePowerPerHour,
        NodeKind.Generator => Tuning.GeneratorPowerPerHour
            * (1 + Math.Min(Tuning.FruitBonusMax, node.Fruits.Count * Tuning.FruitBonusEach)),
        _ => 0,
    };

    // Power a part costs per real hour while it isn't wilting. Each leaf halves a stem's.
    public static float UpkeepPerHour(PlantNode node) => node.Spec.Kind == NodeKind.Stem
        ? Tuning.StemUpkeepPerHour * MathF.Pow(Tuning.LeafUpkeepFactor, node.Fruits.Count(f => f.Spec.Kind == NodeKind.Leaf))
        : KindUpkeepPerHour(node.Spec.Kind);

    // What a new part of this kind costs per hour (no fruit yet).
    public static float KindUpkeepPerHour(NodeKind kind) => kind switch
    {
        NodeKind.Stem => Tuning.StemUpkeepPerHour,
        NodeKind.Lab => Tuning.LabUpkeepPerHour,
        _ => 0,
    };

    // Genes a part makes per real hour while it isn't wilting: only labs, their research fruit adding a little.
    public static float GenesPerHour(PlantNode node) => node.Spec.Kind == NodeKind.Lab
        ? Tuning.LabGenesPerHour * (1 + Math.Min(Tuning.FruitBonusMax, node.Fruits.Count * Tuning.FruitBonusEach))
        : 0;

    // ==== GENES AND UNLOCKS ==== \\

    // A lab's blob of genes leaves for the core. Its genes count at the core in this same step, so a catch-up (which
    // runs no physics) and a live session agree; the blob is their journey made visible, a few seconds of travel up
    // the branch. In a catch-up, or with too many already in flight, it isn't shown.
    void SendGenes(PlantNode lab, float genes)
    {
        Deliver(genes);
        if (!ShowBlobs || Motes.Count >= Tuning.MaxMotes || lab.Parent == null) return;
        Motes.Add(new Mote(lab, lab.Parent, lab.Spec.Rgb) { Homing = true });
    }

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
    // plant looks the same at 1x and 60x. Motes are the blobs that do travel: genes from the labs (see SendGenes),
    // fruit to come.
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
            var next = mote.Homing ? arrived.Parent
                : arrived.Spec.IsFruit || ++mote.Hops >= Tuning.MoteMaxHops ? null : NextHop(arrived, mote.From);
            if (next == null)
            {
                Motes.RemoveAt(i);
                continue;
            }
            mote.From = arrived;
            mote.To = next;
            mote.T -= 1;
        }
    }

    // Motes mostly flow outward, towards the tips and fruit, but now and then double back.
    PlantNode? NextHop(PlantNode at, PlantNode? cameFrom)
    {
        var options = at.Neighbours().Where(n => n != cameFrom && (n != at.Parent || rng.NextDouble() < 0.15)).ToList();
        return options.Count == 0 ? null : options[rng.Next(options.Count)];
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
        Layout, GrowthClock, EconomyClock, Capacity,
        Nodes.Select(n => new NodeSave(n.Id, n.Spec.Kind, n.Parent?.Id ?? -1, n.Position.X, n.Position.Y,
            n.Parent != null && n.Fixed, n.Power, n.Wilting, n.Genes)).ToList(),
        Genes, Unlocks);

    // Parts are saved in the order they grew, so a parent always comes before its children.
    public static Garden FromSave(GardenSave save, int? seed = null)
    {
        var garden = new Garden(save.Layout, seed)
        {
            GrowthClock = save.GrowthClock,
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
            node.Genes = n.Genes;
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
    float Genes = 0);

public sealed record GardenSave(Layout Layout, double GrowthClock, double EconomyClock, Capacity Capacity, List<NodeSave> Nodes,
    double Genes = 0, int Unlocks = 0);
