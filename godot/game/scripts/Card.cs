using Godot;

namespace SpaceGarden;

// The one card (DESIGN.md, Information display), after the original game's: a heading in the hue of what it's about,
// then short "Label: value" lines in whole numbers. It's about the selected part (tapping a part selects it) or the
// whole garden (Info, in the core's menu). The widget sets it down in a spot of its own as it opens and leaves it
// there, so the layout never jumps; its top corner stays put as its lines change. The numbers come from the rules
// (NodeStats, GardenStats), worked out again twice a second; growthSpeed turns the garden's rates into real ones.
public partial class Card : PanelContainer
{
    const double RefreshSeconds = 0.5;

    static readonly Color Ink = new(1, 1, 1, 0.9f);
    static readonly Color HighInk = new(1, 0.88f, 0.35f), LowInk = new(1, 0.38f, 0.33f), GeneInk = new(0.62f, 0.45f, 1f);
    static readonly Color RoomGreen = new(0.4f, 0.95f, 0.45f);
    static readonly Color QuirkInk = new(1, 1, 1, 0.6f);
    static readonly Color GardenTint = GardenView.Rgb(NodeSpecs.All[NodeKind.Core].Rgb);

    readonly Label heading;
    readonly VBoxContainer column;
    readonly List<HBoxContainer> rows = new();
    double refreshTimer;
    Tween? fade;

    // What it shows: a part, or the garden (null) while ShowsGarden.
    public PlantNode? Part { get; private set; }
    public bool ShowsGarden { get; private set; }
    public bool IsOpen => Part != null || ShowsGarden;

    public Card()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        var style = Toolbar.PillStyle();
        style.SetCornerRadiusAll(10);
        style.ContentMarginLeft = style.ContentMarginRight = 12;
        style.ContentMarginTop = 6;
        style.ContentMarginBottom = 8;
        AddThemeStyleboxOverride("panel", style);
        column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 0);
        AddChild(column);
        heading = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            LabelSettings = new LabelSettings { FontSize = 16 },
        };
        column.AddChild(heading);
    }

    public void ShowPart(PlantNode node) => Open(node, false);

    public void ShowGarden() => Open(null, true);

    void Open(PlantNode? part, bool garden)
    {
        bool wasOpen = IsOpen;
        Part = part;
        ShowsGarden = garden;
        refreshTimer = 0;
        if (wasOpen) return;
        if (!Visible) Modulate = new Color(1, 1, 1, 0);
        Visible = true;
        fade?.Kill();
        fade = CreateTween();
        fade.TweenProperty(this, "modulate:a", 1.0, 0.15);
    }

    public void Close()
    {
        if (!IsOpen) return;
        Part = null;
        ShowsGarden = false;
        fade?.Kill();
        fade = CreateTween();
        fade.TweenProperty(this, "modulate:a", 0.0, 0.2);
        fade.TweenCallback(Callable.From(() => Visible = false));
    }

    public void Update(Garden garden, double growthSpeed, double delta)
    {
        if (!IsOpen || (refreshTimer -= delta) > 0) return;
        refreshTimer = RefreshSeconds;
        int used = 0;
        if (Part is { } node) ShowNode(node, garden, growthSpeed, ref used);
        else ShowGardenLines(GardenStats.Of(garden), growthSpeed, ref used);
        for (int i = used; i < rows.Count; i++) rows[i].Visible = false;
    }

    void ShowNode(PlantNode node, Garden garden, double speed, ref int used)
    {
        var spec = node.Spec;
        var stats = NodeStats.Of(node);
        heading.Text = spec.Name;
        heading.LabelSettings.FontColor = GardenView.Rgb(spec.Rgb).Lightened(0.35f);
        // A wild flower's quirk, under its name: what it trades for its effect.
        if (spec.Wild is { } wild) Line(ref used, (wild.Quirk, QuirkInk));
        Line(ref used, ("Power: ", Ink), ($"{stats.Power:0}", PowerInk(stats.Power, stats.Store)), ($" / {stats.Store:0}", Ink));
        if (spec.PowerPerSecond > 0) // by kind, so the lines stay put when a rate falls to 0
            Line(ref used, $"Gen: {stats.Makes * speed:0}/s (sends {stats.Sends * speed:0}/s)");
        if (spec.PowerPerSecond < 0) Line(ref used, $"Drain: {stats.Upkeep * speed:0}/s");
        if (spec.Sends == BlobKind.Growth) Line(ref used, $"Growth: {stats.GrowthBlobsPerHour / 60 * speed:0}/min");
        if (spec.Sends == BlobKind.Genes) Line(ref used, $"Genes: {stats.GenesPerHour / 60 * speed:0}/min");
        if (stats.Links >= 2) Line(ref used, $"Connections: {stats.Links} / {stats.MaxLinks}");
        // A seedling's evolution, once chosen (its Power line is its progress: its store is the evolution's target).
        if (stats.Evolution is { } evolution)
            Line(ref used, ("Evolution: ", Ink), (evolution.Name, GardenView.Rgb(evolution.Spec.Rgb).Lightened(0.3f)));
        // A wild flower that ends itself: how long it has left (an Ember burns out, a Comet Bloom withers).
        if (spec.LifeSeconds > 0)
            Line(ref used, $"{(spec.Dims ? "Burns out" : "Withers")} in: {Eta(Math.Max(0, spec.LifeSeconds - node.Life) / speed)}");
        // On the core: an evolution whose seedling was pruned before it bloomed, which the next one planted takes up.
        if (node == garden.Core && garden.Waiting.FirstOrDefault() is { } waiting)
            Line(ref used, $"Next seedling: {waiting.Name}");
        if (stats.Forming is { } kind)
        {
            var forming = NodeSpecs.All[kind];
            Line(ref used, $"Forming: {forming.Name}{(forming.IsFruit ? "" : " bud")} {stats.FormingProgress * 100:0}%");
        }
    }

    void ShowGardenLines(GardenStats stats, double speed, ref int used)
    {
        heading.Text = "Garden";
        heading.LabelSettings.FontColor = GardenTint.Lightened(0.35f);
        Line(ref used, ("Power: ", Ink), ($"{stats.PowerHeld:0}", PowerInk(stats.PowerHeld, stats.PowerStore)),
            ($" / {stats.PowerStore:0}", Ink));
        // Now (they follow the fills). Gen's colour weighs what the garden makes against what it costs.
        Line(ref used, ("Gen: ", Ink), ($"{stats.Production * speed:0}/s", GenInk(stats.Production, stats.Upkeep)),
            ($", Drain: {stats.Upkeep * speed:0}/s", Ink));
        // The balance to spend, purple shading to white as it nears the cheapest next upgrade's cost; then that upgrade,
        // its cost and when the balance will cover it (or that it does: it's waiting in Research).
        if (stats.Cheapest is { } next)
        {
            Line(ref used, ("Genes: ", Ink),
                ($"{Math.Floor(stats.GeneBalance):0}", GeneInk.Lerp(Ink, (float)Math.Clamp(stats.GeneBalance / next.Cost, 0, 1))));
            string when = stats.SecondsToNext is { } eta ? eta <= 0 ? " (in Research)" : $" (ETA {Eta(eta / speed)})"
                : stats.Labs == 0 ? " (plant a lab)" : "";
            Line(ref used, $"Next: {next.Name}, {next.Cost:#,0}{when}");
        }
        else
        {
            Line(ref used, $"Genes: {Math.Floor(stats.GeneBalance):0}");
            Line(ref used, "Next: every upgrade bought");
        }
        Line(ref used, ("", Ink)); // a gap before the caps
        var caps = stats.Capacity;
        Line(ref used, ("Stems: ", Ink), ($"{stats.Stems}", RoomInk(stats.Stems, caps.Stems)), (Of(caps.Stems), Ink));
        Line(ref used, ("Generators: ", Ink), ($"{stats.Generators}", RoomInk(stats.Generators, caps.Generators)),
            (Of(caps.Generators), Ink));
        Line(ref used, ("Core links: ", Ink), ($"{stats.CoreLinksUsed}", RoomInk(stats.CoreLinksUsed, caps.CoreLinks)),
            ($" / {caps.CoreLinks}", Ink));
        // The wild flower limit, once there is one (step 5d).
        if (caps.WildFlowers > 0)
            Line(ref used, ("Wild flowers: ", Ink), ($"{stats.WildFlowers}", RoomInk(stats.WildFlowers, caps.WildFlowers)),
                ($" / {caps.WildFlowers}", Ink));
        Line(ref used, $"Groves: {stats.Groves}, Labs: {stats.Labs}");
    }

    void Line(ref int used, string text) => Line(ref used, (text, Ink));

    // The next row, in pieces of their own colours (a row with one empty piece is a small gap).
    void Line(ref int used, params (string Text, Color Colour)[] pieces)
    {
        if (used == rows.Count)
        {
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 0);
            column.AddChild(row);
            rows.Add(row);
        }
        var into = rows[used++];
        into.Visible = true;
        while (into.GetChildCount() < pieces.Length)
            into.AddChild(new Label { MouseFilter = MouseFilterEnum.Ignore, LabelSettings = new LabelSettings { FontSize = 13 } });
        for (int i = 0; i < into.GetChildCount(); i++)
        {
            var label = into.GetChild<Label>(i);
            label.Visible = i < pieces.Length;
            if (i >= pieces.Length) continue;
            label.Text = pieces[i].Text;
            label.LabelSettings.FontColor = pieces[i].Colour;
            label.CustomMinimumSize = pieces[i].Text.Length == 0 ? new Vector2(0, 6) : Vector2.Zero;
        }
    }

    // Shows at once: a new part, the garden, a prune.
    public void RefreshNow() => refreshTimer = 0;

    static string Of(int cap) => cap == int.MaxValue ? "" : $" / {cap}";

    // The values' colours (Jeremy, Oct 29). Power: white when full, towards yellow above it (all yellow at 125%),
    // towards red below it (all red at 50% and under).
    static Color PowerInk(float held, float store) => Level(store > 0 ? held / store : 1, 0.5f);

    // What the garden makes at full against what it costs at full: yellow as it makes more (all yellow at 125%), red
    // as it makes less (all red at 80%: an overload matters sooner than an emptying store).
    static Color GenInk(float made, float cost) => Level(cost > 0 ? made / cost : 2, 0.8f);

    static Color Level(float ratio, float allLow)
    {
        if (ratio >= 1) return Ink.Lerp(HighInk, Math.Clamp((ratio - 1) / 0.25f, 0, 1));
        return Ink.Lerp(LowInk, Math.Clamp((1 - ratio) / (1 - allLow), 0, 1));
    }

    // Room under a cap: green with all of it left, towards white as it fills (white when full).
    static Color RoomInk(int used, int cap) =>
        cap == int.MaxValue || cap <= 0 ? Ink : RoomGreen.Lerp(Ink, Math.Clamp(used / (float)cap, 0, 1));

    // A short time to wait, in real seconds: "under 1m", "25m", "4h 47m", "2d 3h".
    public static string Eta(double seconds)
    {
        if (seconds < 60) return "under 1m";
        double minutes = Math.Round(seconds / 60);
        if (minutes < 60) return $"{minutes:0}m";
        double hours = Math.Floor(minutes / 60);
        if (hours < 24) return $"{hours:0}h {minutes % 60:0}m";
        return $"{Math.Floor(hours / 24):0}d {hours % 24:0}h";
    }
}

// While the last prune can be undone, a pill under the plant counting down; a click puts the branch back.
public partial class UndoPill : PanelContainer
{
    public event Action? Pressed;

    readonly Label text;
    double refreshTimer;

    public UndoPill()
    {
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        var style = Toolbar.PillStyle();
        style.ContentMarginLeft = style.ContentMarginRight = 12;
        style.ContentMarginTop = style.ContentMarginBottom = 4;
        AddThemeStyleboxOverride("panel", style);
        text = new Label { LabelSettings = new LabelSettings { FontSize = 13 }, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(text);
        TooltipText = "Put the last pruned branch back (Ctrl+Z)";
        MouseEntered += () => Modulate = new Color(1.25f, 1.25f, 1.25f);
        MouseExited += () => Modulate = Colors.White;
        GuiInput += e =>
        {
            if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;
            AcceptEvent();
            Pressed?.Invoke();
        };
    }

    // Whether the pointer is on it (in its layer's units).
    public bool Catches(Vector2 point) => Visible && GetGlobalRect().Grow(4).HasPoint(point);

    public void Update(Garden garden, double delta)
    {
        if ((refreshTimer -= delta) > 0) return;
        refreshTimer = 0.25;
        Visible = garden.CanUndoPrune;
        if (Visible) text.Text = $"Undo prune  {Math.Ceiling(garden.UndoSecondsLeft):0} s";
    }

    public void RefreshNow() => refreshTimer = 0;
}
