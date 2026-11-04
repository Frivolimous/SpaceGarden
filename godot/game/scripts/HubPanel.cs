using Godot;

namespace SpaceGarden;

// The Hub's panel (DESIGN.md, The Hub): a window of its own (see FloatingWindow), opened by the upgrades button on the
// Hub's toolbar and closed by it or its ✕ (or the Hub pruned). Its header shows the points to spend; under it, the pool's progress to the next point.
// Then a row for every kind's path (Hubs.Paths), the basic parts, the wild flowers and the Hub's own, in two columns: the
// kind's shape and name in its hue, what its next level gives ("Next: +20% genes", or its milestone), its levels as pips (a milestone's ringed), and a
// button to buy the next, its cost in points. A kind the garden hasn't grown yet shows "???". Buying is the rules'
// (Garden.BuyHubLevel): the widget does it on BuyPressed.
public partial class HubPanel : FloatingWindow
{
    public event Action<NodeKind>? BuyPressed;
    // Respec (Jeremy, Nov 3): every path back to 0, the points refunded but a level's worth (Garden.RespecHub).
    public event Action? RespecPressed;
    readonly ConfirmButton respec;

    const double RefreshSeconds = 0.25;
    const float RowWidth = 320;
    const int LeftRows = 10; // the basic parts and the first wild flowers; the rest and the Hub's on the right

    static readonly Color Hue = GardenView.Rgb(NodeSpecs.All[NodeKind.Hub].Rgb);
    static readonly Color HueInk = Hue.Lightened(0.3f);
    static readonly Color Back = new(0.06f, 0.07f, 0.09f);

    sealed record PathRow(HubPath Path, StyleBoxFlat Style, Color Hue, PathGlyph Glyph, Label Name, Label Detail, LevelPips Pips, Button Buy, Control Row,
        string Milestones);

    // When the Hub's next point comes, at the rate genes find it now (GardenStats.HubGenesPerHour): " (in 25m)", or
    // nothing while no genes are finding it.
    public static string NextPointIn(Garden garden, double growthSpeed)
    {
        double perSecond = GardenStats.HubGenesPerHour(garden) / 3600 * growthSpeed;
        return perSecond > 0 ? $" (in {Card.Eta((garden.NextPointCost - garden.HubPool) / perSecond)})" : "";
    }

    readonly List<PathRow> rows = new();
    readonly Label poolText;
    readonly PoolBar poolBar;
    double refreshTimer;

    public HubPanel() : base("Hub", "Close the Hub")
    {
        Note.LabelSettings.FontColor = HueInk;
        Note.LabelSettings.FontSize = 14;
        Note.HorizontalAlignment = HorizontalAlignment.Right;

        // The pool: how far it is to the next point.
        var pool = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        pool.AddThemeConstantOverride("separation", 8);
        poolBar = new PoolBar { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        pool.AddChild(poolBar);
        poolText = new Label { MouseFilter = MouseFilterEnum.Ignore, LabelSettings = new LabelSettings { FontSize = 11, FontColor = Dim } };
        pool.AddChild(poolText);
        respec = new ConfirmButton("Reset levels", "Confirm reset",
            $"Every path back to level 0, its points refunded but {Tuning.HubRespecCost} (a level's worth)");
        respec.Outlined();
        respec.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        respec.Confirmed += () => RespecPressed?.Invoke();
        pool.AddChild(respec);
        Column.AddChild(pool);
        Column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 2), MouseFilter = MouseFilterEnum.Ignore });

        var columns = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        columns.AddThemeConstantOverride("separation", 12);
        Column.AddChild(columns);
        var left = PathColumn(columns);
        var right = PathColumn(columns);
        left.AddChild(Heading("Parts"));
        int i = 0;
        foreach (var path in Hubs.Paths)
        {
            var into = i < LeftRows ? left : right;
            bool firstWild = NodeSpecs.All[path.Kind].IsWild && (i == 0 || !NodeSpecs.All[Hubs.Paths[i - 1].Kind].IsWild);
            if (firstWild) into.AddChild(Heading("Wildflowers"));
            else if (path.Kind == NodeKind.Hub) into.AddChild(Heading("Hub"));
            else if (i == LeftRows) into.AddChild(Heading("")); // level with the left column's first row
            into.AddChild(Row(path));
            i++;
        }
        Column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6), MouseFilter = MouseFilterEnum.Ignore });
    }

    static VBoxContainer PathColumn(HBoxContainer into)
    {
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 4);
        into.AddChild(column);
        return column;
    }

    static Label Heading(string text) => new()
    {
        Text = text,
        MouseFilter = MouseFilterEnum.Ignore,
        CustomMinimumSize = new Vector2(0, 18),
        VerticalAlignment = VerticalAlignment.Bottom,
        LabelSettings = new LabelSettings { FontSize = 10, FontColor = Dim },
    };

    // A kind's path: its glyph, name and what its levels give, its pips, and the button for the next level.
    Control Row(HubPath path)
    {
        var spec = NodeSpecs.All[path.Kind];
        var hue = GardenView.Rgb(spec.Rgb);
        var style = new StyleBoxFlat
        {
            BgColor = Back.Lerp(hue, 0.05f),
            BorderColor = hue with { A = 0.16f },
            ContentMarginLeft = 6, ContentMarginRight = 6, ContentMarginTop = 3, ContentMarginBottom = 3,
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(8);
        var row = new PanelContainer { MouseFilter = MouseFilterEnum.Pass, CustomMinimumSize = new Vector2(RowWidth, 0) };
        row.AddThemeStyleboxOverride("panel", style);
        var line = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 6);
        row.AddChild(line);

        var glyph = new PathGlyph(spec.Shape, hue);
        line.AddChild(glyph);
        var words = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        words.AddThemeConstantOverride("separation", -2);
        var name = new Label { MouseFilter = MouseFilterEnum.Ignore, LabelSettings = new LabelSettings { FontSize = 13 } };
        var detail = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            ClipText = true,
            LabelSettings = new LabelSettings { FontSize = 10, FontColor = Dim },
        };
        words.AddChild(name);
        words.AddChild(detail);
        line.AddChild(words);
        var pips = new LevelPips(path, hue) { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        line.AddChild(pips);

        var buy = new Button
        {
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(44, 22),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        StyleBoxFlat Fill(Color colour)
        {
            var s = new StyleBoxFlat { BgColor = colour, ContentMarginLeft = 6, ContentMarginRight = 6 };
            s.SetCornerRadiusAll(11);
            return s;
        }
        buy.AddThemeStyleboxOverride("normal", Fill(Hue with { A = 0.85f }));
        buy.AddThemeStyleboxOverride("hover", Fill(Hue.Lightened(0.25f)));
        buy.AddThemeStyleboxOverride("pressed", Fill(Hue.Lightened(0.45f)));
        var short_ = Fill(Colors.Transparent);
        short_.BorderColor = Hue with { A = 0.25f };
        short_.SetBorderWidthAll(1);
        buy.AddThemeStyleboxOverride("disabled", short_);
        buy.AddThemeFontSizeOverride("font_size", 11);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color" })
            buy.AddThemeColorOverride(state, Back);
        buy.AddThemeColorOverride("font_disabled_color", HueInk with { A = 0.6f });
        buy.Pressed += () => BuyPressed?.Invoke(path.Kind);
        line.AddChild(buy);

        // Its milestones, in the row's tooltip.
        row.TooltipText = $"Each level: {path.PerLevel}.\n"
            + string.Join("\n", path.Milestones.Select(m => $"Level {m.Level} ({path.Cost(m.Level)} points): {m.Text}."));
        rows.Add(new PathRow(path, style, hue, glyph, name, detail, pips, buy, row, row.TooltipText));
        return row;
    }

    protected override void Opened() => refreshTimer = 0;

    // Shows at once: a level just bought.
    public void RefreshNow() => refreshTimer = 0;

    // Every frame: follows the pointer while its header is held, keeps it on the screen, and refreshes the rows a few
    // times a second.
    public void Update(Garden garden, double growthSpeed, Vector2 pointer, Vector2 screen, double delta)
    {
        Place(pointer, screen, GetCombinedMinimumSize());
        if (!IsOpen || !Visible || (refreshTimer -= delta) > 0) return;
        refreshTimer = RefreshSeconds;
        Note.Text = garden.HubPoints == 1 ? "1 point" : $"{garden.HubPoints} points";
        respec.Visible = garden.HubLevelsBought > 0;
        poolBar.Progress = (float)Math.Clamp(garden.HubPool / garden.NextPointCost, 0, 1);
        poolText.Text = $"{Math.Floor(garden.HubPool):#,0} / {garden.NextPointCost:#,0} genes to the next point"
            + NextPointIn(garden, growthSpeed);
        foreach (var row in rows)
        {
            var kind = row.Path.Kind;
            var spec = NodeSpecs.All[kind];
            bool found = garden.Tracking.Discovered.ContainsKey(kind);
            int level = garden.HubLevel(kind);
            row.Glyph.Known = found;
            // (An unknown kind gives nothing away, not even its hue.)
            var tint = found ? row.Hue : new Color(1, 1, 1);
            row.Style.BgColor = Back.Lerp(tint, found ? 0.05f : 0.02f);
            row.Style.BorderColor = tint with { A = found ? 0.16f : 0.08f };
            row.Name.Text = found ? spec.Name : "???";
            row.Row.TooltipText = found ? row.Milestones : "Not found in the garden yet";
            row.Name.LabelSettings.FontColor = found ? GardenView.Rgb(spec.Rgb).Lightened(0.3f) : Dim;
            row.Pips.Level = level;
            row.Pips.Visible = found;
            row.Buy.Visible = found;
            if (!found)
            {
                row.Detail.Text = "Not found in the garden yet";
                continue;
            }
            // What the next level gives: its milestone, or each level's share; complete, the last milestone.
            int max = row.Path.MaxLevel;
            row.Detail.Text = level >= max ? $"Complete: {row.Path.MilestoneAt(max)?.Text ?? row.Path.PerLevel}"
                : row.Path.MilestoneAt(level + 1) is { } next ? $"Next: {next.Text}" : $"Next: {row.Path.PerLevel}";
            if (level >= max)
            {
                row.Buy.Text = "Max";
                row.Buy.Disabled = true;
                continue;
            }
            int cost = row.Path.Cost(level + 1);
            row.Buy.Text = cost == 1 ? "1 pt" : $"{cost} pts";
            row.Buy.Disabled = !garden.CanBuyHubLevel(kind);
        }
    }
}

// A kind's shape in its hue, or a dim "?" in a ring while the garden hasn't grown one.
public partial class PathGlyph : Control
{
    readonly Shape shape;
    readonly Color hue;
    bool known = true;

    public bool Known
    {
        get => known;
        set
        {
            if (value == known) return;
            known = value;
            QueueRedraw();
        }
    }

    public PathGlyph(Shape shape, Color hue)
    {
        this.shape = shape;
        this.hue = hue;
        CustomMinimumSize = new Vector2(24, 24);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        var c = Size / 2;
        if (!known)
        {
            var ink = new Color(1, 1, 1, 0.35f);
            DrawArc(c, 8.5f, 0, Mathf.Tau, 28, ink, 1.2f, true);
            DrawString(ThemeDB.FallbackFont, c + new Vector2(-3.5f, 4.5f), "?", HorizontalAlignment.Left, -1, 12, ink);
            return;
        }
        ToolButton.DrawShape(this, c, shape, 9, new Color(0, 0, 0, 0.5f));
        ToolButton.DrawShape(this, c, shape, 8, hue);
    }
}

// A path's levels as a row of pips, filled up to its level; a milestone's pip is ringed.
public partial class LevelPips : Control
{
    const float Gap = 7;
    readonly HubPath path;
    readonly Color hue;
    int level = -1;

    public int Level
    {
        get => level;
        set
        {
            if (value == level) return;
            level = value;
            QueueRedraw();
        }
    }

    public LevelPips(HubPath path, Color hue)
    {
        this.path = path;
        this.hue = hue;
        CustomMinimumSize = new Vector2(Gap * Hubs.Paths.Max(p => p.MaxLevel) + 2, 12); // (every row's alike)
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        for (int i = 1; i <= path.MaxLevel; i++)
        {
            var c = new Vector2(Gap * (i - 0.5f) + 1, Size.Y / 2);
            bool filled = i <= level;
            if (path.MilestoneAt(i) != null)
                DrawArc(c, 3.4f, 0, Mathf.Tau, 16, hue with { A = filled ? 0.9f : 0.45f }, 1, true);
            DrawCircle(c, 1.9f, filled ? hue : new Color(1, 1, 1, 0.16f));
        }
    }
}

// The pool's progress to the next point: a thin bar in the Hub's green.
public partial class PoolBar : Control
{
    static readonly Color Hue = GardenView.Rgb(NodeSpecs.All[NodeKind.Hub].Rgb);
    float progress;

    public float Progress
    {
        get => progress;
        set
        {
            if (Mathf.IsEqualApprox(value, progress)) return;
            progress = value;
            QueueRedraw();
        }
    }

    public PoolBar()
    {
        CustomMinimumSize = new Vector2(120, 6);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        Bar(Size.X, Hue with { A = 0.12f });
        if (progress > 0) Bar(Mathf.Lerp(Size.Y, Size.X, progress), Hue with { A = 0.75f });
    }

    void Bar(float width, Color colour)
    {
        var box = new StyleBoxFlat { BgColor = colour };
        box.SetCornerRadiusAll((int)(Size.Y / 2));
        DrawStyleBox(box, new Rect2(0, 0, width, Size.Y));
    }
}
