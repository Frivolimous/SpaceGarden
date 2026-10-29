using Godot;

namespace SpaceGarden;

// The Almanac (DESIGN.md, Step 4): a window of its own over the desktop, opened and closed by its slot in the core's
// menu or its ✕, moved by dragging its header and resized by the grip in its corner. It stays open whatever else
// happens (clicks elsewhere, the focus going, Escape), out of sight only while the garden isn't Active (nothing but
// the core takes clicks then), and keeps its place and size until the garden quits.
//
// Tabs across the top, shown even with one: the Journal now, the Codex in step 5, Stats if player-facing graphs come.
// The Journal is the garden's moments, newest first, under a heading for each day of its life ("Day 2"), each with
// the time on that day. The list scrolls under the wheel.
public partial class Almanac : PanelContainer
{
    public event Action? CloseRequested;

    const double RefreshSeconds = 1;
    public const float Width = 420, Height = 520; // as it first opens
    static readonly Vector2 MinSize = new(300, 240);

    static readonly Color Ink = new(1, 1, 1, 0.9f), Dim = new(1, 1, 1, 0.5f);
    static readonly Color Tint = GardenView.Rgb(NodeSpecs.All[NodeKind.Core].Rgb).Lightened(0.35f);
    static readonly Color UnlockInk = new(0.82f, 0.74f, 1f); // the genes' purple, as the unlock notes

    readonly Label age;
    readonly VBoxContainer list;
    readonly ScrollContainer scroll;
    Tracking? shownFor; // whose journal it shows (a new garden has a new one), and how much of it
    int shownEntries;
    double refreshTimer;
    Tween? fade;
    Vector2? grab; // while the header is held: where on the window the pointer took it
    (Vector2 Pointer, Vector2 Size)? resize; // while the grip is held: where the pointer and the size started
    Vector2 size = new(Width, Height);
    bool away;

    public bool IsOpen { get; private set; }

    // Whether it has been set down yet (later it opens where it was left).
    public bool Placed { get; private set; }

    // Being moved or resized: it takes the pointer even off its edge.
    public bool Dragging => grab != null || resize != null;

    public Almanac()
    {
        MouseFilter = MouseFilterEnum.Stop; // a click on it stays on it, and the wheel scrolls it
        Visible = false;
        CustomMinimumSize = MinSize;
        var style = Toolbar.PillStyle();
        style.SetCornerRadiusAll(10);
        style.ContentMarginLeft = style.ContentMarginRight = 12;
        style.ContentMarginTop = 4;
        style.ContentMarginBottom = 2;
        AddThemeStyleboxOverride("panel", style);
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 4);
        AddChild(column);

        // The header: its title and the garden's day and time, to drag it by, and the ✕.
        var bar = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.Move };
        bar.AddThemeConstantOverride("separation", 10);
        column.AddChild(bar);
        bar.AddChild(new Label
        {
            Text = "Almanac",
            MouseFilter = MouseFilterEnum.Ignore,
            LabelSettings = new LabelSettings { FontSize = 17, FontColor = Tint },
        });
        age = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            LabelSettings = new LabelSettings { FontSize = 12, FontColor = Dim },
        };
        bar.AddChild(age);
        var close = new Button
        {
            Text = "✕",
            Flat = true,
            TooltipText = "Close the Almanac",
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        close.AddThemeFontSizeOverride("font_size", 15);
        close.AddThemeColorOverride("font_color", Dim);
        close.AddThemeColorOverride("font_hover_color", Ink);
        close.Pressed += () => CloseRequested?.Invoke();
        bar.AddChild(close);
        bar.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press) return;
            grab = bar.GetGlobalRect().Position + press.Position - Position;
            AcceptEvent();
        };

        // The tabs, then the page.
        var tabs = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        tabs.AddThemeConstantOverride("separation", 2);
        column.AddChild(tabs);
        tabs.AddChild(Tab("Journal"));
        column.AddChild(new HSeparator { MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0.3f) });
        scroll = new ScrollContainer
        {
            MouseFilter = MouseFilterEnum.Pass,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        column.AddChild(scroll);
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
        column.AddChild(grip);
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

    // Opens it, set down at spot the first time (later it opens where it was left).
    public void Open(Vector2 spot)
    {
        if (IsOpen) return;
        IsOpen = true;
        if (!Placed) Position = spot;
        Placed = true;
        refreshTimer = 0;
        scroll.ScrollVertical = 0; // the newest at the top
        if (away) return;
        FadeIn();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        grab = null;
        resize = null;
        FadeOut();
    }

    // Out of sight while the garden isn't Active, and back as it was after.
    public void SetAway(bool value)
    {
        if (value == away) return;
        away = value;
        grab = null;
        resize = null;
        if (!IsOpen) return;
        if (away) FadeOut();
        else FadeIn();
    }

    void FadeIn()
    {
        if (!Visible) Modulate = new Color(1, 1, 1, 0);
        Visible = true;
        fade?.Kill();
        fade = CreateTween();
        fade.TweenProperty(this, "modulate:a", 1.0, 0.15);
    }

    void FadeOut()
    {
        fade?.Kill();
        fade = CreateTween();
        fade.TweenProperty(this, "modulate:a", 0.0, 0.2);
        fade.TweenCallback(Callable.From(() => Visible = false));
    }

    // Whether the pointer is on it (in its layer's units).
    public bool Catches(Vector2 point) => Visible && IsOpen && GetGlobalRect().HasPoint(point);

    // Every frame: follows the pointer while its header or grip is held (polled, as the widget polls the pointer: no
    // events arrive while clicks fall through), keeps it on the screen, and refreshes its moments now and then.
    public void Update(Garden garden, Vector2 pointer, Vector2 screen, double delta)
    {
        bool held = Input.IsMouseButtonPressed(MouseButton.Left);
        if (!held)
        {
            grab = null;
            resize = null;
        }
        if (grab is { } offset) Position = pointer - offset;
        if (resize is var (from, start)) size = (start + pointer - from).Max(MinSize);
        var room = (screen - new Vector2(8, 8)).Max(MinSize);
        Size = size.Min(room);
        Position = Position.Clamp(new Vector2(4, 4), (screen - Size - new Vector2(4, 4)).Max(new Vector2(4, 4)));

        if (!IsOpen || (refreshTimer -= delta) > 0) return;
        refreshTimer = RefreshSeconds;
        var tracking = garden.Tracking;
        age.Text = $"Day {Tracking.DayOf(tracking.Age)}, {Tracking.ClockOf(tracking.Age)}";
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
                LabelSettings = new LabelSettings { FontSize = 14, FontColor = entry.Kind == JournalKind.Unlock ? UnlockInk : Ink },
            });
            list.AddChild(row);
        }
    }
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
