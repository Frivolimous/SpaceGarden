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
// Two clocks: Step() is one 60 Hz physics tick and only moves things; Grow() takes real seconds and only adds
// parts. Speeding growth up (or catching up time away) never changes how the plant moves.
public sealed class Garden
{
    public readonly List<PlantNode> Nodes = new();
    public readonly List<Link> Links = new();
    public readonly List<Mote> Motes = new();
    public readonly Layout Layout;
    public PlantNode Core => Nodes[0];
    // Only an upright plant's core is planted; a radial one floats like any other part.
    public bool Planted => Layout == Layout.Upright;

    // Seconds of motion (drives the wind), and real seconds of growth not yet spent on a sprout.
    public double Time;
    public double GrowthClock;

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
    readonly Dictionary<PlantNode, float> moteTimers = new();
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
        return garden;
    }

    // ==== GROWTH ==== \\

    // Spends real seconds on growth; returns how many parts sprouted.
    public int Grow(double seconds)
    {
        GrowthClock += seconds;
        int sprouted = 0;
        while (GrowthClock >= Tuning.SproutEverySeconds)
        {
            GrowthClock -= Tuning.SproutEverySeconds;
            if (Sprout() == null)
            {
                GrowthClock = 0; // full: don't bank growth for later
                break;
            }
            sprouted++;
        }
        return sprouted;
    }

    // Time away: grow for it (capped), then let the physics settle so the plant isn't shown mid-tangle.
    public int CatchUp(double secondsAway)
    {
        int sprouted = Grow(Math.Clamp(secondsAway, 0, Tuning.CatchUpCapSeconds));
        if (sprouted > 0)
        {
            for (int i = 0; i < Tuning.SettleTicks; i++) Step();
            foreach (var node in Nodes) node.Age = Math.Max(node.Age, 10);
        }
        return sprouted;
    }

    // One new part somewhere on the plant, or null if it's full.
    public PlantNode? Sprout()
    {
        if (Nodes.Count >= Tuning.MaxNodes) return null;
        var branchable = Nodes.Where(n => n.CanBranch).ToList();
        var fruitable = Nodes.Where(n => n.CanFruit).ToList();
        bool fruit = branchable.Count == 0 || (fruitable.Count > 0 && rng.NextDouble() < Tuning.FruitChance);
        if (fruit)
        {
            if (fruitable.Count == 0) return null;
            return AddFruit(Pick(fruitable));
        }
        return AddBranch(Pick(branchable));
    }

    // A sprout the player asked for by clicking a part: a branch if it has room, else a fruit.
    public PlantNode? SproutFrom(PlantNode node)
    {
        if (Nodes.Count >= Tuning.MaxNodes) return null;
        if (node.Spec.IsFruit && node.Parent != null) node = node.Parent;
        if (node.CanBranch) return AddBranch(node);
        if (node.CanFruit) return AddFruit(node);
        return null;
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
        foreach (var n in removed)
        {
            linkToParent.Remove(n);
            moteTimers.Remove(n);
        }
        if (Held != null && gone.Contains(Held)) LetGo();
        return removed;
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

    PlantNode AddBranch(PlantNode parent)
    {
        var kind = NodeKind.Stem;
        if (parent.Depth >= 2)
        {
            double roll = rng.NextDouble();
            kind = roll < 0.74 ? NodeKind.Stem : roll < 0.88 ? NodeKind.Generator : NodeKind.Lab;
        }
        return AddNear(NodeSpecs.All[kind], parent);
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

    // ==== PHYSICS ==== \\

    // One 60 Hz tick: springs, repulsion, light and wind, then movement. Also moves the motes and fades glows,
    // so everything that animates runs on the same fixed tick.
    public void Step()
    {
        const float dt = 1f / Tuning.TicksPerSecond;
        Time += dt;
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

    // ==== MOTES ==== \\

    void UpdateMotes(float dt)
    {
        foreach (var node in Nodes)
        {
            node.Age += dt;
            node.Glow = Math.Max(0, node.Glow - Tuning.GlowFadePerSecond * dt);
            float every = node.Spec.Kind switch
            {
                NodeKind.Core => Tuning.CoreMoteEvery,
                NodeKind.Generator => Tuning.GeneratorMoteEvery,
                _ => 0,
            };
            if (every == 0) continue;
            moteTimers[node] = moteTimers.GetValueOrDefault(node, (float)rng.NextDouble() * every) - dt;
            if (moteTimers[node] > 0) continue;
            moteTimers[node] += every;
            var first = NextHop(node, null);
            if (first != null) Motes.Add(new Mote(node, first, node.Spec.Kind == NodeKind.Core ? 0xFFFFD0u : 0xE5E570u));
        }
        foreach (var link in Links) link.Flash = Math.Max(0, link.Flash - Tuning.LinkFlashFadePerSecond * dt);

        for (int i = Motes.Count - 1; i >= 0; i--)
        {
            var mote = Motes[i];
            mote.T += dt / Tuning.MoteLinkSeconds;
            if (mote.T < 1) continue;

            var arrived = mote.To;
            arrived.Glow = 1;
            if (LinkBetween(mote.From, arrived) is { } link) link.Flash = 1;
            var next = arrived.Spec.IsFruit || ++mote.Hops >= Tuning.MoteMaxHops ? null : NextHop(arrived, mote.From);
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
        Layout, GrowthClock,
        Nodes.Select(n => new NodeSave(n.Id, n.Spec.Kind, n.Parent?.Id ?? -1, n.Position.X, n.Position.Y, n.Parent != null && n.Fixed)).ToList());

    // Parts are saved in the order they grew, so a parent always comes before its children.
    public static Garden FromSave(GardenSave save, int? seed = null)
    {
        var garden = new Garden(save.Layout, seed) { GrowthClock = save.GrowthClock };
        var byId = new Dictionary<int, PlantNode>();
        foreach (var n in save.Nodes)
        {
            var parent = n.ParentId < 0 ? null : byId[n.ParentId];
            var node = garden.Add(NodeSpecs.All[n.Kind], parent, new Vector2(n.X, n.Y));
            node.Age = 10;
            node.Fixed = (parent == null && garden.Planted) || n.Pinned;
            byId[n.Id] = node;
        }
        return garden;
    }
}

public sealed record NodeSave(int Id, NodeKind Kind, int ParentId, float X, float Y, bool Pinned = false);

public sealed record GardenSave(Layout Layout, double GrowthClock, List<NodeSave> Nodes);
