using Godot;
using Num = System.Numerics;

namespace SpaceGarden;

// Draws the garden each frame, back to front: pruned parts tumbling away, stems, soft halos, the parts, pins and the
// ring on the part under the pointer, then the motes on top. Nothing here changes the garden.
//
// Each layer is a child canvas item drawn in as few draw calls as possible, however big the plant: the halos, the
// motes and each kind of shape are one instanced batch apiece (a textured quad per part), the stems a few line
// batches grouped by width. Drawing every part separately cost about four draw calls a part (884 a frame at 220
// parts), which was most of the widget's CPU time.
public partial class GardenView : Node2D
{
    // A pruned part on its way out: it keeps its last motion, falls, spins and fades.
    sealed class Falling
    {
        public Vector2 Position, Velocity;
        public float Rotation, Spin, Radius, Life = 1;
        public required NodeSpec Spec;
    }

    const float FallGravity = 260; // px/s²
    const float FallSeconds = 1.4f;
    const float RimWidth = 1.2f;   // the lighter outline round each part, in garden units

    public PlantNode? Highlight;

    readonly Garden garden;
    readonly List<Falling> falling = new();
    readonly Node2D fallingLayer = new(), linkLayer = new(), haloLayer = new(), shapeLayer = new(), markLayer = new(), moteLayer = new();
    readonly Batch halos = new(), moteHalos = new(), moteDots = new();
    readonly Dictionary<Shape, Batch> shapes = new();
    readonly Dictionary<int, (List<Vector2> Points, List<Color> Colours)> linkWidths = new();
    float highlightRing;
    float blend = 1;

    public GardenView(Garden garden)
    {
        this.garden = garden;
        foreach (var shape in Enum.GetValues<Shape>()) shapes[shape] = new Batch();
        shapeLayer.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        moteLayer.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        fallingLayer.Draw += DrawFalling;
        linkLayer.Draw += DrawLinks;
        haloLayer.Draw += DrawHalos;
        shapeLayer.Draw += DrawShapes;
        markLayer.Draw += DrawMarks;
        moteLayer.Draw += DrawMotes;
        foreach (var layer in new[] { fallingLayer, linkLayer, haloLayer, shapeLayer, markLayer, moteLayer }) AddChild(layer);
    }

    public void Drop(IEnumerable<PlantNode> nodes)
    {
        foreach (var n in nodes)
            falling.Add(new Falling
            {
                Spec = n.Spec,
                Position = V(n.Position),
                Velocity = V(n.Velocity) * Tuning.TicksPerSecond + new Vector2((float)GD.RandRange(-30.0, 30.0), (float)GD.RandRange(-60.0, -10.0)),
                Rotation = n.Rotation,
                Spin = (float)GD.RandRange(-3.0, 3.0),
                Radius = n.Spec.Radius,
            });
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        for (int i = falling.Count - 1; i >= 0; i--)
        {
            var f = falling[i];
            f.Velocity.Y += FallGravity * dt;
            f.Position += f.Velocity * dt;
            f.Rotation += f.Spin * dt;
            f.Life -= dt / FallSeconds;
            if (f.Life <= 0) falling.RemoveAt(i);
        }
        highlightRing = Mathf.MoveToward(highlightRing, Highlight == null ? 0 : 1, dt * 6);
        blend = (float)Engine.GetPhysicsInterpolationFraction();
        if (falling.Count > 0) fallingLayer.QueueRedraw();
        linkLayer.QueueRedraw();
        haloLayer.QueueRedraw();
        shapeLayer.QueueRedraw();
        markLayer.QueueRedraw();
        moteLayer.QueueRedraw();
    }

    // Where to draw a part this frame: blended between its last two physics ticks by how far the frame is into the
    // next, so motion is smooth at any frame rate (30 when idle, the monitor's rate when active) and not stepped
    // to the 60 Hz tick.
    Vector2 At(PlantNode node) => V(Num.Vector2.Lerp(node.PreviousPosition, node.Position, blend));

    float Size(PlantNode node) => node.Spec.Radius * GrowIn(node) * (1 + 0.15f * node.Glow);

    // ==== LAYERS ==== \\

    // Few at a time and short-lived, so drawn one by one.
    void DrawFalling()
    {
        foreach (var f in falling)
        {
            var fill = Rgb(f.Spec.Rgb);
            fill.A = f.Life;
            fallingLayer.DrawColoredPolygon(Place(UnitPolygon(f.Spec.Shape), f.Position, f.Radius * (0.6f + 0.4f * f.Life), f.Rotation), fill);
        }
    }

    // One antialiased line batch per stem width (the stems thin with depth).
    void DrawLinks()
    {
        foreach (var (points, colours) in linkWidths.Values)
        {
            points.Clear();
            colours.Clear();
        }
        foreach (var link in garden.Links)
        {
            var colour = Shade(Rgb(link.Target.Spec.Rgb), 0.45f + 0.55f * link.Flash);
            colour.A = Mathf.Min(1, GrowIn(link.Target) * 1.5f);
            float width = link.Target.Spec.IsFruit ? 1.2f : Mathf.Max(1.2f, 3.4f - link.Target.Depth * 0.18f);
            int key = Mathf.RoundToInt(width * 5);
            if (!linkWidths.TryGetValue(key, out var batch)) linkWidths[key] = batch = (new(), new());
            batch.Points.Add(At(link.Origin));
            batch.Points.Add(At(link.Target));
            batch.Colours.Add(colour);
        }
        foreach (var (key, (points, colours)) in linkWidths)
            if (points.Count > 0) linkLayer.DrawMultilineColors(points.ToArray(), colours.ToArray(), key / 5f, true);
    }

    void DrawHalos()
    {
        halos.Begin();
        foreach (var node in garden.Nodes)
        {
            var colour = Rgb(node.Spec.Rgb);
            colour.A = 0.3f + 0.4f * node.Glow;
            halos.Add(At(node), Size(node) * 3.6f, 0, colour);
        }
        halos.Draw(haloLayer, Textures.Halo);
    }

    // Each part is its shape twice: a lighter rim, then the fill a little smaller on top of it.
    void DrawShapes()
    {
        foreach (var batch in shapes.Values) batch.Begin();
        foreach (var node in garden.Nodes)
        {
            float size = Size(node);
            if (size < 0.2f) continue;
            var fill = Shade(Rgb(node.Spec.Rgb), 0.75f + 0.45f * node.Glow);
            var batch = shapes[node.Spec.Shape];
            var at = At(node);
            batch.Add(at, Textures.ShapeDiameter(size + RimWidth / 2), node.Rotation, fill.Lightened(0.35f));
            batch.Add(at, Textures.ShapeDiameter(Mathf.Max(0.1f, size - RimWidth / 2)), node.Rotation, fill);
        }
        foreach (var (shape, batch) in shapes) batch.Draw(shapeLayer, Textures.Shape(shape));
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
    }

    void DrawMotes()
    {
        moteHalos.Begin();
        moteDots.Begin();
        foreach (var mote in garden.Motes)
        {
            var p = At(mote.From).Lerp(At(mote.To), mote.T);
            var colour = Rgb(mote.Rgb);
            colour.A = 0.85f;
            moteHalos.Add(p, 26, 0, colour);
            moteDots.Add(p, Textures.ShapeDiameter(2.6f), 0, Colors.White);
        }
        moteHalos.Draw(moteLayer, Textures.Halo);
        moteDots.Draw(moteLayer, Textures.Shape(Shape.Circle));
    }

    // ==== HELPERS ==== \\

    // Parts swell in over their first second and a half, overshooting a little like a bud opening.
    static float GrowIn(PlantNode node)
    {
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

    static Vector2[] Place(Vector2[] unit, Vector2 centre, float radius, float rotation)
    {
        var points = new Vector2[unit.Length];
        for (int i = 0; i < unit.Length; i++) points[i] = centre + unit[i].Rotated(rotation) * radius;
        return points;
    }

    // The TS game's colorLuminance: scale the colour's channels, keep its alpha.
    static Color Shade(Color c, float lum) => new(Mathf.Min(1, c.R * lum), Mathf.Min(1, c.G * lum), Mathf.Min(1, c.B * lum), c.A);

    public static Color Rgb(uint rgb) => new((rgb << 8) | 0xFF);

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

    public void Add(Vector2 centre, float diameter, float rotation, Color colour)
    {
        if ((count + 1) * Floats > buffer.Length) Array.Resize(ref buffer, buffer.Length * 2);
        float cos = Mathf.Cos(rotation) * diameter, sin = Mathf.Sin(rotation) * diameter;
        int i = count++ * Floats;
        buffer[i] = cos; buffer[i + 1] = -sin; buffer[i + 2] = 0; buffer[i + 3] = centre.X;
        buffer[i + 4] = sin; buffer[i + 5] = cos; buffer[i + 6] = 0; buffer[i + 7] = centre.Y;
        buffer[i + 8] = colour.R; buffer[i + 9] = colour.G; buffer[i + 10] = colour.B; buffer[i + 11] = colour.A;
    }

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
