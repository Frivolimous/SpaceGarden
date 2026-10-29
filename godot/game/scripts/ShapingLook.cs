using Godot;

namespace SpaceGarden;

// How the shaping tools look (DESIGN.md, Shaping the garden): quiet overlays in a pale moonlight blue, no role's hue,
// drawn in the parts' outline look (a dark edge, a crisp outline, a dark fill), each with a slow movement of its own
// that says what it does. Shared by the garden (GardenView, in garden units) and the shaping bar's icons (pixels).
public static class ShapingLook
{
    public static readonly Color Tint = new(0.74f, 0.84f, 0.98f);
    static readonly Color Edge = new(0, 0, 0, 0.5f);
    static readonly Color Fill = new(0.08f, 0.1f, 0.14f);

    public static string Name(ShapingKind kind) => kind switch
    {
        ShapingKind.Pot => "Gravity Pot",
        ShapingKind.Trellis => "Trellis",
        _ => "Gravity Well",
    };

    public static string Does(ShapingKind kind) => kind switch
    {
        ShapingKind.Pot => "Drop a part on it: the part is held there, and its branch grows the way the pot faces.",
        ShapingKind.Trellis => "Drop parts on it: they're held along its line.",
        _ => "Pulls the whole plant gently towards it.",
    };

    // A pot: a half ring cupping the back of a part of radius r centred at `at` (the side away from u, the way it
    // faces), just outside the part's edge, with its rim's two ends level across the part's middle.
    public static void Pot(CanvasItem ci, Vector2 at, Vector2 u, float r, Color tint)
    {
        const int steps = 16;
        float inner = r + ShapingTool.PotGap, outer = inner + ShapingTool.PotWall;
        float start = u.Angle() + Mathf.Pi / 2; // one rim end, then round the back to the other
        var points = new Vector2[2 * (steps + 1)];
        for (int i = 0; i <= steps; i++)
        {
            var dir = Vector2.FromAngle(start + Mathf.Pi * i / steps);
            points[i] = at + dir * outer;
            points[2 * steps + 1 - i] = at + dir * inner;
        }
        Outlined(ci, points, tint, 1);
    }

    // The pot's push, shown: small chevrons rising past its part (radius r) the way it faces, fading as they go.
    public static void Rising(CanvasItem ci, Vector2 at, Vector2 u, float r, Color tint, float time)
    {
        var side = new Vector2(-u.Y, u.X);
        for (int i = 0; i < 3; i++)
        {
            float phase = Mathf.PosMod(time * 0.3f + i / 3f, 1);
            var p = at + u * (r + 6 + 20 * phase);
            var colour = tint with { A = tint.A * 0.55f * Mathf.Sin(Mathf.Pi * phase) };
            Vector2[] chevron = [p - u * 3 + side * 3.5f, p, p - u * 3 - side * 3.5f];
            ci.DrawPolyline(chevron, Edge with { A = Edge.A * colour.A }, 3, true);
            ci.DrawPolyline(chevron, colour, 1.3f, true);
        }
    }

    // A garden trellis from a to b: two zigzags crossing in a lattice of diamonds over a slim rail, a post at each end.
    public static void Trellis(CanvasItem ci, Vector2 a, Vector2 b, Color tint, float s = 1)
    {
        var along = b - a;
        float length = along.Length();
        if (length < 1) return;
        var dir = along / length;
        var normal = new Vector2(-dir.Y, dir.X) * 4.5f * s;
        int diamonds = Mathf.Max(2, Mathf.RoundToInt(length / (16 * s)));
        var zig = new Vector2[diamonds + 1];
        var zag = new Vector2[diamonds + 1];
        for (int i = 0; i <= diamonds; i++)
        {
            var p = a + along * i / diamonds;
            var off = i % 2 == 0 ? normal : -normal;
            zig[i] = p + off;
            zag[i] = p - off;
        }
        var edge = Edge with { A = Edge.A * tint.A };
        foreach (var line in new[] { zig, zag, new[] { a, b } }) ci.DrawPolyline(line, edge, 3.2f * s, true);
        ci.DrawLine(a, b, tint with { A = tint.A * 0.6f }, 1.2f * s, true);
        ci.DrawPolyline(zig, tint, 1.2f * s, true);
        ci.DrawPolyline(zag, tint, 1.2f * s, true);
        foreach (var end in new[] { a, b })
        {
            ci.DrawCircle(end, 3.6f * s, edge);
            ci.DrawCircle(end, 2.6f * s, tint);
        }
    }

    // A gravity well: a dark eye ringed in the tint, with faint rings drawing in towards it (time moves them).
    public static void Well(CanvasItem ci, Vector2 at, Color tint, float time, float s = 1)
    {
        var edge = Edge with { A = Edge.A * tint.A };
        for (int i = 0; i < 3; i++)
        {
            float phase = Mathf.PosMod(time * 0.2f + i / 3f, 1);
            float r = Mathf.Lerp(ShapingTool.WellRadius * 2.2f, 7.5f, phase) * s;
            float a = tint.A * 0.6f * Mathf.Sin(Mathf.Pi * phase);
            ci.DrawArc(at, r, 0, Mathf.Tau, 48, Edge with { A = Edge.A * a }, 3 * s, true);
            ci.DrawArc(at, r, 0, Mathf.Tau, 48, tint with { A = a }, 1.1f * s, true);
        }
        ci.DrawCircle(at, 8.6f * s, edge);
        ci.DrawCircle(at, 7.5f * s, Fill with { A = 0.9f * tint.A });
        ci.DrawArc(at, 7.5f * s, 0, Mathf.Tau, 32, tint, 1.5f * s, true);
        ci.DrawCircle(at, 2 * s, tint.Lightened(0.4f) with { A = tint.A });
    }

    // A handle the pointer can take (a pot's turning knob, a trellis's ends): a small bright bead.
    public static void Knob(CanvasItem ci, Vector2 at, float r, Color tint)
    {
        ci.DrawCircle(at, r + 1.4f, Edge with { A = Edge.A * tint.A });
        ci.DrawCircle(at, r, Fill with { A = tint.A });
        ci.DrawArc(at, r, 0, Mathf.Tau, 20, tint, 1.4f, true);
    }

    // The shaping bar's icons, about 18 px across, centred on c.
    public static void DrawIcon(CanvasItem ci, Tool tool, Vector2 c, Color tint)
    {
        switch (tool)
        {
            case Tool.Pot: // a part (a dot) in its cup
                ci.DrawCircle(c + new Vector2(0, -1), 3.5f, tint with { A = tint.A * 0.6f });
                Pot(ci, c + new Vector2(0, -1), Vector2.Up, 4.5f, tint);
                break;
            case Tool.Trellis: Trellis(ci, c + new Vector2(-8, 5), c + new Vector2(8, -5), tint, 0.6f); break;
            case Tool.Well: Well(ci, c, tint, 0.1f, 0.45f); break;
        }
    }

    // A flat shape in the outline look: a dark edge, a dark fill, a crisp outline.
    static void Outlined(CanvasItem ci, Vector2[] points, Color tint, float s)
    {
        Vector2[] closed = [.. points, points[0]];
        ci.DrawPolyline(closed, Edge with { A = Edge.A * tint.A }, 3.4f * Mathf.Sqrt(s), true);
        ci.DrawColoredPolygon(points, Fill with { A = 0.85f * tint.A });
        ci.DrawPolyline(closed, tint, 1.4f * Mathf.Sqrt(s), true);
    }
}
