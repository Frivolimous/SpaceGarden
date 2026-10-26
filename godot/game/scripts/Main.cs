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
// parts at once (for stress tests; past the usual limit too), plus Screenshot's options. --screenshot runs never
// touch the real save.
public partial class Main : Node2D
{
    const double SaveEverySeconds = 60;

    public Garden Garden = null!;
    public double GrowthSpeed = 60;
    public GardenView View = null!;

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
        if (save == null) PlantAtHome();
        else widget.KeepOnScreen();
        if (args.TryGetValue("parts", out var p))
        {
            int parts = int.Parse(p);
            Tuning.MaxNodes = Math.Max(Tuning.MaxNodes, parts);
            while (Garden.Nodes.Count < parts && Garden.Sprout() != null)
                for (int i = 0; i < 3; i++) Garden.Step();
            for (int i = 0; i < 1800; i++) Garden.Step(); // half a minute's settling
            foreach (var node in Garden.Nodes) node.Age = 10;
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
        View = new GardenView(Garden) { Name = "GardenView" };
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

    // --fpslog prints the frame rate four times a second, with the fastest part's speed and the frame cap.
    public override void _Process(double delta)
    {
        if (!args.ContainsKey("fpslog") || (fpsLogTimer -= delta) > 0) return;
        fpsLogTimer = 0.25;
        GD.Print($"FPS {Engine.GetFramesPerSecond():0} cap {Engine.MaxFps} speed {Garden.Speed:0.00} held {Garden.Held != null}"
            + $" | ms/frame process {Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.00}"
            + $" physics {Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000:0.00}"
            + $" | draw calls {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0}"
            + $" primitives {Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame):0}");
    }

    public override void _PhysicsProcess(double delta)
    {
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

    void Save() => SaveFile.Write(new SaveData(Garden.ToSave(), Time.GetUnixTimeFromSystem(), widget.PlantScale));

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
