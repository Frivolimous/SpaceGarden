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
    static readonly Color WarnInk = new(1f, 0.5f, 0.45f);
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
        if (Part is { } node) ShowNode(node, growthSpeed, ref used);
        else ShowGardenLines(GardenStats.Of(garden), growthSpeed, ref used);
        for (int i = used; i < rows.Count; i++) rows[i].Visible = false;
    }

    void ShowNode(PlantNode node, double speed, ref int used)
    {
        var spec = node.Spec;
        var stats = NodeStats.Of(node);
        heading.Text = spec.Name;
        heading.LabelSettings.FontColor = GardenView.Rgb(spec.Rgb).Lightened(0.35f);
        Line(ref used, $"Power: {stats.Power:0} / {stats.Store:0}");
        if (stats.Makes > 0) Line(ref used, $"Makes: {stats.Makes * speed:0}/s (sends {stats.Sends * speed:0}/s)");
        if (stats.Upkeep > 0) Line(ref used, $"Upkeep: {stats.Upkeep * speed:0}/s");
        if (spec.Kind == NodeKind.Grove) Line(ref used, $"Growth: {stats.GrowthBlobsPerHour / 60 * speed:0}/min");
        if (spec.Kind == NodeKind.Lab) Line(ref used, $"Genes: {stats.GenesPerHour / 60 * speed:0}/min");
        if (stats.Links >= 2) Line(ref used, $"Connections: {stats.Links} / {stats.MaxLinks}");
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
        Line(ref used, $"Power: {stats.PowerHeld:0} / {stats.PowerStore:0}");
        // At full, as the overload is judged: steady, where the live rates rock with the fills.
        Line(ref used, ($"Gen: {stats.FullProduction * speed:0}/s, ", Ink),
            ($"Drain: {stats.FullUpkeep * speed:0}/s", stats.Overloaded ? WarnInk : Ink));
        if (stats.NextUnlock is { } next)
        {
            Line(ref used, $"Genes: {Math.Floor(stats.Genes):0} / {next.Genes:0}");
            string when = stats.SecondsToNext is { } eta ? $" (ETA {Eta(eta / speed)})" : stats.Labs == 0 ? " (plant a lab)" : "";
            Line(ref used, $"Next: {Gives(next)}{when}");
        }
        else
        {
            Line(ref used, $"Genes: {Math.Floor(stats.Genes):0}");
            Line(ref used, "Next: every unlock reached");
        }
        Line(ref used, ("", Ink)); // a gap before the caps
        var caps = stats.Capacity;
        Line(ref used, $"Stems: {stats.Stems}{Of(caps.Stems)}");
        Line(ref used, $"Generators: {stats.Generators}{Of(caps.Generators)}");
        Line(ref used, $"Core links: {stats.CoreLinksUsed} / {caps.CoreLinks}");
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

    static string Gives(UnlockStep step)
    {
        var parts = new List<string>();
        if (step.Stems > 0) parts.Add($"+{step.Stems} stem{(step.Stems == 1 ? "" : "s")}");
        if (step.Generators > 0) parts.Add($"+{step.Generators} generator{(step.Generators == 1 ? "" : "s")}");
        if (step.CoreLinks > 0) parts.Add($"+{step.CoreLinks} core link{(step.CoreLinks == 1 ? "" : "s")}");
        if (step.Pots > 0) parts.Add($"+{step.Pots} pot{(step.Pots == 1 ? "" : "s")}");
        if (step.Trellises > 0) parts.Add($"+{step.Trellises} trellis{(step.Trellises == 1 ? "" : "es")}");
        if (step.Wells > 0) parts.Add($"+{step.Wells} well{(step.Wells == 1 ? "" : "s")}");
        return string.Join(", ", parts);
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
