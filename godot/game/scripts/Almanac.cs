using Godot;

namespace SpaceGarden;

// The Almanac (DESIGN.md, Step 4): a window of its own over the desktop (see FloatingWindow), opened and closed by its
// slot in the core's menu or its ✕, moved by dragging its header and resized by the grip in its corner. It keeps its
// place and size until the garden quits.
//
// Tabs across the top: the Journal and the Codex (step 5d), Stats if player-facing graphs come. The Journal is the
// garden's moments, newest first, under a heading for each day of its life ("Day 2"), each with the time on that day.
// The Codex lists the kinds the garden has discovered (only those: a kind is discovered the first time it grows), each
// with what it does and when it was first found, under Parts, Wild flowers and Fruit. The page scrolls under the wheel.
public partial class Almanac : FloatingWindow
{
    const double RefreshSeconds = 1;
    public const float Width = 420, Height = 520; // as it first opens
    static readonly Vector2 MinSize = new(300, 240);

    static readonly Color GeneInk = new(0.82f, 0.74f, 1f); // the genes' purple, lightened
    // A wild flower discovered: a pale gold, in the Journal and the away note.
    public static readonly Color DiscoveryInk = new(1f, 0.93f, 0.68f);
    static readonly Color EvolutionInk = GardenView.Rgb(NodeSpecs.All[NodeKind.Seedling].Rgb).Lightened(0.3f); // a seedling's green

    readonly VBoxContainer list, codex;
    readonly ScrollContainer scroll;
    readonly Button journalTab, codexTab;
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
        tabs.AddChild(journalTab = Tab("Journal", group, () => ShowCodex(false)));
        tabs.AddChild(codexTab = Tab("Codex", group, () => ShowCodex(true)));
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

    // Shows the Codex's page (or the Journal's), its tab chosen.
    public void ShowCodex(bool shown)
    {
        codex.Visible = shown;
        list.Visible = !shown;
        (shown ? codexTab : journalTab).SetPressedNoSignal(true);
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

    // The Codex: the kinds discovered, under Parts, Wild flowers and Fruit, each in the order first found. Each shows
    // its name in its hue and when it was first found, then what it does (and a wild flower's quirk).
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
            foreach (var (spec, age) in kinds)
            {
                var entry = new VBoxContainer { MouseFilter = MouseFilterEnum.Pass };
                entry.AddThemeConstantOverride("separation", 0);
                var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
                top.AddChild(new Label
                {
                    Text = spec.Name,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    MouseFilter = MouseFilterEnum.Pass,
                    LabelSettings = new LabelSettings { FontSize = 14, FontColor = GardenView.Rgb(spec.Rgb).Lightened(0.35f) },
                });
                top.AddChild(new Label
                {
                    Text = spec.Kind == NodeKind.Core ? "At the planting" : $"Found day {Tracking.DayOf(age)}, {Tracking.ClockOf(age)}",
                    MouseFilter = MouseFilterEnum.Pass,
                    LabelSettings = new LabelSettings { FontSize = 12, FontColor = Dim },
                });
                entry.AddChild(top);
                string text = spec.Wild is { } wild ? $"{spec.Description} {wild.Quirk}" : spec.Description;
                entry.AddChild(new Label
                {
                    Text = text,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    MouseFilter = MouseFilterEnum.Pass,
                    LabelSettings = new LabelSettings { FontSize = 13, FontColor = Ink },
                });
                codex.AddChild(entry);
            }
        }
        Section("Parts", s => !s.IsFruit && !s.IsWild);
        Section("Wild flowers", s => s.IsWild);
        Section("Fruit", s => s.IsFruit);
    }

    Tracking? codexFor;

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
