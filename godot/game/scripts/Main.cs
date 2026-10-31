using Godot;

namespace SpaceGarden;

// Builds the overlay: loads the garden (catching up the time since it was last seen), draws it over the desktop,
// and saves it every minute and on quit. The plant is never rescaled automatically; only the Size setting in the
// menu changes it.
//
// Dev options (after "--"): --grow=N growth speed (default 1, the real pace), --upright for the shelved
// planted layout (every garden, saved ones too, is otherwise radial), --fresh to ignore the save, --seed=N, --away=HOURS to pretend that long
// has passed since the last save, --backdrop=RRGGBB to draw a colour behind the plant, --profile=NAME to use
// user://NAME.json instead of the real save, --fpslog to print the frame rate, --parts=N to grow a new garden to N
// parts at once (for stress tests; past the usual limit and caps too), --drain to start with every part but the core
// empty (to watch power flow back), --genes=N to bring the gene balance up to N genes (as if they had reached the
// core), --build to open in build mode, --shaping to place one of each shaping tool (or --shaping=N at physics
// tick N, with --screenshot), --pausetest=AT,FOR to act as if hidden for FOR seconds
// from AT seconds after start (to test the pause), --awaynote[=HOURS] to catch up that long (8 h) at once and show its
// note, --export=PATH (debug builds) to write the tracking as CSV and quit (see ExportTracking), --evolve=NAME[,NAME]
// to choose and bloom evolutions at once, --seedling to plant a seedling at once, --seedfill=N to set what it has
// taken in (planting one if needed; 200 brings its choice, with --tap=seedling to show it), --choose=N to take its
// Nth offer, plus Screenshot's
// options and the widget's (--inspect[=core|garden], --coremenu[=SLOT], --almanac, --research, --ambient, --corehover: see
// Widget.DevFlags). --screenshot runs never touch the real save.
//
// While nobody can see the garden (screen locked, display off, a full-screen app on its monitor: see Visibility) it
// stops: no drawing, no physics, and the widget wakes only a few times a second. Growth isn't lost: on coming back,
// the time it was hidden is caught up like time away, and the plant settles before it's shown.
public partial class Main : Node2D
{
    const double SaveEverySeconds = 60;
    const double VisibilityCheckSeconds = 0.5;

    public Garden Garden = null!;
    public double GrowthSpeed = 1;
    public GardenView View = null!;

    // Why the garden can't be seen just now, or null while it can.
    public string? HiddenReason { get; private set; }
    double hiddenSinceUnix, visibilityTimer;
    (double At, double For)? pauseTest;

    readonly Dictionary<string, string> args = new();
    Widget widget = null!;
    double saveTimer = SaveEverySeconds;
    int journalShown; // how much of the journal the notes have looked through, for shaping tools gained
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
        AwayNote? awayNote = null;
        if (save != null)
        {
            Garden = Garden.FromSave(save.Garden with { Layout = layout }, seed);
            journalShown = Garden.Tracking.Journal.Count;
            double away = Time.GetUnixTimeFromSystem() - save.LastSeenUnix;
            if (args.TryGetValue("away", out var hours)) away = double.Parse(hours) * 3600;
            awayNote = CatchUp(away * GrowthSpeed);
        }
        else Garden = Garden.New(layout, seed);

        widget = new Widget(this) { Name = "Widget" };
        AddChild(widget);
        widget.CoverScreen(args.TryGetValue("scale", out var sc) ? float.Parse(sc) : save?.PlantScale ?? 0.75f);
        if (save != null && Widget.IdleRates.Contains(save.IdleFps)) widget.IdleFps = save.IdleFps;
        if (save == null) PlantAtHome();
        else widget.KeepOnScreen();
        // Dev aid: a new garden with its first grove and lab already planted (nothing grows passively without a grove).
        if (save == null && args.ContainsKey("grove"))
        {
            var stem = Garden.Nodes[1];
            Garden.GrowBranch(stem, NodeKind.Grove);
            if (Garden.GrowBranch(stem, NodeKind.Stem) is { } second) Garden.GrowBranch(second, NodeKind.Lab);
        }
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
        if (args.ContainsKey("drain"))
        {
            foreach (var node in Garden.Nodes)
                if (node.Parent != null) node.Power = 0;
            Garden.SettleLooks();
        }
        if (args.TryGetValue("genes", out var genes)) Garden.Deliver(Math.Max(0, double.Parse(genes) - Garden.GeneBalance));
        if (args.TryGetValue("wildlimit", out var limit))
            Garden.Capacity = Garden.Capacity with { WildFlowers = Math.Max(0, int.Parse(limit)) };
        // Dev aid: the Evolution kinds unlocked, as their evolutions would (5c), all three or those named.
        if (args.TryGetValue("unlockwild", out var unlock))
            foreach (var spec in NodeSpecs.Wild.Where(s => s.Wild!.Unlock == WildUnlock.Evolution))
                if (unlock == "" || unlock.Split(',').Any(k => k.Equals(spec.Kind.ToString(), StringComparison.OrdinalIgnoreCase)))
                    Garden.UnlockWild(spec.Kind);
        if (args.TryGetValue("wild", out var wild)) GrowWild(wild);
        // Dev aids (step 5c): evolutions chosen and bloomed at once (by name, "SecondPath" or "second-path"), a seedling
        // planted, and the power it has taken in (at 200 it offers its evolutions; past its target it blooms).
        if (args.TryGetValue("evolve", out var evolve))
            foreach (var name in evolve.Split(','))
                if (Evolutions.Parse(name) is { } kind) Garden.EvolveNow(kind);
                else GD.PrintErr($"--evolve: no evolution called {name}");
        if (args.ContainsKey("seedling") || (args.ContainsKey("seedfill") && !Garden.Nodes.Any(n => n.IsSeedling)))
            Garden.PlantSeedling();
        if (args.TryGetValue("seedfill", out var fill))
            foreach (var seedling in Garden.Nodes.Where(n => n.IsSeedling).ToList())
                Garden.FillSeedling(seedling, double.Parse(fill));
        // --choose=N: each seedling waiting for its choice takes its Nth offer (from 0).
        if (args.TryGetValue("choose", out var choose))
            foreach (var seedling in Garden.Nodes.Where(n => n.Choosing).ToList())
                if (int.TryParse(choose == "" ? "0" : choose, out int i) && i < seedling.Offers!.Count)
                    Garden.Choose(seedling, seedling.Offers[i]);
        if (args.ContainsKey("evolve") || args.ContainsKey("seedling") || args.ContainsKey("seedfill"))
        {
            foreach (var node in Garden.Nodes) node.Power = node.Store;
            Garden.SettleLooks();
            for (int i = 0; i < 600; i++) Garden.Step(); // ten seconds' settling
            foreach (var node in Garden.Nodes) node.Age = Math.Max(node.Age, 10);
        }
        if (args.TryGetValue("shaping", out var shaping) && shaping == "") PlaceShaping(); // else Screenshot does
        if (args.ContainsKey("build")) widget.SetBuilding(true);
        // Dev aid: time away now (HOURS, 8 by default), on whatever garden was loaded or planted, to see its note.
        if (args.TryGetValue("awaynote", out var awayFor))
            awayNote = CatchUp((awayFor == "" ? 8 : double.Parse(awayFor)) * 3600) ?? awayNote;

        ShowGarden();
        if (awayNote != null) ShowAwayNote(awayNote);
        ShowToolsGained();

        if (args.TryGetValue("export", out var export) && OS.IsDebugBuild())
        {
            ExportTracking(export == "" ? null : export);
            if (!args.ContainsKey("screenshot")) GetTree().Quit();
        }
        Screenshot.CaptureIfRequested(this);
    }

    // Catches up time away (growth seconds) and works out what changed, or null if nothing worth a note did.
    AwayNote? CatchUp(double seconds)
    {
        var mark = AwayNote.MarkNow(Garden);
        Garden.CatchUp(seconds);
        var note = AwayNote.Since(Garden, mark);
        return note.Any ? note : null;
    }

    // Debug aid (the Developer menu, or --export=PATH): the history, totals and journal as three CSV files, PATH and
    // beside it PATH-totals.csv and PATH-journal.csv (by default stamped with the time in the user folder's exports).
    public void ExportTracking(string? path)
    {
        path ??= $"user://exports/tracking-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        string full = path.StartsWith("user://") || path.StartsWith("res://")
            ? ProjectSettings.GlobalizePath(path) : System.IO.Path.GetFullPath(path);
        if (!full.EndsWith(".csv")) full += ".csv";
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        string Beside(string suffix) => full[..^4] + suffix + ".csv";
        var tracking = Garden.Tracking;
        System.IO.File.WriteAllText(full, tracking.HistoryCsv());
        System.IO.File.WriteAllText(Beside("-totals"), tracking.TotalsCsv(Garden));
        System.IO.File.WriteAllText(Beside("-journal"), tracking.JournalCsv());
        GD.Print($"Tracking exported: {full}");
        Toast($"Exported the history to {full}");
    }

    // Sets a new garden's core down at the widget's home spot, with a first stem so it isn't just a seed.
    void PlantAtHome()
    {
        Garden.Translate(widget.HomeSpot() - Garden.Core.Position);
        Garden.Sprout();
    }

    // Dev aid, --wild=KIND[,KIND...] (or --wild=all, one of each, for a sheet of their looks): grows those wild flowers
    // at once, each on a stem of its own from the core (a new core link and stem past the caps), whatever their lock,
    // the wild flower limit raised to fit (see Garden.GrowWild); an inline kind gets a stem beyond it, to show it
    // carries on. Every part starts full, so they show at full size. Kinds by their NodeKind name (Ember, LumberGrove).
    void GrowWild(string which)
    {
        var kinds = which is "" or "all"
            ? NodeSpecs.Wild.Select(s => s.Kind).ToList()
            : which.Split(',').Select(k => Enum.Parse<NodeKind>(k, true)).ToList();
        foreach (var kind in kinds)
        {
            int stems = GardenStats.Of(Garden).Stems; // (a Tendril or Big Stem takes a stem slot too)
            Garden.Capacity = Garden.Capacity with
            {
                CoreLinks = Garden.Core.Links + 1,
                Stems = Math.Max(Garden.Capacity.Stems, stems + 2),
            };
            if (Garden.GrowBranch(Garden.Core, NodeKind.Stem) is not { } stem || Garden.GrowWild(kind, stem) is not { } flower)
                continue;
            if (flower.MaxLinks == 2) Garden.GrowBranch(flower, NodeKind.Stem);
        }
        // A sheet of them all: in the middle of the screen, where there's room round it.
        if (kinds.Count > 3 && Garden.Borders is var (min, max)) Garden.Translate((min + max) / 2 - Garden.Core.Position);
        foreach (var node in Garden.Nodes) node.Power = node.Store;
        Garden.SettleLooks();
        for (int i = 0; i < 900; i++) Garden.Step(); // fifteen seconds' settling
        foreach (var node in Garden.Nodes) node.Age = Math.Max(node.Age, 10);
    }

    // Dev aid, --shaping (now, or --shaping=N at physics tick N): one of each shaping tool, their caps raised to allow
    // it. A pot where the stem with the biggest branch beyond it stands, holding it and facing a little up from its
    // way out; a trellis across the end of another branch, holding the stems there; and a well off to one side.
    public void PlaceShaping()
    {
        Garden.Capacity = Garden.Capacity with
        {
            Pots = Math.Max(1, Garden.Capacity.Pots),
            Trellises = Math.Max(1, Garden.Capacity.Trellises),
            Wells = Math.Max(1, Garden.Capacity.Wells),
        };
        var core = Garden.Core.Position;
        int Size(PlantNode n) => 1 + n.Branches.Sum(Size);
        var stems = Garden.Nodes.Where(n => n.Spec.Kind == NodeKind.Stem && n.Depth >= 2).OrderByDescending(Size).ToList();
        if (stems.Count == 0) return;
        var potted = stems[0];
        var away = System.Numerics.Vector2.Normalize(potted.Position - core);
        if (Garden.Place(ShapingKind.Pot, potted.Position, MathF.Atan2(away.Y, away.X) - 0.7f) is { } pot) Garden.Tie(potted, pot);

        var others = Garden.Nodes.Where(n => !n.Spec.IsFruit && n.Branches.Count == 0 && n.Depth >= 2
            && !IsBeyond(n, potted)).Take(3).ToList();
        if (others.Count > 0 && Garden.Place(ShapingKind.Trellis, default) is { } trellis)
        {
            var middle = others.Aggregate(System.Numerics.Vector2.Zero, (sum, n) => sum + n.Position) / others.Count;
            var out_ = System.Numerics.Vector2.Normalize(middle - core);
            trellis.Position = middle + out_ * 30;
            trellis.Angle = MathF.Atan2(out_.X, -out_.Y); // across the way out
            trellis.Length = 220;
            foreach (var n in others) Garden.Tie(n, trellis);
        }
        Garden.Place(ShapingKind.Well, core + new System.Numerics.Vector2(away.Y, -away.X) * 260);

        static bool IsBeyond(PlantNode n, PlantNode of)
        {
            for (var p = n; p != null; p = p.Parent)
                if (p == of) return true;
            return false;
        }
    }

    void ShowGarden()
    {
        View?.QueueFree();
        View = new GardenView(Garden) { Name = "GardenView", Frozen = HiddenReason != null, Labels = args.ContainsKey("labels") };
        AddChild(View);
    }

    public void NewGarden()
    {
        var borders = Garden.Borders;
        Garden = Garden.New(layout, seed);
        Garden.Borders = borders;
        journalShown = Garden.Tracking.Journal.Count;
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

    // On the next frame rather than within half a second (the display mode just changed).
    public void CheckVisibilityNow() => visibilityTimer = 0;

    // Hides or shows the garden as Visibility (or --pausetest, or Minimised mode) says.
    void CheckVisibility(double delta)
    {
        if ((visibilityTimer -= delta) > 0) return;
        visibilityTimer = VisibilityCheckSeconds;
        var reason = widget.Mode == DisplayMode.Minimised ? "minimised" : Visibility.HiddenReason();
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
            var note = CatchUp((now - hiddenSinceUnix) * GrowthSpeed);
            if (note != null && note.Seconds >= AwayNoteSeconds) ShowAwayNote(note);
            if (args.ContainsKey("fpslog")) GD.Print($"SHOWN after {now - hiddenSinceUnix:0.0} s hidden ({HiddenReason}): {string.Join(" ", note?.Lines ?? [])}");
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
        ShowToolsGained();

        saveTimer -= delta;
        if (saveTimer <= 0)
        {
            saveTimer = SaveEverySeconds;
            Save();
        }
    }

    // The garden grew to a size that gains a shaping tool since the last note (live, or during a catch-up): one small
    // note says what there is to place now, from the journal's moments.
    void ShowToolsGained()
    {
        var journal = Garden.Tracking.Journal;
        if (journal.Count <= journalShown) return;
        var gained = journal.Skip(journalShown).Where(e => e.Kind == JournalKind.Tool).Select(e => e.Text).ToList();
        journalShown = journal.Count;
        if (gained.Count == 0) return;
        Toast($"{string.Join(". ", gained)}. Place {(gained.Count == 1 ? "it" : "them")} in build mode.", ShapingLook.Tint);
    }

    // What changed in a catch-up (DESIGN.md, Step 4: While you were away), in a small panel above the plant where the
    // notes show, for longer than a note (there's more to read). It says what grew, the genes, the shaping tools gained
    // (so their note isn't shown as well) and any dip in power, from the tracking and the journal.
    void ShowAwayNote(AwayNote note)
    {
        journalShown = Garden.Tracking.Journal.Count;
        var layer = new CanvasLayer { Scale = new Vector2(widget.Dpi, widget.Dpi) };
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var style = Toolbar.PillStyle();
        style.SetCornerRadiusAll(10);
        style.ContentMarginLeft = style.ContentMarginRight = 12;
        style.ContentMarginTop = 6;
        style.ContentMarginBottom = 8;
        panel.AddThemeStyleboxOverride("panel", style);
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 1);
        panel.AddChild(column);
        column.AddChild(new Label
        {
            Text = $"While you were away ({Card.Eta(note.Seconds / GrowthSpeed)})",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            LabelSettings = new LabelSettings { FontSize = 15, FontColor = GardenView.Rgb(NodeSpecs.All[NodeKind.Core].Rgb).Lightened(0.35f) },
        });
        for (int i = 0; i < note.Lines.Count; i++)
            column.AddChild(new Label
            {
                Text = note.Lines[i],
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(AwayNoteWidth, 0),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                // A wild flower discovered is picked out.
                LabelSettings = new LabelSettings { FontSize = 13, FontColor = note.Highlighted.Contains(i) ? Almanac.DiscoveryInk : new Color(1, 1, 1, 0.9f) },
            });
        layer.AddChild(panel);
        AddChild(layer);

        // Centred above the plant, kept on the screen.
        var size = panel.GetCombinedMinimumSize();
        var (min, _) = Garden.Bounds();
        var above = GetViewport().GetCanvasTransform() * new Vector2(Garden.Core.Position.X, min.Y) / widget.Dpi;
        var screen = GetViewport().GetVisibleRect().Size / widget.Dpi;
        panel.Position = new Vector2(above.X - size.X / 2, above.Y - size.Y - 16)
            .Clamp(new Vector2(8, 8), (screen - size - new Vector2(8, 8)).Max(new Vector2(8, 8)));
        panel.Size = size;
        var tween = CreateTween();
        tween.TweenProperty(panel, "modulate:a", 0.0, 1.5).SetDelay(AwayNoteShowSeconds);
        tween.TweenCallback(Callable.From(layer.QueueFree));
    }

    // A hidden spell shorter than this (growth seconds) gets no note: a glance away from the screen isn't time away.
    const double AwayNoteSeconds = 5 * 60;
    const double AwayNoteShowSeconds = 15;
    const float AwayNoteWidth = 400; // its lines wrap at this

    int toastsShowing;

    // A short note above the plant that fades after a few seconds. Notes showing at once stack upward, and a note
    // near a screen edge is kept on the screen.
    void Toast(string text, Color? colour = null)
    {
        const float width = 640;
        var (min, _) = Garden.Bounds();
        var above = GetViewport().GetCanvasTransform() * new Vector2(Garden.Core.Position.X, min.Y) / widget.Dpi
            - new Vector2(0, 26 * toastsShowing++);
        float screen = GetViewport().GetVisibleRect().Size.X / widget.Dpi;
        float left = Math.Clamp(above.X - width / 2, 8, Math.Max(8, screen - width - 8));
        var layer = new CanvasLayer { Scale = new Vector2(widget.Dpi, widget.Dpi) };
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(left, Math.Max(8, above.Y - 40)),
            Size = new Vector2(width, 24),
            LabelSettings = new LabelSettings { FontSize = 14, FontColor = colour ?? Colors.White, OutlineSize = 5, OutlineColor = new Color(0, 0, 0, 0.7f) },
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        layer.AddChild(label);
        AddChild(layer);
        var tween = CreateTween();
        tween.TweenProperty(label, "modulate:a", 0.0, 1.5).SetDelay(6);
        tween.TweenCallback(Callable.From(() =>
        {
            toastsShowing--;
            layer.QueueFree();
        }));
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
