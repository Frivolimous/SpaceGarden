using Godot;
using Num = System.Numerics;

namespace SpaceGarden;

// Draws the garden each frame: stems, then soft halos, then the parts, then the motes on top, plus the ring on the
// part under the pointer and pruned parts tumbling away. Everything is immediate-mode drawing from the Core state;
// nothing here changes the garden.
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
    const float MinShapeSize = 1.5f; // garden units; smaller parts are drawn as dots

    public PlantNode? Highlight;

    readonly Garden garden;
    readonly Texture2D halo = MakeHalo();
    readonly Dictionary<Shape, Vector2[]> unitShapes = new();
    readonly List<Falling> falling = new();
    float highlightRing;

    public GardenView(Garden garden)
    {
        this.garden = garden;
        foreach (var shape in Enum.GetValues<Shape>()) unitShapes[shape] = UnitPolygon(shape);
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
        QueueRedraw();
    }

    // Where to draw a part this frame: blended between its last two physics ticks by how far the frame is into the
    // next, so motion is smooth at any frame rate (30 when idle, the monitor's rate when active) and not stepped
    // to the 60 Hz tick.
    Vector2 At(PlantNode node) => V(Num.Vector2.Lerp(node.PreviousPosition, node.Position, blend));

    float blend = 1;

    public override void _Draw()
    {
        blend = (float)Engine.GetPhysicsInterpolationFraction();
        foreach (var f in falling)
        {
            var fill = Rgb(f.Spec.Rgb);
            fill.A = f.Life;
            DrawColoredPolygon(Place(unitShapes[f.Spec.Shape], f.Position, f.Radius * (0.6f + 0.4f * f.Life), f.Rotation), fill);
        }

        foreach (var link in garden.Links)
        {
            var colour = Shade(Rgb(link.Target.Spec.Rgb), 0.45f + 0.55f * link.Flash);
            colour.A = Mathf.Min(1, GrowIn(link.Target) * 1.5f);
            float width = Mathf.Max(1.2f, 3.4f - link.Target.Depth * 0.18f);
            if (link.Target.Spec.IsFruit) width = 1.2f;
            DrawLine(At(link.Origin), At(link.Target), colour, width, true);
        }

        foreach (var node in garden.Nodes)
        {
            float size = node.Spec.Radius * GrowIn(node) * (1 + 0.15f * node.Glow);
            var colour = Rgb(node.Spec.Rgb);
            colour.A = 0.3f + 0.4f * node.Glow;
            DrawHalo(At(node), size * 3.6f, colour);
        }

        foreach (var node in garden.Nodes)
        {
            float size = node.Spec.Radius * GrowIn(node) * (1 + 0.15f * node.Glow);
            if (size < 0.2f) continue;
            var fill = Shade(Rgb(node.Spec.Rgb), 0.75f + 0.45f * node.Glow);
            // A part just sprouting is a fraction of a pixel across, too small for Godot to triangulate its shape
            // ("Invalid polygon data"); at that size a dot looks the same.
            if (size < MinShapeSize)
            {
                DrawCircle(At(node), size, fill);
                continue;
            }
            var points = Place(unitShapes[node.Spec.Shape], At(node), size, node.Rotation);
            DrawColoredPolygon(points, fill);
            var rim = fill.Lightened(0.35f);
            DrawPolyline([.. points, points[0]], rim, 1.2f, true);
            if (node.Fixed && node.Parent != null)
            {
                DrawCircle(At(node), Mathf.Max(2, size * 0.32f), new Color(1, 0.9f, 0.45f));
                DrawArc(At(node), Mathf.Max(2, size * 0.32f), 0, Mathf.Tau, 16, new Color(0, 0, 0, 0.5f), 1, true);
            }
        }

        if (Highlight != null && highlightRing > 0)
        {
            float r = Highlight.Spec.Radius * GrowIn(Highlight) + 4 + 2 * (1 - highlightRing);
            // White on a soft dark edge, so it shows over a light window as well as a dark one.
            DrawArc(At(Highlight), r, 0, Mathf.Tau, 40, new Color(0, 0, 0, 0.35f * highlightRing), 3.5f, true);
            DrawArc(At(Highlight), r, 0, Mathf.Tau, 40, new Color(1, 1, 1, 0.8f * highlightRing), 1.5f, true);
        }

        foreach (var mote in garden.Motes)
        {
            var p = At(mote.From).Lerp(At(mote.To), mote.T);
            var colour = Rgb(mote.Rgb);
            colour.A = 0.85f;
            DrawHalo(p, 26, colour);
            DrawCircle(p, 2.6f, Colors.White);
        }
    }

    // Parts swell in over their first second and a half, overshooting a little like a bud opening.
    static float GrowIn(PlantNode node)
    {
        float t = Mathf.Clamp(node.Age / 1.5f, 0, 1);
        const float c1 = 1.70158f, c3 = c1 + 1;
        return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
    }

    void DrawHalo(Vector2 centre, float diameter, Color colour) =>
        DrawTextureRect(halo, new Rect2(centre - new Vector2(diameter, diameter) / 2, new Vector2(diameter, diameter)), false, colour);

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

    static Vector2[] UnitPolygon(Shape shape)
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
