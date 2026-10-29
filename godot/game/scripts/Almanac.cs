using Godot;

namespace SpaceGarden;

// The Almanac (DESIGN.md, Step 4): the garden's journal of moments, newest first, each with the garden's age when it
// happened. A window of its own over the desktop, opened and closed by its slot in the core's menu or its ✕, and
// moved by dragging its header. It stays open whatever else happens (clicks elsewhere, the focus going, Escape), out of
// sight only while the garden isn't Active (nothing but the core takes clicks then), and keeps its place until the
// garden quits. No stats page and no catalogue: just the moments. The list scrolls under the wheel.
public partial class Almanac : PanelContainer
{
    public event Action? CloseRequested;

    const double RefreshSeconds = 1;
    public const float Width = 420, Height = 520;

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
    bool away;

    public bool IsOpen { get; private set; }

    // Where it was set down (its top left, in its layer's units), once it has been; kept while the garden runs.
    public bool Placed { get; private set; }

    public bool Dragging => grab != null;

    public Almanac()
    {
        MouseFilter = MouseFilterEnum.Stop; // a click on it stays on it, and the wheel scrolls it
        Visible = false;
        Size = CustomMinimumSize = new Vector2(Width, Height);
        var style = Toolbar.PillStyle();
        style.SetCornerRadiusAll(10);
        style.ContentMarginLeft = style.ContentMarginRight = 12;
        style.ContentMarginTop = 4;
        style.ContentMarginBottom = 10;
        AddThemeStyleboxOverride("panel", style);
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 6);
        AddChild(column);

        // The header: its title and the garden's age, to drag it by, and the ✕.
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
        FadeOut();
    }

    // Out of sight while the garden isn't Active, and back as it was after.
    public void SetAway(bool value)
    {
        if (value == away) return;
        away = value;
        grab = null;
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

    // Every frame: follows the pointer while its header is held (polled, as the widget polls the pointer: no events
    // arrive while clicks fall through), keeps it on the screen, and refreshes its moments now and then.
    public void Update(Garden garden, Vector2 pointer, Vector2 screen, double delta)
    {
        if (grab is { } held)
        {
            if (Input.IsMouseButtonPressed(MouseButton.Left)) Position = pointer - held;
            else grab = null;
        }
        Size = new Vector2(Width, Mathf.Min(Height, screen.Y - 8));
        Position = Position.Clamp(new Vector2(4, 4), (screen - Size - new Vector2(4, 4)).Max(new Vector2(4, 4)));

        if (!IsOpen || (refreshTimer -= delta) > 0) return;
        refreshTimer = RefreshSeconds;
        var tracking = garden.Tracking;
        age.Text = $"garden age {Tracking.AgeText(tracking.Age)}";
        if (tracking == shownFor && tracking.Journal.Count == shownEntries) return;
        shownFor = tracking;
        shownEntries = tracking.Journal.Count;
        foreach (var child in list.GetChildren()) child.QueueFree();
        for (int i = tracking.Journal.Count - 1; i >= 0; i--)
        {
            var entry = tracking.Journal[i];
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
            row.AddThemeConstantOverride("separation", 10);
            row.AddChild(new Label
            {
                Text = Tracking.AgeText(entry.Age),
                CustomMinimumSize = new Vector2(58, 0),
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
                CustomMinimumSize = new Vector2(Width - 120, 0),
                MouseFilter = MouseFilterEnum.Pass,
                LabelSettings = new LabelSettings { FontSize = 14, FontColor = entry.Kind == JournalKind.Unlock ? UnlockInk : Ink },
            });
            list.AddChild(row);
        }
    }
}
