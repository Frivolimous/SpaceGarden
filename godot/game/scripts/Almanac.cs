using Godot;

namespace SpaceGarden;

// The Almanac (DESIGN.md, Step 4): a window of its own over the desktop (see FloatingWindow), opened and closed by its
// slot in the core's menu or its ✕, moved by dragging its header and resized by the grip in its corner. It keeps its
// place and size until the garden quits.
//
// Tabs across the top: the Journal, the Codex (step 5d) and Evolutions (5c), Stats if player-facing graphs come. The Journal is the
// garden's moments, newest first, under a heading for each day of its life ("Day 2"), each with the time on that day.
// The Codex lists the kinds the garden has discovered (only those: a kind is discovered the first time it grows), each
// with what it does and when it was first found, under Parts, Wild flowers and Fruit. Evolutions lists the garden's
// evolutions (see RefreshEvolutions). The page scrolls under the wheel.
public partial class Almanac : FloatingWindow
{
    const double RefreshSeconds = 1;
    public const float Width = 420, Height = 520; // as it first opens
    static readonly Vector2 MinSize = new(300, 240);

    static readonly Color GeneInk = new(0.82f, 0.74f, 1f); // the genes' purple, lightened
    // A wild flower discovered: a pale gold, in the Journal and the away note.
    public static readonly Color DiscoveryInk = new(1f, 0.93f, 0.68f);
    static readonly Color EvolutionInk = GardenView.Rgb(NodeSpecs.All[NodeKind.Seedling].Rgb).Lightened(0.3f); // a seedling's green

    readonly VBoxContainer list, codex, evolutions;
    readonly ScrollContainer scroll;
    readonly Button journalTab, codexTab, evolutionsTab;
    Tracking? shownFor; // whose journal it shows (a new garden has a new one), and how much of it
    int shownEntries, shownKinds = -1;
    double refreshTimer;
    (Vector2 Pointer, Vector2 Size)? resize; // while the grip is held: where the pointer and the size started
    Vector2 size = new(Width, Height);

    // Being moved or resized: it takes the pointer even off its edge.
    public override bool Dragging => base.Dragging || resize != null;

    public Almanac() : base("Almanac", "Close the Almanac")
    {
        CustomMinimumSize = MinSize;

        // The tabs, then the page.
        var tabs = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        tabs.AddThemeConstantOverride("separation", 2);
        Column.AddChild(tabs);
        var group = new ButtonGroup();
        tabs.AddChild(journalTab = Tab("Journal", group, () => ShowPage(Page.Journal)));
        tabs.AddChild(codexTab = Tab("Codex", group, () => ShowPage(Page.Codex)));
        tabs.AddChild(evolutionsTab = Tab("Evolutions", group, () => ShowPage(Page.Evolutions)));
        journalTab.SetPressedNoSignal(true);
        Column.AddChild(new HSeparator { MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0.3f) });
        scroll = new ScrollContainer
        {
            MouseFilter = MouseFilterEnum.Pass,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        Column.AddChild(scroll);
        var pages = new VBoxContainer { MouseFilter = MouseFilterEnum.Pass, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(pages);
        list = new VBoxContainer { MouseFilter = MouseFilterEnum.Pass, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 4);
        pages.AddChild(list);
        codex = new VBoxContainer { MouseFilter = MouseFilterEnum.Pass, SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = false };
        codex.AddThemeConstantOverride("separation", 4);
        pages.AddChild(codex);
        evolutions = new VBoxContainer { MouseFilter = MouseFilterEnum.Pass, SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = false };
        evolutions.AddThemeConstantOverride("separation", 4);
        pages.AddChild(evolutions);

        // The resize grip, in the bottom right corner.
        var grip = new AlmanacGrip { SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        grip.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press) return;
            resize = (grip.GetGlobalRect().Position + press.Position, size);
            AcceptEvent();
        };
        Column.AddChild(grip);
    }

    public enum Page { Journal, Codex, Evolutions }

    // Shows a page, its tab chosen.
    public void ShowPage(Page page)
    {
        list.Visible = page == Page.Journal;
        codex.Visible = page == Page.Codex;
        evolutions.Visible = page == Page.Evolutions;
        // (Set quietly, so the others are let go by hand: the group only does that for a click.)
        journalTab.SetPressedNoSignal(page == Page.Journal);
        codexTab.SetPressedNoSignal(page == Page.Codex);
        evolutionsTab.SetPressedNoSignal(page == Page.Evolutions);
        scroll.ScrollVertical = 0;
        refreshTimer = 0;
    }

    // A tab across the top: its name, underlined in the core's hue while it's the page showing.
    static Button Tab(string name, ButtonGroup group, Action picked)
    {
        var tab = new Button
        {
            Text = name,
            ToggleMode = true,
            ButtonGroup = group,
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        StyleBoxFlat Style(bool chosen)
        {
            var s = new StyleBoxFlat
            {
                BgColor = chosen ? new Color(1, 1, 1, 0.06f) : Colors.Transparent,
                BorderColor = Tint,
                BorderWidthBottom = chosen ? 2 : 0,
                ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 3, ContentMarginBottom = 4,
            };
            s.SetCornerRadiusAll(0);
            s.CornerRadiusTopLeft = s.CornerRadiusTopRight = 5;
            return s;
        }
        foreach (var state in new[] { "normal", "hover" }) tab.AddThemeStyleboxOverride(state, Style(false));
        foreach (var state in new[] { "pressed", "hover_pressed" }) tab.AddThemeStyleboxOverride(state, Style(true));
        tab.AddThemeFontSizeOverride("font_size", 13);
        tab.AddThemeColorOverride("font_color", Dim);
        tab.AddThemeColorOverride("font_hover_color", Ink);
        tab.AddThemeColorOverride("font_pressed_color", Ink);
        tab.AddThemeColorOverride("font_hover_pressed_color", Ink);
        tab.Toggled += on =>
        {
            if (on) picked();
        };
        return tab;
    }

    protected override void Opened()
    {
        refreshTimer = 0;
        scroll.ScrollVertical = 0; // the newest at the top
    }

    protected override void LetGo()
    {
        base.LetGo();
        resize = null;
    }

    // Every frame: follows the pointer while its header or grip is held, keeps it on the screen, and refreshes its
    // moments now and then.
    public void Update(Garden garden, Vector2 pointer, Vector2 screen, double delta)
    {
        if (resize is var (from, start) && Input.IsMouseButtonPressed(MouseButton.Left))
            size = (start + pointer - from).Max(MinSize);
        Place(pointer, screen, size);

        if (!IsOpen || (refreshTimer -= delta) > 0) return;
        refreshTimer = RefreshSeconds;
        var tracking = garden.Tracking;
        Note.Text = $"Day {Tracking.DayOf(tracking.Age)}, {Tracking.ClockOf(tracking.Age)}";
        if (codex.Visible)
        {
            RefreshCodex(tracking);
            return;
        }
        if (evolutions.Visible)
        {
            RefreshEvolutions(garden);
            return;
        }
        if (tracking == shownFor && tracking.Journal.Count == shownEntries) return;
        shownFor = tracking;
        shownEntries = tracking.Journal.Count;
        foreach (var child in list.GetChildren()) child.QueueFree();
        int day = 0;
        for (int i = tracking.Journal.Count - 1; i >= 0; i--)
        {
            var entry = tracking.Journal[i];
            if (Tracking.DayOf(entry.Age) != day)
            {
                day = Tracking.DayOf(entry.Age);
                list.AddChild(new Label
                {
                    Text = $"Day {day}",
                    MouseFilter = MouseFilterEnum.Pass,
                    LabelSettings = new LabelSettings { FontSize = 14, FontColor = Tint },
                    // A little space above each day but the first.
                    CustomMinimumSize = new Vector2(0, list.GetChildCount() > 0 ? 30 : 0),
                    VerticalAlignment = VerticalAlignment.Bottom,
                });
            }
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
            row.AddThemeConstantOverride("separation", 10);
            row.AddChild(new Label
            {
                Text = Tracking.ClockOf(entry.Age),
                CustomMinimumSize = new Vector2(44, 0),
                SizeFlagsVertical = SizeFlags.ShrinkBegin, // level with its moment's first line
                HorizontalAlignment = HorizontalAlignment.Right,
                MouseFilter = MouseFilterEnum.Pass,
                LabelSettings = new LabelSettings { FontSize = 12, FontColor = Dim },
            });
            row.AddChild(new Label
            {
                Text = entry.Text,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Pass,
                LabelSettings = new LabelSettings { FontSize = 14, FontColor = Colour(entry.Kind) },
            });
            list.AddChild(row);
        }
    }

    // The Codex: the kinds discovered, under Parts, Wild flowers and Fruit, each in the order first found, as cards two
    // to a row (see EntryCard): its shape and name in its hue, what it does (and a wild flower's quirk), and when it was
    // first found.
    void RefreshCodex(Tracking tracking)
    {
        if (tracking == codexFor && tracking.Discovered.Count == shownKinds) return;
        codexFor = tracking;
        shownKinds = tracking.Discovered.Count;
        foreach (var child in codex.GetChildren()) child.QueueFree();
        var found = tracking.Discovered.OrderBy(d => d.Value).ThenBy(d => d.Key).Select(d => (Spec: NodeSpecs.All[d.Key], Age: d.Value)).ToList();
        void Section(string title, Func<NodeSpec, bool> which)
        {
            var kinds = found.Where(f => which(f.Spec)).ToList();
            if (kinds.Count == 0) return;
            codex.AddChild(new Label
            {
                Text = title,
                MouseFilter = MouseFilterEnum.Pass,
                LabelSettings = new LabelSettings { FontSize = 14, FontColor = Tint },
                CustomMinimumSize = new Vector2(0, codex.GetChildCount() > 0 ? 30 : 0),
                VerticalAlignment = VerticalAlignment.Bottom,
            });
            var grid = Cards();
            foreach (var (spec, age) in kinds)
                grid.AddChild(EntryCard(spec.Name, spec.Shape, GardenView.Rgb(spec.Rgb),
                    spec.Wild is { } wild ? $"{spec.Description} {wild.Quirk}" : spec.Description,
                    spec.Kind == NodeKind.Core ? "At the planting" : $"Found day {Tracking.DayOf(age)}, {Tracking.ClockOf(age)}"));
            codex.AddChild(grid);
        }
        Section("Parts", s => !s.IsFruit && !s.IsWild);
        Section("Wildflowers", s => s.IsWild);
        Section("Fruit", s => s.IsFruit);
    }

    Tracking? codexFor;

    // Evolutions (step 5c; Jeremy, Oct 31): every level the garden has chosen, in the order chosen ("Pulsar Power", then
    // "Pulsar Power II"), as cards two to a row: the shape of the flower it blooms into and its name in its role hue,
    // its effect in all at that level ("Core power ×2"), and when it was chosen.
    // Respec (Jeremy, Nov 3): a bloomed evolution's card has Delete, which asks once more, then takes it back (see
    // Garden.Respec).
    public event Action<Evolution>? RespecPressed;

    void RefreshEvolutions(Garden garden)
    {
        var shown = (garden, garden.Evolved.Count, garden.Blooms);
        if (shown == evolutionsShown) return;
        evolutionsShown = shown;
        foreach (var child in evolutions.GetChildren()) child.QueueFree();
        if (garden.Evolved.Count == 0)
        {
            evolutions.AddChild(new Label
            {
                Text = "None yet. Plant a seedling, fill it with power, and choose its evolution.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = MouseFilterEnum.Pass,
                LabelSettings = new LabelSettings { FontSize = 13, FontColor = Dim },
            });
            return;
        }
        var grid = Cards();
        foreach (var evolution in garden.Evolved)
        {
            var spec = evolution.Spec;
            var card = EntryCard(evolution.Name, NodeSpecs.All[spec.Becomes].Shape, GardenView.Rgb(spec.Rgb),
                spec.Effect.At(evolution.Level).Text + ".",
                $"Chosen day {Tracking.DayOf(evolution.ChosenAt)}, {Tracking.ClockOf(evolution.ChosenAt)}");
            if (evolution.Bloomed)
            {
                var delete = new ConfirmButton("", "Delete?",
                    "Take this evolution back: its effect and caps go (its flower stays), and its seedling slot is free for a new seedling")
                { SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
                delete.Confirmed += () => RespecPressed?.Invoke(evolution);
                card.GetChild(0).AddChild(delete);
            }
            grid.AddChild(card);
        }
        evolutions.AddChild(grid);
    }

    (Garden, int, int)? evolutionsShown;

    // Cards two to a row, each half the page wide.
    static GridContainer Cards()
    {
        var grid = new GridContainer { Columns = 2, MouseFilter = MouseFilterEnum.Pass, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        return grid;
    }

    // One entry as a small card (the Codex's and Evolutions' pages): its shape in its hue beside its name, then a few
    // words on what it does, then when (dim).
    static PanelContainer EntryCard(string name, Shape shape, Color hue, string text, string when)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(1, 1, 1, 0.04f),
            BorderColor = new Color(1, 1, 1, 0.1f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6,
        };
        style.SetCornerRadiusAll(6);
        var card = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.Fill, // the row's height, so cards side by side line up
        };
        card.AddThemeStyleboxOverride("panel", style);
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        column.AddThemeConstantOverride("separation", 2);
        card.AddChild(column);
        var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        top.AddThemeConstantOverride("separation", 6);
        top.AddChild(new ShapeIcon(shape, hue));
        top.AddChild(new Label
        {
            Text = name,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Pass,
            LabelSettings = new LabelSettings { FontSize = 14, FontColor = hue.Lightened(0.35f) },
        });
        column.AddChild(top);
        column.AddChild(new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill, // pushes the date to the card's foot
            MouseFilter = MouseFilterEnum.Pass,
            LabelSettings = new LabelSettings { FontSize = 13, FontColor = Ink },
        });
        column.AddChild(new Label
        {
            Text = when,
            MouseFilter = MouseFilterEnum.Pass,
            LabelSettings = new LabelSettings { FontSize = 11, FontColor = Dim },
        });
        return card;
    }

    // A moment's colour: upgrades bought (and the old track's unlocks) in the genes' purple, shaping tools gained in
    // the tools' blue, wild flowers discovered in a pale gold.
    static Color Colour(JournalKind kind) => kind switch
    {
        JournalKind.Purchase or JournalKind.Unlock => GeneInk,
        JournalKind.Tool => ShapingLook.Tint,
        JournalKind.Discovery => DiscoveryInk,
        JournalKind.Ended => Dim,
        JournalKind.Chosen or JournalKind.Bloomed => EvolutionInk,
        _ => Ink,
    };
}

// The corner grip: three short diagonal strokes.
public partial class AlmanacGrip : Control
{
    public AlmanacGrip()
    {
        CustomMinimumSize = new Vector2(14, 14);
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Fdiagsize;
    }

    public override void _Draw()
    {
        var ink = new Color(1, 1, 1, 0.35f);
        for (int i = 1; i <= 3; i++)
            DrawLine(new Vector2(14 - i * 4, 13), new Vector2(13, 14 - i * 4), ink, 1.2f, true);
    }
}

// A part's shape in its hue, as the cards in the Almanac show it (a wild flower's from its own texture too).
public partial class ShapeIcon : Control
{
    readonly Shape shape;
    readonly Color hue;

    public ShapeIcon(Shape shape, Color hue)
    {
        this.shape = shape;
        this.hue = hue;
        CustomMinimumSize = new Vector2(20, 20);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Pass;
    }

    public override void _Draw()
    {
        ToolButton.DrawShape(this, Size / 2, shape, 8.5f, new Color(0, 0, 0, 0.5f));
        ToolButton.DrawShape(this, Size / 2, shape, 7.5f, hue);
    }
}
