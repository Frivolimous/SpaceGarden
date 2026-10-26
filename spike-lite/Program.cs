using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Raylib_cs;
using SpaceGarden.Core;

namespace SpaceGarden.Lite;

// Spike: the Godot prototype's overlay without Godot. The same garden rules (godot/src/Core, unchanged) in a
// borderless, transparent, always-on-top, click-through raylib window over the screen's work area, stepped at
// 60 Hz and drawn at a fixed idle frame rate, sleeping between frames. No input, toolbar or menu: it only exists to
// measure what the shell costs at idle.
//
// Options: --parts=N grow a new garden to N parts at once (as the Godot version's --parts), --seed=N (default 1),
// --grow=N growth speed (default 60x), --fps=N idle frame rate (default 30), --fpslog print the frame rate once a
// second, --seconds=N quit after that long.
static class Program
{
    static readonly Dictionary<string, string> args = new();

    static int Main(string[] argv)
    {
        foreach (var a in argv)
        {
            var parts = a.TrimStart('-').Split('=', 2);
            args[parts[0]] = parts.Length > 1 ? parts[1] : "";
        }
        int seed = args.TryGetValue("seed", out var s) ? int.Parse(s) : 1;
        double growth = args.TryGetValue("grow", out var g) ? double.Parse(g) : 60;
        int fps = args.TryGetValue("fps", out var f) ? int.Parse(f) : 30;
        bool fpsLog = args.ContainsKey("fpslog");
        double quitAfter = args.TryGetValue("seconds", out var q) ? double.Parse(q) : double.PositiveInfinity;

        Win32.timeBeginPeriod(1); // 1 ms sleeps (Godot does the same)
        Win32.SetProcessDpiAwarenessContext(-4); // per-monitor v2: work area and window size in real pixels
        var work = Win32.WorkArea();
        if (work.Width == Win32.GetSystemMetrics(0) && work.Height == Win32.GetSystemMetrics(1)) work.Height -= 1; // not "fullscreen"
        float dpi = Math.Clamp(MathF.Round(Win32.GetDpiForSystem() / 96f * 4) / 4, 1, 3);
        float scale = dpi * 0.75f; // the Godot version's default Size

        Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        Raylib.SetConfigFlags(ConfigFlags.UndecoratedWindow | ConfigFlags.TransparentWindow | ConfigFlags.TopmostWindow);
        Raylib.InitWindow(work.Width, work.Height, "Space Garden (lite spike)");
        Raylib.SetWindowPosition(work.X, work.Y);
        Win32.MakeClickThrough(Win32.Handle());

        var garden = Garden.New(Layout.Radial, seed);
        garden.Borders = (Vector2.Zero, new Vector2(work.Width, work.Height) / scale);
        garden.Translate(new Vector2(work.Width - 320 * dpi, work.Height - 300 * dpi) / scale - garden.Core.Position);
        garden.Sprout();
        if (args.TryGetValue("parts", out var p))
        {
            int parts = int.Parse(p);
            Tuning.MaxNodes = Math.Max(Tuning.MaxNodes, parts);
            while (garden.Nodes.Count < parts && garden.Sprout() != null)
                for (int i = 0; i < 3; i++) garden.Step();
            for (int i = 0; i < 1800; i++) garden.Step(); // half a minute's settling
            foreach (var node in garden.Nodes) node.Age = 10;
        }

        var view = new View(garden, scale);
        const double tick = 1.0 / Tuning.TicksPerSecond;
        double frame = 1.0 / fps;
        var clock = Stopwatch.StartNew();
        double last = 0, accumulator = 0, nextFrame = 0, logAt = 1, prepare = 0, present = 0, physics = 0;
        int frames = 0, ticks = 0;

        while (!Raylib.WindowShouldClose() && clock.Elapsed.TotalSeconds < quitAfter)
        {
            double now = clock.Elapsed.TotalSeconds;
            accumulator += now - last;
            last = now;
            double ticked = clock.Elapsed.TotalSeconds;
            for (int i = 0; accumulator >= tick && i < 8; i++) // Godot's max_physics_steps_per_frame is 8 too
            {
                garden.Step();
                garden.Grow(tick * growth);
                accumulator -= tick;
                ticks++;
            }
            accumulator = Math.Min(accumulator, tick);
            physics += clock.Elapsed.TotalSeconds - ticked;

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(0, 0, 0, 0));
            double before = clock.Elapsed.TotalSeconds;
            view.Draw((float)(accumulator / tick));
            prepare += clock.Elapsed.TotalSeconds - before;
            before = clock.Elapsed.TotalSeconds;
            Raylib.EndDrawing();
            present += clock.Elapsed.TotalSeconds - before;
            frames++;

            if (fpsLog && now >= logAt)
            {
                Console.WriteLine($"FPS {frames / (now - logAt + 1):0.0} ticks {ticks} parts {garden.Nodes.Count} speed {garden.Speed:0.00}"
                    + $" | ms/frame physics {physics / frames * 1000:0.00} drawing prep {prepare / frames * 1000:0.00}"
                    + $" flush and present {present / frames * 1000:0.00}");
                logAt = now + 1;
                frames = ticks = 0;
                prepare = present = physics = 0;
            }

            // Sleep to the next frame; if it fell more than a frame behind, start counting again from now.
            nextFrame += frame;
            double wait = nextFrame - clock.Elapsed.TotalSeconds;
            if (wait > 0) Thread.Sleep(TimeSpan.FromSeconds(wait));
            else if (wait < -frame) nextFrame = clock.Elapsed.TotalSeconds;
        }

        Raylib.CloseWindow();
        Win32.timeEndPeriod(1);
        return 0;
    }
}

// Draws the garden as GardenView does, back to front: stems, halos, each kind of shape (a lighter rim, then the
// fill), then the motes. Like GardenView, each layer (and each kind of shape) is one batch: one draw call however
// big the plant.
sealed class View
{
    const float RimWidth = 1.2f;

    readonly Garden garden;
    readonly float scale;
    readonly Batch links, halos, moteHalos, moteDots;
    readonly Batch[] shapes;
    static readonly Vector4[] kindColours = Enum.GetValues<NodeKind>().Select(k => Rgb(NodeSpecs.All[k].Rgb)).ToArray();
    float blend;

    public View(Garden garden, float scale)
    {
        this.garden = garden;
        this.scale = scale;
        var halo = Textures.Halo();
        links = new Batch(Textures.Line());
        halos = new Batch(halo);
        moteHalos = new Batch(halo);
        shapes = Enum.GetValues<Shape>().Select(s => new Batch(Textures.Shape(s))).ToArray();
        moteDots = new Batch(Textures.Shape(Shape.Circle));
    }

    Vector2 At(PlantNode node) => Vector2.Lerp(node.PreviousPosition, node.Position, blend);

    float Size(PlantNode node) => node.Spec.Radius * GrowIn(node) * (1 + 0.15f * node.Glow);

    public void Draw(float blend)
    {
        this.blend = blend;

        links.Begin();
        foreach (var link in garden.Links)
        {
            var colour = Shade(ColourOf(link.Target.Spec), 0.45f + 0.55f * link.Flash);
            colour.W = MathF.Min(1, GrowIn(link.Target) * 1.5f);
            float width = link.Target.Spec.IsFruit ? 1.2f : MathF.Max(1.2f, 3.4f - link.Target.Depth * 0.18f);
            var from = At(link.Origin);
            var along = At(link.Target) - from;
            float length = along.Length();
            if (length < 0.01f) continue;
            var across = new Vector2(-along.Y, along.X) / length * (width / Textures.LineCore);
            links.AddAxes(from + along / 2, along, across, colour);
        }

        halos.Begin();
        foreach (var node in garden.Nodes)
        {
            var colour = ColourOf(node.Spec);
            colour.W = 0.3f + 0.4f * node.Glow;
            halos.AddRound(At(node), Size(node) * 3.6f, colour);
        }

        foreach (var batch in shapes) batch.Begin();
        foreach (var node in garden.Nodes)
        {
            float size = Size(node);
            if (size < 0.2f) continue;
            var fill = Shade(ColourOf(node.Spec), 0.75f + 0.45f * node.Glow);
            var batch = shapes[(int)node.Spec.Shape];
            var at = At(node);
            var (sin, cos) = MathF.SinCos(node.Rotation);
            batch.AddTurned(at, Textures.ShapeDiameter(size + RimWidth / 2), cos, sin, Lighten(fill, 0.35f));
            batch.AddTurned(at, Textures.ShapeDiameter(MathF.Max(0.1f, size - RimWidth / 2)), cos, sin, fill);
        }

        moteHalos.Begin();
        moteDots.Begin();
        foreach (var mote in garden.Motes)
        {
            var p = Vector2.Lerp(At(mote.From), At(mote.To), mote.T);
            var colour = Rgb(mote.Rgb);
            colour.W = 0.85f;
            moteHalos.AddRound(p, 26, colour);
            moteDots.AddRound(p, Textures.ShapeDiameter(2.6f), Vector4.One);
        }

        Raylib.BeginMode2D(new Camera2D { Zoom = scale });
        Rlgl.DisableBackfaceCulling(); // y points down, so the quads wind the other way
        // Premultiplied output, as the compositor expects of a transparent window (raylib's default blend squares
        // the alpha).
        Rlgl.SetBlendFactorsSeparate(0x0302, 0x0303, 1, 0x0303, 0x8006, 0x8006);
        Raylib.BeginBlendMode(BlendMode.CustomSeparate);
        links.Draw();
        halos.Draw();
        foreach (var batch in shapes) batch.Draw();
        moteHalos.Draw();
        moteDots.Draw();
        Raylib.EndBlendMode();
        Raylib.EndMode2D();
    }

    static float GrowIn(PlantNode node)
    {
        if (node.Age >= 1.5f) return 1;
        float t = Math.Clamp(node.Age / 1.5f, 0, 1);
        const float c1 = 1.70158f, c3 = c1 + 1;
        return 1 + c3 * MathF.Pow(t - 1, 3) + c1 * MathF.Pow(t - 1, 2);
    }

    static Vector4 Shade(Vector4 c, float lum) => new(MathF.Min(1, c.X * lum), MathF.Min(1, c.Y * lum), MathF.Min(1, c.Z * lum), c.W);

    static Vector4 Lighten(Vector4 c, float by) => new(c.X + (1 - c.X) * by, c.Y + (1 - c.Y) * by, c.Z + (1 - c.Z) * by, c.W);

    static Vector4 Rgb(uint rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1);

    static Vector4 ColourOf(NodeSpec spec) => kindColours[(int)spec.Kind];
}

// One draw call: a textured quad per entry, each with its own centre, axes and colour, as GardenView's Batch. The
// quads are written straight into a dynamic mesh's vertex memory, then uploaded and drawn in one go by Draw.
sealed unsafe class Batch
{
    readonly Material material;
    Mesh mesh;
    int capacity, count;

    public Batch(Texture2D texture)
    {
        material = Raylib.LoadMaterialDefault();
        material.Maps[(int)MaterialMapIndex.Albedo].Texture = texture;
        Allocate(64);
    }

    public void Begin() => count = 0;

    // A quad centred here, its unit square mapped onto the two axes.
    public void AddAxes(Vector2 centre, Vector2 x, Vector2 y, Vector4 colour)
    {
        if (count == capacity) Allocate(capacity * 2);
        var hx = x / 2;
        var hy = y / 2;
        float* v = mesh.Vertices + count * 12;
        Corner(v, centre - hx - hy);
        Corner(v + 3, centre + hx - hy);
        Corner(v + 6, centre + hx + hy);
        Corner(v + 9, centre - hx + hy);
        uint rgba = (uint)(colour.X * 255) | (uint)(colour.Y * 255) << 8 | (uint)(colour.Z * 255) << 16 | (uint)(colour.W * 255) << 24;
        uint* c = (uint*)(mesh.Colors + count * 16);
        c[0] = c[1] = c[2] = c[3] = rgba;
        count++;
    }

    static void Corner(float* v, Vector2 p)
    {
        v[0] = p.X;
        v[1] = p.Y;
        v[2] = 0;
    }

    public void AddTurned(Vector2 centre, float diameter, float cos, float sin, Vector4 colour) =>
        AddAxes(centre, new Vector2(cos, sin) * diameter, new Vector2(-sin, cos) * diameter, colour);

    public void AddRound(Vector2 centre, float diameter, Vector4 colour) =>
        AddAxes(centre, new Vector2(diameter, 0), new Vector2(0, diameter), colour);

    public void Draw()
    {
        if (count == 0) return;
        Raylib.UpdateMeshBuffer(mesh, 0, mesh.Vertices, count * 12 * sizeof(float), 0);
        Raylib.UpdateMeshBuffer(mesh, 3, mesh.Colors, count * 16, 0);
        var drawn = mesh;
        drawn.TriangleCount = count * 2;
        Raylib.DrawMesh(drawn, material, Matrix4x4.Identity);
    }

    // Room for this many quads: texture coordinates and indices are fixed, positions and colours rewritten each frame.
    void Allocate(int quads)
    {
        var old = mesh;
        mesh = new Mesh
        {
            VertexCount = quads * 4,
            TriangleCount = quads * 2,
            Vertices = (float*)Raylib.MemAlloc((uint)(quads * 12 * sizeof(float))),
            TexCoords = (float*)Raylib.MemAlloc((uint)(quads * 8 * sizeof(float))),
            Colors = (byte*)Raylib.MemAlloc((uint)(quads * 16)),
            Indices = (ushort*)Raylib.MemAlloc((uint)(quads * 6 * sizeof(ushort))),
        };
        for (int q = 0; q < quads; q++)
        {
            float* uv = mesh.TexCoords + q * 8;
            uv[0] = 0; uv[1] = 0; uv[2] = 1; uv[3] = 0; uv[4] = 1; uv[5] = 1; uv[6] = 0; uv[7] = 1;
            ushort* i = mesh.Indices + q * 6;
            int b = q * 4;
            i[0] = (ushort)b; i[1] = (ushort)(b + 1); i[2] = (ushort)(b + 2); i[3] = (ushort)b; i[4] = (ushort)(b + 2); i[5] = (ushort)(b + 3);
        }
        if (old.Vertices != null)
        {
            Buffer.MemoryCopy(old.Vertices, mesh.Vertices, quads * 12 * sizeof(float), count * 12 * sizeof(float));
            Buffer.MemoryCopy(old.Colors, mesh.Colors, quads * 16, count * 16);
            Raylib.UnloadMesh(old);
        }
        Raylib.UploadMesh(ref mesh, true);
        capacity = quads;
    }
}

// The same textures as the Godot view's: a soft halo, a soft-edged stem, and each shape as supersampled white
// coverage with mipmaps, tinted per quad.
static class Textures
{
    const int ShapeSize = 128;
    const int Supersample = 4;
    const float ShapeFill = 0.94f;
    public const float LineCore = 0.7f;

    public static float ShapeDiameter(float radius) => radius * 2 / ShapeFill;

    public static Texture2D Halo() => Make(64, 64, (x, y) =>
    {
        float d = Math.Min(1, Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32)) / 32);
        return d < 0.35f ? 1 - d / 0.35f * 0.65f : 0.35f * (1 - (d - 0.35f) / 0.65f);
    });

    public static Texture2D Line() => Make(4, 32, (_, y) =>
    {
        float fromMiddle = MathF.Abs((y + 0.5f) / 32 - 0.5f) * 2;
        return Math.Clamp((1 - fromMiddle) / (1 - LineCore), 0, 1);
    });

    public static Texture2D Shape(Shape shape)
    {
        int sides = shape switch { Core.Shape.Triangle => 3, Core.Shape.Square => 4, Core.Shape.Pentagon => 5, Core.Shape.Hexagon => 6, _ => 0 };
        var corners = Enumerable.Range(0, sides).Select(i => new Vector2(MathF.Cos(MathF.Tau * i / sides), MathF.Sin(MathF.Tau * i / sides))).ToArray();
        return Make(ShapeSize, ShapeSize, (x, y) =>
        {
            int inside = 0;
            for (int sy = 0; sy < Supersample; sy++)
                for (int sx = 0; sx < Supersample; sx++)
                {
                    var p = (new Vector2(x + (sx + 0.5f) / Supersample, y + (sy + 0.5f) / Supersample) / ShapeSize * 2 - Vector2.One) / ShapeFill;
                    if (Inside(corners, p)) inside++;
                }
            return inside / (float)(Supersample * Supersample);
        });
    }

    static bool Inside(Vector2[] corners, Vector2 p)
    {
        if (corners.Length == 0) return p.LengthSquared() <= 1;
        for (int i = 0; i < corners.Length; i++)
        {
            var a = corners[i];
            var e = corners[(i + 1) % corners.Length] - a;
            var d = p - a;
            if (e.X * d.Y - e.Y * d.X < 0) return false;
        }
        return true;
    }

    // White with this alpha per pixel, trilinear-filtered with mipmaps.
    static unsafe Texture2D Make(int width, int height, Func<int, int, float> alpha)
    {
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                pixels[i] = pixels[i + 1] = pixels[i + 2] = 255;
                pixels[i + 3] = (byte)MathF.Round(Math.Clamp(alpha(x, y), 0, 1) * 255);
            }
        Texture2D texture;
        fixed (byte* data = pixels)
            texture = Raylib.LoadTextureFromImage(new Image { Data = data, Width = width, Height = height, Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8 });
        Raylib.GenTextureMipmaps(ref texture);
        Raylib.SetTextureFilter(texture, TextureFilter.Trilinear);
        return texture;
    }
}

static class Win32
{
    [StructLayout(LayoutKind.Sequential)]
    struct Rect { public int Left, Top, Right, Bottom; }

    public record struct Area(int X, int Y, int Width, int Height);

    [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(nint context);
    [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] static extern bool SystemParametersInfoW(uint action, uint param, ref Rect rect, uint winIni);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern nint GetWindowLongPtr(nint hWnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern nint SetWindowLongPtr(nint hWnd, int index, nint value);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(nint hWnd, uint colourKey, byte alpha, uint flags);

    public static Area WorkArea()
    {
        var r = new Rect();
        SystemParametersInfoW(0x30, 0, ref r, 0); // SPI_GETWORKAREA
        return new Area(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    public static unsafe nint Handle() => (nint)Raylib.GetWindowHandle();

    // As the Godot version's ClickThrough with the pointer off the plant: layered (fully opaque) and transparent to
    // clicks, so the measurement window doesn't block the desktop.
    public static void MakeClickThrough(nint hWnd)
    {
        long style = GetWindowLongPtr(hWnd, -20);
        SetWindowLongPtr(hWnd, -20, (nint)(style | 0x80000 | 0x20));
        SetLayeredWindowAttributes(hWnd, 0, 255, 0x2);
    }
}
