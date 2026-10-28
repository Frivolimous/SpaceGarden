using Godot;
using Num = System.Numerics;

namespace SpaceGarden;

// Draws the garden each frame, back to front: stems, the motes travelling along them, soft halos (and the glow where
// a mote is absorbed), the parts, then pins and the ring on the part under the pointer. The parts of a branch just
// pruned drift off loose, fading out. Nothing here changes the garden.
//
// Each layer is a child canvas item drawn in as few draw calls as possible, however big the plant: the stems, the
// halos, the motes and each kind of shape are one instanced batch apiece (a textured quad per part, or per stem,
// stretched along it). Drawing every part separately cost about four draw calls a part (884 a frame at 220 parts),
// which was most of the widget's CPU time.
public partial class GardenView : Node2D
{
    // A part's outline and fill brightness, from empty (Dim) to full (Dim + Lit): the outline bright, the fill dark.
    const float OutlineDim = 0.55f, OutlineLit = 0.6f, FillDim = 0.12f, FillLit = 0.3f;

    // The outline's width in garden units: a little wider on big parts (about 3.4 on the core, 1.4 on a fruit).
    static float OutlineWidth(float radius) => 1 + 0.08f * radius;

    // The dark edge round each part and under each stem (in garden units, added to the radius or width).
    const float EdgeWidth = 1.4f;
    static readonly Color Edge = new(0, 0, 0, 0.55f);

    public PlantNode? Highlight;

    // While the garden can't be seen nothing is redrawn (Godot then renders nothing at all).
    public bool Frozen;

    readonly Garden garden;
    readonly Node2D linkLayer = new(), haloLayer = new(), shapeLayer = new(), formingLayer = new(),
        markLayer = new(), moteLayer = new();
    readonly Batch links = new(), linkEdges = new(), lit = new(), halos = new(), moteDots = new();
    // A power flash's light flooding along its stem, then draining: done before the next (at most 3 a second).
    const float FillSeconds = 0.1f, DrainSeconds = 0.2f;
    readonly Dictionary<Shape, Batch> shapes = new(), forming = new();
    float highlightRing;
    float blend = 1;

    // Small moments (marks): a ring widening off a part as it pops out, a glint on a fruit as it appears, and a
    // ripple out from the core as genes unlock a step of the track (seconds since each began).
    const float PopSeconds = 0.8f, RippleSeconds = 2.4f, RippleGap = 0.4f;
    static readonly Color RippleTint = new(0.82f, 0.74f, 1f); // the genes' purple, lightened (as the unlock note)
    readonly List<float> ripples = new();
    int unlocksSeen;
    float coreGlow;
    readonly HashSet<PlantNode> restored = new(); // put back by an undo: they grow in again, with no moment

    public void Restored(IEnumerable<PlantNode> nodes) => restored.UnionWith(nodes);

    public GardenView(Garden garden)
    {
        this.garden = garden;
        unlocksSeen = garden.Unlocks;
        foreach (var shape in Enum.GetValues<Shape>())
        {
            shapes[shape] = new Batch();
            forming[shape] = new Batch();
        }
        shapeLayer.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        formingLayer.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        linkLayer.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        moteLayer.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        linkLayer.Draw += DrawLinks;
        haloLayer.Draw += DrawHalos;
        shapeLayer.Draw += DrawShapes;
        formingLayer.Draw += DrawForming;
        markLayer.Draw += DrawMarks;
        moteLayer.Draw += DrawMotes;
        foreach (var layer in new[] { linkLayer, moteLayer, haloLayer, shapeLayer, formingLayer, markLayer }) AddChild(layer);
    }

    public override void _Process(double delta)
    {
        if (Frozen) return;
        float dt = (float)delta;
        highlightRing = Mathf.MoveToward(highlightRing, Highlight == null ? 0 : 1, dt * 6);
        // Genes count as their blob leaves the lab, so an unlock waits for the gene blobs on their way to arrive (the
        // core glows as each does), unless none are shown.
        if (garden.Unlocks > unlocksSeen)
        {
            bool arrived = garden.Core.Glow > coreGlow;
            if (arrived || !garden.Motes.Any(m => m.Absorbed && m.Route[^1] == garden.Core))
            {
                unlocksSeen = garden.Unlocks;
                ripples.Add(0);
            }
        }
        coreGlow = garden.Core.Glow;
        for (int i = ripples.Count - 1; i >= 0; i--)
            if ((ripples[i] += dt) >= RippleSeconds + RippleGap) ripples.RemoveAt(i);
        blend = (float)Engine.GetPhysicsInterpolationFraction();
        linkLayer.QueueRedraw();
        haloLayer.QueueRedraw();
        shapeLayer.QueueRedraw();
        formingLayer.QueueRedraw();
        markLayer.QueueRedraw();
        moteLayer.QueueRedraw();
    }

    // Where to draw a part this frame: blended between its last two physics ticks by how far the frame is into the
    // next, so motion is smooth at any frame rate (30 when idle, the monitor's rate when active) and not stepped
    // to the 60 Hz tick.
    Vector2 At(PlantNode node) => V(Num.Vector2.Lerp(node.PreviousPosition, node.Position, blend));

    // A part's brightness and size are its power level (DESIGN.md, Visual language), as in the original
    // (PlantNodeView.intensityInterpolation): its shown fill, never below FillLookFloor, plus the bounce from power
    // arriving. Full parts are full size; a drained one shrinks to about two thirds and dims, so the plant's shape
    // still reads when it's low.
    static float Level(PlantNode node) => Mathf.Max(Tuning.FillLookFloor, node.FillLook) + node.Bounce;

    // An empty part is already small and dim, so wilting only greys it and dims it a little more.
    const float WiltDim = 0.2f, WiltDesaturate = 0.5f;

    float Size(PlantNode node) => node.Spec.Radius * GrowIn(node) * (0.55f + 0.45f * Level(node));

    static Color Wilted(Color c, float wilt)
    {
        if (wilt <= 0) return c;
        float grey = (c.R + c.G + c.B) / 3;
        var muted = c.Lerp(new Color(grey, grey, grey, c.A), WiltDesaturate * wilt);
        return Shade(muted, 1 - WiltDim * wilt);
    }

    // ==== LAYERS ==== \\

    // Each stem is a soft-edged quad stretched from end to end, tapering from its parent's end to its own (the taper
    // is in the texture: see Textures.Taper), over a wider dark one so it shows over a light window. The stems thin
    // with depth too.
    void DrawLinks()
    {
        links.Begin();
        linkEdges.Begin();
        lit.Begin();
        foreach (var link in garden.Links)
        {
            AddLink(link);
            if (link.FlashAge < FillSeconds + DrainSeconds) AddLit(link);
        }
        linkEdges.Draw(linkLayer, Textures.Taper);
        links.Draw(linkLayer, Textures.Taper);
        lit.Draw(linkLayer, Textures.Line);
    }

    // A power flash lights its whole stem, as in the original, but in a wipe from the end the power came from: the
    // light floods along to the far end, then drains away from the same end it came in by. As bright as the flash
    // (the fuller the part it reached, the brighter). Drawn over the stem at rest, an even width a little inside
    // the stem's where the lit stretch is.
    void AddLit(Link link)
    {
        float age = link.FlashAge;
        float head = Mathf.Min(1, age / FillSeconds);
        float tail = Mathf.Clamp((age - FillSeconds) / DrainSeconds, 0, 1);
        if (head - tail < 0.01f) return;
        var (from, to) = link.FlowsToTarget ? (link.Origin, link.Target) : (link.Target, link.Origin);
        var a = At(from);
        var along = At(to) - a;
        float length = along.Length();
        if (length < 0.01f) return;
        float middle = (head + tail) / 2; // how far along from the end the power left
        float fromParent = link.FlowsToTarget ? middle : 1 - middle;
        float width = LinkWidth(link.Target) * Mathf.Lerp(1, Textures.TaperEnd, fromParent) * 0.9f;
        var colour = Wilted(Shade(ColourOf(link.Target.Spec), 0.45f + 0.9f * link.FlashStrength), link.Target.WiltLook);
        colour.A = Mathf.Min(1, GrowIn(link.Target) * 1.5f);
        var normal = new Vector2(-along.Y, along.X) / length / Textures.LineCore * width;
        lit.AddAxes(a + along * middle, along * (head - tail), normal, colour);
    }

    void AddLink(Link link)
    {
        // As the original's link (FDGLink.getColor): its target's colour, dim at rest (lit by a power flash: see
        // AddLit). (The original rested at 30%; 45% keeps the stems readable over a dark desktop.)
        var colour = Wilted(Shade(ColourOf(link.Target.Spec), 0.45f), link.Target.WiltLook);
        colour.A = Mathf.Min(1, GrowIn(link.Target) * 1.5f);
        float width = LinkWidth(link.Target);
        var from = At(link.Origin);
        var along = At(link.Target) - from; // the texture's parent end is at the origin
        float length = along.Length();
        if (length < 0.01f) return;
        var normal = new Vector2(-along.Y, along.X) / length / Textures.LineCore;
        links.AddAxes(from + along / 2, along, normal * width, colour);
        var edge = Edge;
        edge.A *= colour.A;
        linkEdges.AddAxes(from + along / 2, along, normal * (width + EdgeWidth), edge);
    }

    // A stem's width at its parent's end (it tapers to TaperEnd of that at its own): thinning with depth, a fruit's
    // stalk thinnest.
    static float LinkWidth(PlantNode target) => target.Spec.IsFruit ? 1.3f : Mathf.Max(1.4f, 3.4f - target.Depth * 0.16f);

    // Under each part, a soft halo of its power; and where a mote was just absorbed, a glow of the mote's colour that
    // widens a little as it fades, so it's seen that something happened.
    void DrawHalos()
    {
        halos.Begin();
        foreach (var node in garden.Nodes)
        {
            var colour = Wilted(ColourOf(node.Spec), node.WiltLook);
            colour.A = 0.15f * Level(node) * Level(node) * (1 - 0.6f * node.WiltLook); // faint, and only on full parts
            halos.AddRound(At(node), Size(node) * 3.6f, colour);
            if (node.Glow <= 0) continue;
            // Tight round the part and strong: the halo twice over.
            var glow = Rgb(node.GlowRgb).Lightened(0.2f);
            glow.A = node.Glow;
            float radius = Mathf.Max(node.Spec.Radius * GrowIn(node), Size(node));
            float diameter = radius * (3.4f + 0.6f * (1 - node.Glow));
            halos.AddRound(At(node), diameter, glow);
            halos.AddRound(At(node), diameter, glow);
        }
        halos.Draw(haloLayer, Textures.Halo);
    }

    // Each part is its shape three times: a thin dark edge, so it stands out over a light window too; then, as in the
    // original, a crisp outline in its hue and a dark fill tinted with it a little smaller on top. Power brightens
    // both; even a drained part keeps an outline that reads. A pruned branch drifting off fades out as it goes.
    void DrawShapes()
    {
        foreach (var batch in shapes.Values) batch.Begin();
        foreach (var node in garden.Nodes) AddPart(node, 1);
        foreach (var cut in garden.Cut)
            foreach (var node in cut.Nodes) AddPart(node, cut.FadeOf(node));
        foreach (var (shape, batch) in shapes) batch.Draw(shapeLayer, Textures.Shape(shape));
    }

    void AddPart(PlantNode node, float alpha)
    {
        float size = Size(node);
        if (size < 0.2f) return;
        float level = Level(node);
        var colour = ColourOf(node.Spec);
        var outline = Wilted(Shade(colour, OutlineDim + OutlineLit * level), node.WiltLook);
        var fill = Wilted(Shade(colour, FillDim + FillLit * level), node.WiltLook);
        var edge = Edge;
        outline.A = fill.A = alpha;
        edge.A *= alpha;
        var batch = shapes[node.Spec.Shape];
        var at = At(node);
        var (sin, cos) = MathF.SinCos(node.Rotation);
        batch.AddTurned(at, Textures.ShapeDiameter(size + EdgeWidth), cos, sin, edge);
        batch.AddTurned(at, Textures.ShapeDiameter(size), cos, sin, outline);
        batch.AddTurned(at, Textures.ShapeDiameter(Mathf.Max(0.1f, size - OutlineWidth(node.Spec.Radius))), cos, sin, fill);
    }

    // What growth blobs are forming (DESIGN.md, Economy, Blobs), over the parts. A fruit bud swells from a speck to
    // its full size just off its part's rim, where it'll hang. A bud of a new part is a silhouette in its final hue
    // inside its parent, growing from a speck, until it pops out (see Garden.AddNear).
    void DrawForming()
    {
        foreach (var batch in forming.Values) batch.Begin();
        foreach (var node in garden.Nodes)
        {
            if (node.Forming is not { } kind || node.FormingLook <= 0) continue;
            var spec = NodeSpecs.All[kind];
            float grown = node.FormingLook / (spec.IsFruit ? Tuning.FruitGrowth : Tuning.BudGrowth);
            float level = Level(node) * GrowIn(node);
            var at = At(node);
            var (sin, cos) = MathF.SinCos(node.Rotation);
            var colour = Wilted(ColourOf(spec), node.WiltLook);
            float size;
            if (spec.IsFruit)
            {
                size = Mathf.Lerp(0.8f, spec.Radius, grown) * level;
                // Turning with its part, at a slot of its own.
                var dir = new Vector2(cos, sin).Rotated(node.Fruits.Count * 2.1f);
                at += dir * (node.Spec.Radius * Level(node) + size);
                colour.A = 0.9f;
            }
            else
            {
                // Bright, to show inside its parent's dark fill.
                size = Mathf.Lerp(0.8f, Mathf.Min(spec.Radius, node.Spec.Radius * 0.7f), grown) * level;
                colour.A = 0.75f;
            }
            forming[spec.Shape].AddTurned(at, Textures.ShapeDiameter(size), cos, sin, colour);
        }
        foreach (var (shape, batch) in forming) batch.Draw(formingLayer, Textures.Shape(shape));
    }

    // Pins and the hover ring: a handful at most.
    void DrawMarks()
    {
        foreach (var node in garden.Nodes)
        {
            if (!node.Fixed || node.Parent == null) continue;
            float r = Mathf.Max(2, Size(node) * 0.32f);
            markLayer.DrawCircle(At(node), r, new Color(1, 0.9f, 0.45f));
            markLayer.DrawArc(At(node), r, 0, Mathf.Tau, 16, new Color(0, 0, 0, 0.5f), 1, true);
        }

        if (Highlight != null && highlightRing > 0)
        {
            float r = Highlight.Spec.Radius * GrowIn(Highlight) + 4 + 2 * (1 - highlightRing);
            // White on a soft dark edge, so it shows over a light window as well as a dark one.
            markLayer.DrawArc(At(Highlight), r, 0, Mathf.Tau, 40, new Color(0, 0, 0, 0.35f * highlightRing), 3.5f, true);
            markLayer.DrawArc(At(Highlight), r, 0, Mathf.Tau, 40, new Color(1, 1, 1, 0.8f * highlightRing), 1.5f, true);
        }

        DrawMoments();
    }

    // The small moments: a handful at a time, and brief. Each on a soft dark edge, like the parts.
    void DrawMoments()
    {
        if (restored.Count > 0) restored.RemoveWhere(n => n.Age >= PopSeconds || !garden.Nodes.Contains(n));
        foreach (var node in garden.Nodes)
        {
            if (node.Age < 0 || node.Age >= PopSeconds || restored.Contains(node)) continue;
            float t = node.Age / PopSeconds;
            var colour = ColourOf(node.Spec).Lightened(0.35f);
            var at = At(node);
            if (node.Spec.IsFruit)
            {
                // A glint: a slim four-pointed sparkle that flares and goes, turning a little.
                float flare = Mathf.Sin(Mathf.Pi * t);
                float reach = node.Spec.Radius * 1.8f * flare;
                colour = colour.Lightened(0.5f);
                colour.A = flare;
                float turn = -Mathf.Pi / 2 + t * 0.5f;
                markLayer.DrawColoredPolygon(Sparkle(at, reach + 1, turn), new Color(0, 0, 0, 0.3f * flare));
                markLayer.DrawColoredPolygon(Sparkle(at, reach, turn), colour);
            }
            else
            {
                // Quiet: wild growth pops parts out often.
                float r = node.Spec.Radius * (1.1f + 0.8f * EaseOut(t));
                Ring(at, r, 0.5f + 1.5f * (1 - t), colour, 0.6f * (1 - t) * (1 - t));
            }
        }

        // The unlock ripple: two rings, one after the other, widening slowly from the core.
        var core = garden.Core;
        foreach (var age in ripples)
            for (int i = 0; i < 2; i++)
            {
                float t = (age - i * RippleGap) / RippleSeconds;
                if (t <= 0 || t >= 1) continue;
                float r = core.Spec.Radius * (1.05f + 0.45f * EaseOut(t)); // out to half again the core's size
                Ring(At(core), r, 0.5f + 2f * (1 - t), RippleTint, 0.75f * (1 - t));
            }
    }

    void Ring(Vector2 at, float radius, float width, Color colour, float alpha)
    {
        colour.A = alpha;
        markLayer.DrawArc(at, radius, 0, Mathf.Tau, 48, new Color(0, 0, 0, 0.35f * alpha), width + 2, true);
        markLayer.DrawArc(at, radius, 0, Mathf.Tau, 48, colour, width, true);
    }

    // Four long thin points about a centre.
    static Vector2[] Sparkle(Vector2 at, float reach, float turn)
    {
        var points = new Vector2[8];
        for (int i = 0; i < 8; i++)
            points[i] = at + Vector2.FromAngle(turn + i * Mathf.Pi / 4) * (i % 2 == 0 ? reach : reach * 0.2f);
        return points;
    }

    static float EaseOut(float t) => 1 - (1 - t) * (1 - t) * (1 - t);

    void DrawMotes()
    {
        moteDots.Begin();
        // A blob is a small dot of its colour (a gene blob purple, a growth blob orange) as wide as the stem it
        // travels along, under the parts, so it slips along the stems and in and out of them. The part a growth blob
        // completes stays hidden until it arrives (see GrowIn).
        foreach (var mote in garden.Motes)
        {
            var p = At(mote.From).Lerp(At(mote.To), mote.T);
            bool outward = mote.To.Parent == mote.From;
            var deeper = outward ? mote.To : mote.From;
            float width = LinkWidth(deeper) * Mathf.Lerp(1, Textures.TaperEnd, outward ? mote.T : 1 - mote.T);
            moteDots.AddRound(p, Textures.ShapeDiameter(width * 0.65f), Rgb(mote.Rgb).Lightened(0.3f));
        }
        moteDots.Draw(moteLayer, Textures.Shape(Shape.Circle));
    }

    // ==== HELPERS ==== \\

    // Parts swell in over their first second and a half, overshooting a little like a bud opening. A part whose
    // completing blob is still on its way has an Age below zero, and isn't drawn (nor its stalk) until then.
    static float GrowIn(PlantNode node)
    {
        if (node.Age >= 1.5f) return 1; // grown: almost every part, almost always
        float t = Mathf.Clamp(node.Age / 1.5f, 0, 1);
        const float c1 = 1.70158f, c3 = c1 + 1;
        return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
    }

    public static Vector2[] UnitPolygon(Shape shape)
    {
        int sides = shape switch
        {
            Shape.Triangle => 3,
            Shape.Square => 4,
            Shape.Pentagon => 5,
            Shape.Hexagon => 6,
            _ => 24,
        };
        var points = new Vector2[sides];
        for (int i = 0; i < sides; i++) points[i] = Vector2.FromAngle(Mathf.Tau * i / sides);
        return points;
    }

    // The TS game's colorLuminance: scale the colour's channels, keep its alpha.
    static Color Shade(Color c, float lum) => new(Mathf.Min(1, c.R * lum), Mathf.Min(1, c.G * lum), Mathf.Min(1, c.B * lum), c.A);

    public static Color Rgb(uint rgb) => new((rgb << 8) | 0xFF);

    // Each kind of part's colour, worked out once.
    static readonly Color[] kindColours = Enum.GetValues<NodeKind>().Select(k => Rgb(NodeSpecs.All[k].Rgb)).ToArray();

    static Color ColourOf(NodeSpec spec) => kindColours[(int)spec.Kind];

    public static Vector2 V(Num.Vector2 v) => new(v.X, v.Y);
}

// One instanced draw call: a textured unit quad per instance, each with its own centre, size, turn and colour.
// Filled each frame with Begin/Add, then sent to the GPU in one go with Draw.
public sealed class Batch
{
    const int Floats = 12; // per instance: a 2D transform (8) and a colour (4)

    static readonly ArrayMesh Quad = MakeQuad();

    readonly MultiMesh multiMesh = new()
    {
        TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
        UseColors = true,
        Mesh = Quad,
    };
    float[] buffer = new float[Floats * 64];
    float[] sent = [];
    int count;

    public void Begin() => count = 0;

    // A quad centred here, its unit square mapped onto the two axes (the quad's width along x, height along y).
    public void AddAxes(Vector2 centre, Vector2 x, Vector2 y, Color colour)
    {
        if ((count + 1) * Floats > buffer.Length) Array.Resize(ref buffer, buffer.Length * 2);
        int i = count++ * Floats;
        buffer[i] = x.X; buffer[i + 1] = y.X; buffer[i + 2] = 0; buffer[i + 3] = centre.X;
        buffer[i + 4] = x.Y; buffer[i + 5] = y.Y; buffer[i + 6] = 0; buffer[i + 7] = centre.Y;
        buffer[i + 8] = colour.R; buffer[i + 9] = colour.G; buffer[i + 10] = colour.B; buffer[i + 11] = colour.A;
    }

    // A square this wide, turned by the angle whose cosine and sine these are.
    public void AddTurned(Vector2 centre, float diameter, float cos, float sin, Color colour) =>
        AddAxes(centre, new Vector2(cos, sin) * diameter, new Vector2(-sin, cos) * diameter, colour);

    // A square this wide, unturned (for round things).
    public void AddRound(Vector2 centre, float diameter, Color colour) =>
        AddAxes(centre, new Vector2(diameter, 0), new Vector2(0, diameter), colour);

    public void Draw(CanvasItem onto, Texture2D texture)
    {
        if (count == 0) return;
        if (sent.Length != count * Floats)
        {
            sent = new float[count * Floats];
            multiMesh.InstanceCount = count;
        }
        Array.Copy(buffer, sent, sent.Length);
        multiMesh.Buffer = sent;
        onto.DrawMultimesh(multiMesh, texture);
    }

    static ArrayMesh MakeQuad()
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = new Vector2[] { new(-0.5f, -0.5f), new(0.5f, -0.5f), new(0.5f, 0.5f), new(-0.5f, 0.5f) };
        arrays[(int)Mesh.ArrayType.TexUV] = new Vector2[] { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };
        arrays[(int)Mesh.ArrayType.Index] = new[] { 0, 1, 2, 0, 2, 3 };
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}

// The textures the batches draw: a soft halo, and each part shape as smooth white coverage (supersampled, with
// mipmaps so small parts stay smooth too), tinted per part by the instance colour.
public static class Textures
{
    const int ShapeSize = 128;
    const int Supersample = 4;
    const float ShapeFill = 0.94f; // the shape's radius in the texture, leaving room for its soft edge

    public static readonly Texture2D Halo = MakeHalo();
    static readonly Dictionary<Shape, Texture2D> shapes = new();

    // A stem: solid across the middle LineCore of its height, fading out to the edges for a soft, antialiased look.
    // Stretched along the stem; the quad is drawn the stem's width / LineCore tall so the solid part is the width.
    public const float LineCore = 0.7f;
    public static readonly Texture2D Line = MakeLine(1);

    // The same, narrowing along its length (x) from its full width at the start to TaperEnd of it at the end.
    public const float TaperEnd = 0.55f;
    public static readonly Texture2D Taper = MakeLine(TaperEnd);

    static Texture2D MakeLine(float end)
    {
        const int height = 32;
        int width = end < 1 ? 64 : 4;
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (int x = 0; x < width; x++)
        {
            float scale = Mathf.Lerp(1, end, (x + 0.5f) / width);
            for (int y = 0; y < height; y++)
            {
                float fromMiddle = Mathf.Abs((y + 0.5f) / height - 0.5f) * 2 / scale; // 0 in the middle, 1 at the edges
                float alpha = Mathf.Clamp((1 - fromMiddle) / (1 - LineCore), 0, 1);
                image.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }

    public static Texture2D Shape(Shape shape)
    {
        if (!shapes.TryGetValue(shape, out var texture)) shapes[shape] = texture = MakeShape(shape);
        return texture;
    }

    // The quad size that draws a shape at this radius.
    public static float ShapeDiameter(float radius) => radius * 2 / ShapeFill;

    static Texture2D MakeShape(Shape shape)
    {
        var corners = GardenView.UnitPolygon(shape);
        var image = Image.CreateEmpty(ShapeSize, ShapeSize, false, Image.Format.Rgba8);
        for (int y = 0; y < ShapeSize; y++)
            for (int x = 0; x < ShapeSize; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < Supersample; sy++)
                    for (int sx = 0; sx < Supersample; sx++)
                    {
                        var p = new Vector2(x + (sx + 0.5f) / Supersample, y + (sy + 0.5f) / Supersample) / ShapeSize * 2 - Vector2.One;
                        if (Inside(shape, corners, p / ShapeFill)) inside++;
                    }
                image.SetPixel(x, y, new Color(1, 1, 1, inside / (float)(Supersample * Supersample)));
            }
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }

    static bool Inside(Shape shape, Vector2[] corners, Vector2 p)
    {
        if (shape == SpaceGarden.Core.Shape.Circle) return p.LengthSquared() <= 1;
        for (int i = 0; i < corners.Length; i++)
        {
            var a = corners[i];
            var b = corners[(i + 1) % corners.Length];
            if ((b - a).Cross(p - a) < 0) return false;
        }
        return true;
    }

    static Texture2D MakeHalo()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1, 1, 1, 1));
        gradient.SetColor(1, new Color(1, 1, 1, 0));
        gradient.AddPoint(0.35f, new Color(1, 1, 1, 0.35f));
        return new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1, 0.5f),
            Width = 64,
            Height = 64,
        };
    }
}
