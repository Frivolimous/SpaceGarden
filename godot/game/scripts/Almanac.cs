using Godot;

namespace SpaceGarden;

// The Almanac (DESIGN.md, Step 4): a window of its own over the desktop (see FloatingWindow), opened and closed by its
// slot in the core's menu or its ✕, moved by dragging its header and resized by the grip in its corner. It keeps its
// place and size until the garden quits.
//
// Tabs across the top, shown even with one: the Journal now, the Codex in step 5, Stats if player-facing graphs come.
// The Journal is the garden's moments, newest first, under a heading for each day of its life ("Day 2"), each with
// the time on that day. The list scrolls under the wheel.
public partial class Almanac : FloatingWindow
{
    const double RefreshSeconds = 1;
    public const float Width = 420, Height = 520; // as it first opens
    static readonly Vector2 MinSize = new(300, 240);

    static readonly Color GeneInk = new(0.82f, 0.74f, 1f); // the genes' purple, lightened

    readonly VBoxContainer list;
    readonly ScrollContainer scroll;
    Tracking? shownFor; // whose journal it shows (a new garden has a new one), and how much of it
    int shownEntries;
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
        tabs.AddChild(Tab("Journal"));
        Column.AddChild(new HSeparator { MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0.3f) });
        scroll = new ScrollContainer
        {
            MouseFilter = MouseFilterEnum.Pass,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        Column.AddChild(scroll);
        list = new VBoxContainer { MouseFilter = MouseFilterEnum.Pass, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(list);

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

    // A tab across the top: its name, underlined in the core's hue while it's the page showing.
    static Button Tab(string name)
    {
        var tab = new Button
        {
            Text = name,
            ToggleMode = true,
            ButtonPressed = true,
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
        tab.Toggled += _ => tab.ButtonPressed = true; // the only page, for now: it stays chosen
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

    // A moment's colour: upgrades bought (and the old track's unlocks) in the genes' purple, shaping tools gained in
    // the tools' blue.
    static Color Colour(JournalKind kind) => kind switch
    {
        JournalKind.Purchase or JournalKind.Unlock => GeneInk,
        JournalKind.Tool => ShapingLook.Tint,
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
