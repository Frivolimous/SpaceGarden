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
        ShapingKind.Sway => "Sway Controller",
        _ => "Gravity Well",
    };

    public static string Does(ShapingKind kind) => kind switch
    {
        ShapingKind.Pot => "Drop a part on it: the part is held there.",
        ShapingKind.Trellis => "Drop parts on it: they're held along its line.",
        ShapingKind.Sway => "Place it anywhere and turn its knob to adjust the garden's sway.",
        _ => "Pulls the whole garden towards it. Pot the core to keep the garden where it is.",
    };

    // A flower pot holding a part of radius r centred at `at`, opening the way u points: a rim straight across the
    // part's middle, a little wider than the bowl, and under it a round bowl just bigger than the part, so the part
    // sits in it up to its middle. The bowl is centred on the part, so the gap round it is even, sides and bottom;
    // the rim covers its top. s scales the pot's own sizes (its gap, rim and lip) for an icon.
    public static void Pot(CanvasItem ci, Vector2 at, Vector2 u, float r, Color tint, float s = 1)
    {
        const int steps = 16;
        var side = new Vector2(-u.Y, u.X);
        float bowl = r + ShapingTool.PotGap * s, rim = ShapingTool.PotRim * s, lip = bowl + ShapingTool.PotLip * s;
        var under = at - u * rim; // the rim's underside
        var points = new Vector2[steps + 1];
        float start = u.Angle() + Mathf.Pi / 2; // one side of the rim, round the bottom to the other
        for (int i = 0; i <= steps; i++) points[i] = at + Vector2.FromAngle(start + Mathf.Pi * i / steps) * bowl;
        Outlined(ci, points, tint, s);
        Outlined(ci, [at + side * lip, at - side * lip, under - side * lip, under + side * lip], tint, s);
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

    // A sway controller: a dark dial face, a faint track along its knob's arc (through the top), lit from its left end
    // up to the dial's setting, and in the middle a blade of grass swaying as much as the dial says (time moves it).
    public static void Sway(CanvasItem ci, Vector2 at, float dial, float sway, Color tint, float time, float s = 1)
    {
        var edge = Edge with { A = Edge.A * tint.A };
        float r = ShapingTool.DialRadius * s;
        ci.DrawCircle(at, r + 1.1f, edge);
        ci.DrawCircle(at, r, Fill with { A = 0.9f * tint.A });
        ci.DrawArc(at, r, 0, Mathf.Tau, 40, tint, 1.4f * Mathf.Sqrt(s), true);
        float start = -Mathf.Pi / 2 - ShapingTool.DialSweep / 2;
        float track = r - 3.2f * s;
        ci.DrawArc(at, track, start, start + ShapingTool.DialSweep, 40, tint with { A = tint.A * 0.25f }, 1.6f * s, true);
        if (dial > 0.01f)
            ci.DrawArc(at, track, start, start + ShapingTool.DialSweep * dial, 40, tint, 1.8f * s, true);
        // The blade: rooted below the middle, its tip swinging side to side.
        var root = at + new Vector2(0, 5.5f * s);
        float swing = Mathf.Sin(time * 1.6f) * 2.6f * s * Mathf.Min(sway, Tuning.SwayMax);
        var tip = at + new Vector2(swing, -5.5f * s);
        var mid = (root + tip) / 2 + new Vector2(swing * 0.15f, 0);
        Vector2[] blade = [root, mid, tip];
        ci.DrawPolyline(blade, edge, 3 * s, true);
        ci.DrawPolyline(blade, tint.Lightened(0.3f) with { A = tint.A }, 1.4f * s, true);
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
            case Tool.Pot: // a stem in its pot, at half size
                ci.DrawCircle(c + new Vector2(0, -2), 5, tint with { A = tint.A * 0.6f });
                Pot(ci, c + new Vector2(0, -2), Vector2.Up, 5, tint, 0.5f);
                break;
            case Tool.Trellis: Trellis(ci, c + new Vector2(-8, 5), c + new Vector2(8, -5), tint, 0.6f); break;
            case Tool.Well: Well(ci, c, tint, 0.1f, 0.45f); break;
            case Tool.Sway: Sway(ci, c, 0.65f, 1.5f, tint, 1, 0.62f); break;
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
