using Godot;

namespace SpaceGarden;

// Quiet lines under the plant in Active mode, so the garden can be read without a menu (DESIGN.md, Desktop widget):
// what a player tending it needs to decide what to grow, cut or move. Its power (held against its store, made against
// used, wilting), its genes and how fast they reach the core, the next unlock and when it comes at that rate, the caps
// used (stems, generators, core links, fruit), its flowers, and the wild buds on their way. Each line is tinted by its
// role's hue (DESIGN.md, Visual language). Above them, in a panel of its own, the selected part: what it is, its
// power, what it does, and its own numbers. While the last prune can be undone, an Undo pill sits on top, counting
// down. The lines and the panel show only while the pointer rests on the plant (TextAlpha, faded by the widget); the
// Undo pill stays while it can be used.
public partial class Readout : VBoxContainer
{
    public event Action? UndoPressed;

    const double RefreshSeconds = 0.5;

    static readonly Color PowerTint = new(1f, 0.96f, 0.62f);
    static readonly Color GeneTint = new(0.82f, 0.74f, 1f);
    static readonly Color RoomTint = new(0.78f, 0.95f, 0.78f);
    static readonly Color GrowthTint = new(1f, 0.8f, 0.6f);
    static readonly Color WarnTint = new(1f, 0.7f, 0.6f);

    readonly Label power, genes, unlock, room, flowers, hint, undoText;
    readonly Label nodeTitle, nodeText, nodeDetail;
    readonly Label[] lines;
    readonly PanelContainer undo, nodePanel;
    readonly Control nodeGap;
    double refreshTimer;
    bool shown;

    public Readout()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        AddThemeConstantOverride("separation", 0);

        undo = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, Visible = false, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        var style = Toolbar.PillStyle();
        style.ContentMarginLeft = style.ContentMarginRight = 12;
        style.ContentMarginTop = style.ContentMarginBottom = 4;
        undo.AddThemeStyleboxOverride("panel", style);
        undoText = new Label { LabelSettings = new LabelSettings { FontSize = 13 }, MouseFilter = MouseFilterEnum.Ignore };
        undo.AddChild(undoText);
        undo.TooltipText = "Put the last pruned branch back (Ctrl+Z)";
        undo.MouseEntered += () => undo.Modulate = new Color(1.25f, 1.25f, 1.25f);
        undo.MouseExited += () => undo.Modulate = Colors.White;
        undo.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;
            undo.AcceptEvent();
            UndoPressed?.Invoke();
        };
        AddChild(undo);
        AddChild(new Control { CustomMinimumSize = new Vector2(0, 6), MouseFilter = MouseFilterEnum.Ignore });

        // The selected part's panel: a dim card, so it reads as about one part rather than the whole garden.
        nodePanel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, Visible = false, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        var card = Toolbar.PillStyle();
        card.BgColor = card.BgColor with { A = 0.6f };
        card.SetCornerRadiusAll(10);
        card.ContentMarginLeft = card.ContentMarginRight = 12;
        card.ContentMarginTop = card.ContentMarginBottom = 5;
        nodePanel.AddThemeStyleboxOverride("panel", card);
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 0);
        nodePanel.AddChild(column);
        nodeTitle = Line(Colors.White, column, 14);
        nodeText = Line(new Color(1, 1, 1, 0.75f), column);
        nodeDetail = Line(new Color(1, 1, 1, 0.9f), column);
        AddChild(nodePanel);
        nodeGap = new Control { CustomMinimumSize = new Vector2(0, 6), MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        AddChild(nodeGap);

        power = Line(PowerTint);
        genes = Line(GeneTint);
        unlock = Line(GeneTint);
        room = Line(RoomTint);
        flowers = Line(GrowthTint);
        hint = Line(new Color(1, 1, 1, 0.7f));
        lines = [power, genes, unlock, room, flowers, hint];
        TextAlpha = 0;
    }

    Label Line(Color tint, Container? into = null, int size = 13)
    {
        var label = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
            LabelSettings = new LabelSettings { FontSize = size, FontColor = tint with { A = tint.A * 0.9f }, OutlineSize = 4, OutlineColor = new Color(0, 0, 0, 0.6f) },
        };
        (into ?? this).AddChild(label);
        return label;
    }

    // Whether the pointer is over the part that takes clicks (the Undo pill), in the readout's layer's units.
    public bool Catches(Vector2 point) => Visible && undo.Visible && undo.GetGlobalRect().Grow(4).HasPoint(point);

    // Whether the pointer is over the lines while they show (so they stay while being read).
    public bool Over(Vector2 point) => Visible && TextAlpha > 0 && GetGlobalRect().Grow(6).HasPoint(point);

    // How much the lines and the panel show, 0-1.
    public float TextAlpha
    {
        get => power.Modulate.A;
        set
        {
            foreach (var line in lines) line.Modulate = new Color(1, 1, 1, value);
            nodePanel.Modulate = new Color(1, 1, 1, value);
        }
    }

    // The lines are worked out only while they show (the genes' odds walk the plant), and at once when they start to.
    // growthSpeed turns the garden's rates into real minutes.
    public void Update(Garden garden, PlantNode? selected, double growthSpeed, double delta)
    {
        bool showing = TextAlpha > 0;
        if (showing && !shown) refreshTimer = 0;
        shown = showing;
        if ((refreshTimer -= delta) > 0) return;
        refreshTimer = RefreshSeconds;

        bool canUndo = garden.CanUndoPrune;
        undo.Visible = canUndo;
        if (canUndo) undoText.Text = $"Undo prune  {Math.Ceiling(garden.UndoSecondsLeft):0} s";
        if (!showing) return;

        var stats = GardenStats.Of(garden);
        power.Text = $"Power {stats.PowerHeld:#,0} of {stats.PowerStore:#,0} ({stats.PowerFill * 100:0}%)"
            + $" · makes {stats.Production:#,0}, uses {stats.Upkeep:#,0} an hour"
            + (stats.Wilting > 0 ? $" · {stats.Wilting} wilting" : "");
        power.LabelSettings.FontColor = (stats.Draining ? WarnTint : PowerTint) with { A = 0.9f };
        if (stats.Draining) power.Text += " · draining";

        genes.Text = $"Genes {stats.Genes:#,0} · +{stats.GenesPerHour / 60 * growthSpeed:#,0.#} a minute reach the core";
        unlock.Text = stats.NextUnlock is not { } next ? "Every unlock reached"
            : $"Next: {Gives(next)} at {next.Genes:#,0} ({stats.GenesToNext:#,0} to go)"
                + (stats.SecondsToNext is { } eta ? $", {Eta(eta / growthSpeed)}" : stats.Labs == 0 ? ": plant a lab" : "");

        var caps = stats.Capacity;
        room.Text = $"Stems {stats.Stems}{Of(caps.Stems)} · generators {stats.Generators}{Of(caps.Generators)}"
            + $" · core links {stats.CoreLinksUsed}/{caps.CoreLinks} · fruit {stats.Fruit}/{stats.FruitRoom}";

        flowers.Text = $"Groves {stats.Groves} · labs {stats.Labs} · " + (stats.Groves == 0 ? "nothing grows wild without a grove"
            : stats.BudsForming.Count == 0 ? "no wild buds forming"
            : "growing wild: " + string.Join(", ", stats.BudsForming.OrderBy(b => b.Key).Select(b => Count(b.Value, Name(b.Key).ToLowerInvariant()))));

        hint.Text = Hotkey.Registered ? $"{Hotkey.Name} fades the garden into the background" : "";
        ShowNode(garden, selected, growthSpeed);
    }

    // The selected part's panel, or none.
    void ShowNode(Garden garden, PlantNode? node, double growthSpeed)
    {
        nodePanel.Visible = nodeGap.Visible = node != null;
        if (node == null) return;
        var (name, description) = NodeSpecs.Info[node.Spec.Kind];
        nodeTitle.Text = $"{name} · {node.Power:0.##} / {node.Store:0.##} power" + (node.Wilting ? " · wilting" : "");
        nodeTitle.LabelSettings.FontColor = GardenView.Rgb(node.Spec.Rgb).Lightened(0.35f);
        nodeText.Text = description;

        var detail = new List<string>();
        float made = Garden.ProductionPerHour(node), upkeep = Garden.UpkeepPerHour(node);
        if (made > 0) detail.Add($"makes {made:#,0} an hour");
        if (upkeep > 0) detail.Add($"upkeep {upkeep:#,0} an hour");
        if (node.Spec.Kind == NodeKind.Lab)
            detail.Add($"{GardenStats.GeneReach(garden, node) * 100:0}% of its genes reach the core"
                + $" (+{GardenStats.GenesPerHourFrom(garden, node) / 60 * growthSpeed:#,0.#} a minute)");
        if (node.Spec.MaxFruits > 0) detail.Add($"fruit {node.Fruits.Count}/{node.Spec.MaxFruits}");
        if (node.Forming is { } kind)
        {
            float of = NodeSpecs.All[kind].IsFruit ? Tuning.FruitGrowth : Tuning.BudGrowth;
            detail.Add($"forming a {Name(kind).ToLowerInvariant()} {node.Growth / of * 100:0}%");
        }
        string text = string.Join(" · ", detail);
        nodeDetail.Text = text.Length > 0 ? char.ToUpperInvariant(text[0]) + text[1..] : "";
        nodeDetail.Visible = detail.Count > 0;
    }

    // Shows at once: the readout appearing, the Undo pill after a prune, a new selection.
    public void RefreshNow() => refreshTimer = 0;

    static string Name(NodeKind kind) => NodeSpecs.Info[kind].Name;

    static string Count(int n, string thing) => n == 1 ? $"1 {thing}" : $"{n} {thing}s";

    static string Of(int cap) => cap == int.MaxValue ? "" : $"/{cap}";

    // A rough time to wait, in real seconds: "in about 25 min", "in about 3 h 10 min", "in about 2 days".
    static string Eta(double seconds)
    {
        if (seconds < 60) return "in under a minute";
        double minutes = Math.Round(seconds / 60);
        if (minutes < 60) return $"in about {minutes:0} min";
        double hours = Math.Floor(minutes / 60);
        if (hours < 24) return $"in about {hours:0} h" + (minutes % 60 >= 5 ? $" {minutes % 60:0} min" : "");
        double days = Math.Round(hours / 24, 1);
        return $"in about {days:0.#} day{(days == 1 ? "" : "s")}";
    }

    static string Gives(UnlockStep step)
    {
        var parts = new List<string>();
        if (step.Stems > 0) parts.Add($"+{step.Stems} stem{(step.Stems == 1 ? "" : "s")}");
        if (step.Generators > 0) parts.Add($"+{step.Generators} generator{(step.Generators == 1 ? "" : "s")}");
        if (step.CoreLinks > 0) parts.Add($"+{step.CoreLinks} core branch{(step.CoreLinks == 1 ? "" : "es")}");
        return string.Join(", ", parts);
    }
}
