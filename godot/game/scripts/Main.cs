using Godot;

namespace SpaceGarden;

// Builds the overlay: loads the garden (catching up the time since it was last seen), draws it over the desktop,
// and saves it every minute and on quit. The plant is never rescaled automatically; only the Size setting in the
// menu changes it.
//
// Dev options (after "--"): --grow=N growth speed (default 60x; 1 is the real pace), --upright for the shelved
// planted layout (every garden, saved ones too, is otherwise radial), --fresh to ignore the save, --seed=N, --away=HOURS to pretend that long
// has passed since the last save, --backdrop=RRGGBB to draw a colour behind the plant, --profile=NAME to use
// user://NAME.json instead of the real save, --fpslog to print the frame rate, --parts=N to grow a new garden to N
// parts at once (for stress tests; past the usual limit and caps too), --wilt to start with every part drained and
// wilting (to watch power flow back), --pausetest=AT,FOR to act as if hidden for FOR seconds
// from AT seconds after start (to test the pause), plus Screenshot's options. --screenshot runs never touch the
// real save.
//
// While nobody can see the garden (screen locked, display off, a full-screen app on its monitor: see Visibility) it
// stops: no drawing, no physics, and the widget wakes only a few times a second. Growth isn't lost: on coming back,
// the time it was hidden is caught up like time away, and the plant settles before it's shown.
public partial class Main : Node2D
{
    const double SaveEverySeconds = 60;
    const double VisibilityCheckSeconds = 0.5;

    public Garden Garden = null!;
    public double GrowthSpeed = 60;
    public GardenView View = null!;

    // Why the garden can't be seen just now, or null while it can.
    public string? HiddenReason { get; private set; }
    double hiddenSinceUnix, visibilityTimer;
    (double At, double For)? pauseTest;

    readonly Dictionary<string, string> args = new();
    Widget widget = null!;
    double saveTimer = SaveEverySeconds;
    Layout layout = Layout.Radial;
    int? seed;

    public override void _Ready()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            var parts = a.TrimStart('-').Split('=', 2);
            args[parts[0]] = parts.Length > 1 ? parts[1] : "";
        }
        if (args.TryGetValue("grow", out var g)) GrowthSpeed = double.Parse(g);
        if (args.ContainsKey("upright")) layout = Layout.Upright;
        if (args.TryGetValue("seed", out var s)) seed = int.Parse(s);
        if (args.TryGetValue("pausetest", out var pt))
        {
            var at = pt.Split(',');
            pauseTest = (double.Parse(at[0]), double.Parse(at[1]));
        }
        if (args.TryGetValue("profile", out var profile)) SaveFile.Path = $"user://{profile}.json";
        else if (args.ContainsKey("screenshot")) SaveFile.Path = null;

        if (args.TryGetValue("backdrop", out var hex))
        {
            var back = new CanvasLayer { Layer = -1 };
            var colour = new ColorRect { Color = Color.FromHtml(hex) };
            colour.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            back.AddChild(colour);
            AddChild(back);
        }

        var save = args.ContainsKey("fresh") ? null : SaveFile.Load();
        int caughtUp = 0;
        if (save != null)
        {
            Garden = Garden.FromSave(save.Garden with { Layout = layout }, seed);
            double away = Time.GetUnixTimeFromSystem() - save.LastSeenUnix;
            if (args.TryGetValue("away", out var hours)) away = double.Parse(hours) * 3600;
            caughtUp = Garden.CatchUp(away * GrowthSpeed);
        }
        else Garden = Garden.New(layout, seed);

        widget = new Widget(this) { Name = "Widget" };
        AddChild(widget);
        widget.CoverScreen(save?.PlantScale ?? 0.75f);
        if (save != null && Widget.IdleRates.Contains(save.IdleFps)) widget.IdleFps = save.IdleFps;
        if (save == null) PlantAtHome();
        else widget.KeepOnScreen();
        if (args.TryGetValue("parts", out var p))
        {
            int parts = int.Parse(p);
            Tuning.MaxNodes = Math.Max(Tuning.MaxNodes, parts);
            Garden.Capacity = Capacity.Unlimited;
            while (Garden.Nodes.Count < parts && Garden.Sprout() != null)
                for (int i = 0; i < 3; i++) Garden.Step();
            for (int i = 0; i < 1800; i++) Garden.Step(); // half a minute's settling
            foreach (var node in Garden.Nodes) node.Age = 10;
        }
        if (args.ContainsKey("wilt"))
            foreach (var node in Garden.Nodes)
            {
                node.Power = 0;
                node.Wilting = !node.Spec.IsFruit && node.Parent != null; // the core never wilts
                node.WiltLook = node.Parent != null ? 1 : 0;
            }

        ShowGarden();
        if (caughtUp > 0) Toast(caughtUp == 1 ? "While you were away, a new part grew." : $"While you were away, {caughtUp} new parts grew.");

        Screenshot.CaptureIfRequested(this);
    }

    // Sets a new garden's core down at the widget's home spot, with a first stem so it isn't just a seed.
    void PlantAtHome()
    {
        Garden.Translate(widget.HomeSpot() - Garden.Core.Position);
        Garden.Sprout();
    }

    void ShowGarden()
    {
        View?.QueueFree();
        View = new GardenView(Garden) { Name = "GardenView", Frozen = HiddenReason != null };
        AddChild(View);
    }

    public void NewGarden()
    {
        var borders = Garden.Borders;
        Garden = Garden.New(layout, seed);
        Garden.Borders = borders;
        PlantAtHome();
        ShowGarden();
        Save();
    }

    double fpsLogTimer;

    // --fpslog prints the frame rate four times a second, with whether it's idling, the fastest part's speed, the time
    // spent per frame (process, physics, and the renderer's CPU and GPU), draw calls and triangles.
    public override void _Process(double delta)
    {
        CheckVisibility(delta);
        if (!args.ContainsKey("fpslog") || (fpsLogTimer -= delta) > 0) return;
        fpsLogTimer = 0.25;
        var viewport = GetViewport().GetViewportRid();
        RenderingServer.ViewportSetMeasureRenderTime(viewport, true);
        GD.Print($"FPS {Engine.GetFramesPerSecond():0} idle {OS.LowProcessorUsageMode} hidden {HiddenReason ?? "no"} parts {Garden.Nodes.Count}"
            + $" speed {Garden.Speed:0.00} held {Garden.Held != null}"
            + $" | ms/frame process {Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.00}"
            + $" physics {Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000:0.00}"
            + $" render cpu {RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport) + RenderingServer.GetFrameSetupTimeCpu():0.00}"
            + $" gpu {RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport):0.00}"
            + $" | draw calls {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0}"
            + $" primitives {Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame):0}");
    }

    // Hides or shows the garden as Visibility (or --pausetest) says.
    void CheckVisibility(double delta)
    {
        if ((visibilityTimer -= delta) > 0) return;
        visibilityTimer = VisibilityCheckSeconds;
        var reason = Visibility.HiddenReason();
        double uptime = Time.GetTicksMsec() / 1000.0;
        if (pauseTest is var (at, length) && uptime >= at && uptime < at + length) reason ??= "pause test";

        double now = Time.GetUnixTimeFromSystem();
        if (reason != null && HiddenReason == null)
        {
            if (Garden.Held != null) Garden.LetGo();
            hiddenSinceUnix = now;
            Save();
        }
        else if (reason == null && HiddenReason != null)
        {
            int grew = Garden.CatchUp((now - hiddenSinceUnix) * GrowthSpeed);
            if (args.ContainsKey("fpslog")) GD.Print($"SHOWN after {now - hiddenSinceUnix:0.0} s hidden ({HiddenReason}); {grew} parts grew meanwhile");
        }
        if (reason != HiddenReason && args.ContainsKey("fpslog") && reason != null) GD.Print($"HIDDEN ({reason})");
        HiddenReason = reason;
        View.Frozen = reason != null;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (HiddenReason != null) return; // nothing moves or grows while hidden; growth is caught up on showing
        Garden.Step();
        Garden.Grow(delta * GrowthSpeed);

        saveTimer -= delta;
        if (saveTimer <= 0)
        {
            saveTimer = SaveEverySeconds;
            Save();
        }
    }

    // A short note above the plant that fades after a few seconds.
    void Toast(string text)
    {
        var (min, _) = Garden.Bounds();
        var above = GetViewport().GetCanvasTransform() * new Vector2(Garden.Core.Position.X, min.Y) / widget.Dpi;
        var layer = new CanvasLayer { Scale = new Vector2(widget.Dpi, widget.Dpi) };
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = above - new Vector2(200, 40),
            Size = new Vector2(400, 24),
            LabelSettings = new LabelSettings { FontSize = 14, OutlineSize = 5, OutlineColor = new Color(0, 0, 0, 0.7f) },
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        layer.AddChild(label);
        AddChild(layer);
        var tween = CreateTween();
        tween.TweenProperty(label, "modulate:a", 0.0, 1.5).SetDelay(6);
        tween.TweenCallback(Callable.From(layer.QueueFree));
    }

    void Save() => SaveFile.Write(new SaveData(SaveFile.Version, Garden.ToSave(), Time.GetUnixTimeFromSystem(), widget.PlantScale, widget.IdleFps));

    public void SaveAndQuit()
    {
        Save();
        GetTree().Quit();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) Save();
    }

    // Also covers quitting from code (the menu, a screenshot run).
    public override void _ExitTree() => Save();
}
