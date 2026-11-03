using Godot;

namespace SpaceGarden;

// A window of its own over the desktop (the Almanac, the Research panel): a header with its title, a short note beside
// it and a ✕, moved by dragging the header. Only its slot in the core's menu or its ✕ opens and closes it: it stays
// open whatever else happens (clicks elsewhere, the focus going, Escape), out of sight only while the garden isn't
// Active (nothing but the core takes clicks then), and keeps its place until the garden quits.
public partial class FloatingWindow : PanelContainer
{
    public event Action? CloseRequested;

    protected static readonly Color Ink = new(1, 1, 1, 0.9f), Dim = new(1, 1, 1, 0.5f);
    protected static readonly Color Tint = GardenView.Rgb(NodeSpecs.All[NodeKind.Core].Rgb).Lightened(0.35f);

    // Its page, under the header; and the note beside its title.
    protected readonly VBoxContainer Column;
    protected readonly Label Note;
    readonly HBoxContainer Bar;
    Tween? fade;
    Vector2? grab; // while the header is held: where on the window the pointer took it
    bool away;

    public bool IsOpen { get; private set; }

    // Whether it has been set down yet (later it opens where it was left).
    public bool Placed { get; private set; }

    // Being moved: it takes the pointer even off its edge.
    public virtual bool Dragging => grab != null;

    public FloatingWindow(string title, string closeTip)
    {
        MouseFilter = MouseFilterEnum.Stop; // a click on it stays on it, and the wheel scrolls it
        Visible = false;
        var style = Toolbar.PillStyle();
        style.SetCornerRadiusAll(10);
        style.ContentMarginLeft = style.ContentMarginRight = 12;
        style.ContentMarginTop = 4;
        style.ContentMarginBottom = 2;
        AddThemeStyleboxOverride("panel", style);
        Column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        Column.AddThemeConstantOverride("separation", 4);
        AddChild(Column);

        // The header: its title and note, to drag it by, and the ✕.
        var bar = Bar = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.Move };
        bar.AddThemeConstantOverride("separation", 10);
        Column.AddChild(bar);
        bar.AddChild(new Label
        {
            Text = title,
            MouseFilter = MouseFilterEnum.Ignore,
            LabelSettings = new LabelSettings { FontSize = 17, FontColor = Tint },
        });
        Note = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            LabelSettings = new LabelSettings { FontSize = 12, FontColor = Dim },
        };
        bar.AddChild(Note);
        var close = new Button
        {
            Text = "✕",
            Flat = true,
            TooltipText = closeTip,
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
    }

    // A small flat button in the header, before the ✕ (the Research panel's minimise).
    protected Button HeaderButton(string text, string tip)
    {
        var button = new Button
        {
            Text = text,
            Flat = true,
            TooltipText = tip,
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        button.AddThemeFontSizeOverride("font_size", 15);
        button.AddThemeColorOverride("font_color", Dim);
        button.AddThemeColorOverride("font_hover_color", Ink);
        Bar.AddChild(button);
        Bar.MoveChild(button, Bar.GetChildCount() - 2);
        return button;
    }

    // Opens it, set down at spot the first time (later it opens where it was left).
    public void Open(Vector2 spot)
    {
        if (IsOpen) return;
        IsOpen = true;
        if (!Placed) Position = spot;
        Placed = true;
        Opened();
        if (away) return;
        FadeIn();
    }

    // Just opened: a chance to refresh its page.
    protected virtual void Opened() { }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        LetGo();
        FadeOut();
    }

    // Out of sight while the garden isn't Active, and back as it was after.
    public void SetAway(bool value)
    {
        if (value == away) return;
        away = value;
        LetGo();
        if (!IsOpen) return;
        if (away) FadeOut();
        else FadeIn();
    }

    // The pointer let go of whatever it held on the window.
    protected virtual void LetGo() => grab = null;

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
    // arrive while clicks fall through), and keeps it on the screen at this size (or the screen's, if smaller).
    protected void Place(Vector2 pointer, Vector2 screen, Vector2 size)
    {
        if (!Input.IsMouseButtonPressed(MouseButton.Left)) LetGo();
        if (grab is { } offset) Position = pointer - offset;
        var room = (screen - new Vector2(8, 8)).Max(CustomMinimumSize);
        Size = size.Min(room);
        Position = Position.Clamp(new Vector2(4, 4), (screen - Size - new Vector2(4, 4)).Max(new Vector2(4, 4)));
    }
}

// A small button that asks before it acts (Jeremy, Nov 3: "delete -> confirm delete"): the first click turns it into
// its Confirm text, in red, for ConfirmSeconds; a second click in that time fires Confirmed; otherwise it goes back.
public partial class ConfirmButton : Button
{
    public event Action? Confirmed;

    const double ConfirmSeconds = 3;
    static readonly Color Calm = new(1, 1, 1, 0.55f), Alarm = new(1, 0.45f, 0.4f);

    readonly string label, confirm;
    double armed;

    public ConfirmButton(string label, string confirm, string tooltip)
    {
        this.label = label;
        this.confirm = confirm;
        Text = label;
        TooltipText = tooltip;
        Flat = true;
        FocusMode = FocusModeEnum.None;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        AddThemeFontSizeOverride("font_size", 11);
        Paint(Calm);
        Pressed += () =>
        {
            if (armed > 0)
            {
                Disarm();
                Confirmed?.Invoke();
                return;
            }
            armed = ConfirmSeconds;
            Text = this.confirm;
            Paint(Alarm);
        };
    }

    void Paint(Color colour)
    {
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color" })
            AddThemeColorOverride(state, state == "font_color" ? colour : colour.Lightened(0.25f));
    }

    void Disarm()
    {
        armed = 0;
        Text = label;
        Paint(Calm);
    }

    public override void _Process(double delta)
    {
        if (armed > 0 && (armed -= delta) <= 0) Disarm();
    }
}
