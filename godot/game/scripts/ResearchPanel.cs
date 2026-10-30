using Godot;

namespace SpaceGarden;

// The Research panel (DESIGN.md, Upgrade tracks): a window of its own (see FloatingWindow), opened and closed by the
// Research slot in the core's menu or its ✕. Three columns, Stems, Generators and Core, each showing only its track's
// next upgrade, its cost and a Buy button, greyed while the gene balance falls short; the balance shows in the header.
// Buying is the rules' (Garden.Buy): the widget does it on BuyPressed.
public partial class ResearchPanel : FloatingWindow
{
    public event Action<Track>? BuyPressed;

    const double RefreshSeconds = 0.25;
    const float ColumnWidth = 132;

    static readonly Color GeneInk = new(0.82f, 0.74f, 1f); // the genes' purple, lightened

    sealed record TrackColumn(Label Now, Label Name, Label Cost, Button Buy);

    readonly Dictionary<Track, TrackColumn> columns = new();
    double refreshTimer;

    public ResearchPanel() : base("Research", "Close Research")
    {
        Note.LabelSettings.FontColor = GeneInk;
        Note.LabelSettings.FontSize = 13;
        Column.AddChild(new HSeparator { MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0.3f) });
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 14);
        Column.AddChild(row);
        foreach (var (track, title) in new[] { (Track.Stems, "Stems"), (Track.Generators, "Generators"), (Track.Core, "Core") })
            row.AddChild(TrackPage(track, title));
        Column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6), MouseFilter = MouseFilterEnum.Ignore });
    }

    // A track's column: its title, what it has given so far, then its next upgrade, the cost and Buy.
    Control TrackPage(Track track, string title)
    {
        var page = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(ColumnWidth, 0) };
        page.AddThemeConstantOverride("separation", 2);
        Label Text(int size, Color colour)
        {
            var label = new Label
            {
                MouseFilter = MouseFilterEnum.Ignore,
                LabelSettings = new LabelSettings { FontSize = size, FontColor = colour },
            };
            page.AddChild(label);
            return label;
        }
        Text(15, Tint).Text = title;
        var now = Text(12, Dim);
        page.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4), MouseFilter = MouseFilterEnum.Ignore });
        var name = Text(14, Ink);
        var cost = Text(13, GeneInk);
        var buy = new Button
        {
            Text = "Buy",
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(64, 0),
        };
        StyleBoxFlat Style(float alpha)
        {
            var s = new StyleBoxFlat
            {
                BgColor = GeneInk with { A = alpha },
                ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 2, ContentMarginBottom = 3,
            };
            s.SetCornerRadiusAll(6);
            return s;
        }
        buy.AddThemeStyleboxOverride("normal", Style(0.22f));
        buy.AddThemeStyleboxOverride("hover", Style(0.36f));
        buy.AddThemeStyleboxOverride("pressed", Style(0.5f));
        buy.AddThemeStyleboxOverride("disabled", Style(0.06f));
        buy.AddThemeFontSizeOverride("font_size", 13);
        buy.AddThemeColorOverride("font_color", Ink);
        buy.AddThemeColorOverride("font_hover_color", Colors.White);
        buy.AddThemeColorOverride("font_disabled_color", new Color(1, 1, 1, 0.3f));
        buy.Pressed += () => BuyPressed?.Invoke(track);
        page.AddChild(new Control { CustomMinimumSize = new Vector2(0, 2), MouseFilter = MouseFilterEnum.Ignore });
        page.AddChild(buy);
        columns[track] = new TrackColumn(now, name, cost, buy);
        return page;
    }

    protected override void Opened() => refreshTimer = 0;

    // Shows at once: an upgrade just bought.
    public void RefreshNow() => refreshTimer = 0;

    // Every frame: follows the pointer while its header is held, keeps it on the screen, and refreshes its columns
    // a few times a second.
    public void Update(Garden garden, Vector2 pointer, Vector2 screen, double delta)
    {
        Place(pointer, screen, GetCombinedMinimumSize());
        if (!IsOpen || (refreshTimer -= delta) > 0) return;
        refreshTimer = RefreshSeconds;
        Note.Text = $"{Math.Floor(garden.GeneBalance):#,0} genes";
        var caps = garden.Capacity;
        foreach (var (track, column) in columns)
        {
            column.Now.Text = track switch
            {
                Track.Stems => $"Cap {Of(caps.Stems)}",
                Track.Generators => $"Cap {Of(caps.Generators)}",
                _ => $"{garden.CoreBought} of {Tuning.CoreCosts.Length} bought",
            };
            var next = garden.Next(track);
            column.Name.Text = next?.Name ?? "All bought";
            column.Cost.Visible = column.Buy.Visible = next != null;
            column.Name.TooltipText = next?.Does ?? "";
            column.Name.MouseFilter = next != null ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
            if (next == null) continue;
            column.Cost.Text = $"{next.Cost:#,0} genes";
            bool affordable = garden.CanSpend(next.Cost);
            column.Buy.Disabled = !affordable;
            column.Buy.TooltipText = affordable ? next.Does : $"{Math.Ceiling(next.Cost - garden.GeneBalance):#,0} more genes needed";
        }
    }

    static string Of(int cap) => cap == int.MaxValue ? "unlimited" : $"{cap}";
}
