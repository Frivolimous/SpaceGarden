using Godot;
using Num = System.Numerics;

namespace SpaceGarden;

// The desktop overlay: a borderless, transparent, always-on-top window over the whole work area of the screen (all
// of it but the taskbar), so the plant seems to live on the desktop itself. It only catches the mouse over the plant
// and its toolbar; everywhere else clicks fall through to whatever is underneath. The window never moves or resizes.
//
// The plant is drawn at a fixed scale (the Size setting times the screen's DPI) with the garden's units as logical
// pixels from the window's top-left corner, so a part's place in the garden is its place on the desktop. The
// screen's edges are the garden's borders.
//
// Resting the pointer on a part rings it; tapping it (a click without a drag) opens its toolbar. Dragging a part
// pulls it to the pointer by its weight and the links drag the rest after it; letting go keeps the momentum.
// Right-click opens the menu. It drops to a low frame rate when nothing's happening.
public partial class Widget : Node
{
    // Idle it draws at 30 fps; while anything's happening, at the monitor's rate (0: no cap, vsync holds it there).
    // It counts as settled once no part has moved more than SettledSpeed a tick for SettleSeconds.
    const int IdleFps = 30;
    const int ActiveFps = 0;
    const float SettledSpeed = 0.3f;
    const double SettleSeconds = 1;
    const float DragThreshold = 4;
    const float HoverSlop = 5;
    const double LeaveDelay = 1.5;
    const float RegionMargin = 14; // the passthrough region's margin where click-through isn't supported

    public static readonly float[] Sizes = [0.5f, 0.75f, 1f, 1.25f];

    enum MenuId { AlwaysOnTop, Speed1, Speed60, Speed3600, Reset, Quit, Size0 = 100 }

    readonly Main main;
    readonly PopupMenu menu = new();
    readonly Toolbar toolbar = new();
    readonly CanvasLayer toolbarLayer = new() { Layer = 10 };

    public float Dpi { get; private set; } = 1;
    float plantScale = 0.75f;

    Vector2I dragStartMouse;
    PlantNode? pressNode;
    bool pressed, dragging;
    double leaveTime, regionTimer, activeTime;

    public Widget(Main main) => this.main = main;

    Garden Garden => main.Garden;

    // Screen pixels per garden unit.
    float Scale => Dpi * plantScale;

    public float PlantScale
    {
        get => plantScale;
        set
        {
            // Rescale around the core, so the plant stays planted where it is.
            var coreOnScreen = GardenView.V(Garden.Core.Position) * Scale;
            plantScale = value;
            Garden.Translate(ToSim(coreOnScreen / Scale) - Garden.Core.Position);
            ApplyTransform();
            CheckMenu();
        }
    }

    public override void _Ready()
    {
        var window = GetWindow();
        window.Borderless = true;
        window.AlwaysOnTop = true;
        window.Transparent = true;
        GetViewport().TransparentBg = true;

        toolbarLayer.AddChild(toolbar);
        AddChild(toolbarLayer);
        toolbar.Used += Use;
        // A click anywhere off the plant lands in another window, which takes the focus: close the toolbar then.
        window.FocusExited += toolbar.HideAway;

        menu.AddCheckItem("Always on top", (int)MenuId.AlwaysOnTop);
        menu.SetItemChecked(menu.GetItemIndex((int)MenuId.AlwaysOnTop), true);
        menu.AddSeparator("Size");
        for (int i = 0; i < Sizes.Length; i++) menu.AddRadioCheckItem($"{Sizes[i] * 100:0}%", (int)MenuId.Size0 + i);
        menu.AddSeparator("Growth speed");
        menu.AddRadioCheckItem("Real time (1x)", (int)MenuId.Speed1);
        menu.AddRadioCheckItem("Fast (60x)", (int)MenuId.Speed60);
        menu.AddRadioCheckItem("Very fast (3600x)", (int)MenuId.Speed3600);
        menu.AddSeparator();
        menu.AddItem("New garden", (int)MenuId.Reset);
        menu.AddItem("Quit", (int)MenuId.Quit);
        menu.IdPressed += OnMenu;
        AddChild(menu);
    }

    // Covers the work area of the main screen. A window covering the whole monitor would be taken by Windows for a
    // fullscreen app (hiding the taskbar, holding back notifications), so with an auto-hiding taskbar it stops a
    // pixel short.
    public void CoverScreen(float scale)
    {
        var window = GetWindow();
        Dpi = Mathf.Clamp(Mathf.Round(DisplayServer.ScreenGetDpi() / 96f * 4) / 4, 1, 3);
        plantScale = scale;
        menu.ContentScaleFactor = Dpi;
        toolbarLayer.Scale = new Vector2(Dpi, Dpi);
        var usable = DisplayServer.ScreenGetUsableRect();
        if (usable == new Rect2I(DisplayServer.ScreenGetPosition(), DisplayServer.ScreenGetSize()))
            usable.Size -= new Vector2I(0, 1);
        window.Position = usable.Position;
        window.Size = usable.Size;
        ApplyTransform();
        CheckMenu();
    }

    void ApplyTransform()
    {
        GetViewport().CanvasTransform = Transform2D.Identity.Scaled(new Vector2(Scale, Scale));
        var size = (Vector2)GetWindow().Size / Scale;
        Garden.Borders = (Num.Vector2.Zero, ToSim(size));
    }

    // Where a new plant is set down: towards the bottom right, with room all round for it to spread (an upright
    // one stands on the taskbar).
    public Num.Vector2 HomeSpot()
    {
        var size = (Vector2)GetWindow().Size;
        if (Garden.Planted)
            return ToSim(new Vector2(size.X - 260 * Dpi, size.Y - 2 * Dpi) / Scale - new Vector2(0, NodeSpecs.All[NodeKind.Core].Radius));
        return ToSim(new Vector2(size.X - 320 * Dpi, size.Y - 300 * Dpi) / Scale);
    }

    // A garden whose core isn't on this screen (another monitor, another resolution) is brought home.
    public void KeepOnScreen()
    {
        var (min, max) = Garden.Borders!.Value;
        var core = Garden.Core.Position;
        if (core.X < min.X || core.Y < min.Y || core.X > max.X || core.Y > max.Y)
            Garden.Translate(HomeSpot() - core);
    }

    // ==== EVERY FRAME ==== \\

    public override void _Process(double delta)
    {
        var pointer = PointerInViewport();
        bool overToolbar = toolbar.Visible && toolbar.GetRect().Grow(4).HasPoint(pointer / Dpi);
        bool inWindow = GetViewport().GetVisibleRect().HasPoint(pointer);
        var under = inWindow && !overToolbar ? Garden.NodeAt(ToSim(ToWorld(pointer)), HoverSlop) : null;

        if (pressed && !Input.IsMouseButtonPressed(MouseButton.Left)) Release();

        if (dragging) Garden.HoldTarget = ToSim(ToWorld(pointer));
        else UpdateToolbar(delta, under, overToolbar);

        if (toolbar.Target is { } target)
        {
            if (!Garden.Nodes.Contains(target)) toolbar.HideAway();
            else
            {
                toolbar.Follow(ToViewport(target.Position) / Dpi, target.Spec.Radius * plantScale, GetViewport().GetVisibleRect().Size / Dpi);
                toolbar.Refresh();
            }
        }
        main.View.Highlight = dragging || pressed ? pressNode : under ?? toolbar.Target;

        bool catchMouse = pressed || overToolbar || under != null;
        if (ClickThrough.Supported) ClickThrough.Set(!catchMouse);
        else UpdateRegion(delta);
        bool active = catchMouse || toolbar.Visible || Garden.Held != null || Garden.Speed > SettledSpeed;
        activeTime = active ? SettleSeconds : activeTime - delta;
        Engine.MaxFps = activeTime > 0 ? ActiveFps : IdleFps;
    }

    // A tapped part's toolbar goes once the pointer has been off both the part and the toolbar for a while (see
    // Release and _Ready for the other ways it closes).
    void UpdateToolbar(double delta, PlantNode? under, bool overToolbar)
    {
        if (toolbar.Target != null && under != toolbar.Target && !overToolbar)
        {
            leaveTime += delta;
            if (leaveTime >= LeaveDelay) toolbar.HideAway();
        }
        else leaveTime = 0;
    }

    // Fallback for other platforms: a passthrough region around the plant. (On Windows nothing outside it would be
    // drawn, hence ClickThrough there.)
    void UpdateRegion(double delta)
    {
        regionTimer -= delta;
        if (regionTimer > 0) return;
        regionTimer = 0.25;
        var (min, max) = Garden.Bounds();
        var toWindow = GetViewport().GetCanvasTransform();
        var a = toWindow * (GardenView.V(min) - new Vector2(RegionMargin, RegionMargin));
        var b = toWindow * (GardenView.V(max) + new Vector2(RegionMargin, RegionMargin));
        DisplayServer.WindowSetMousePassthrough([a, new Vector2(b.X, a.Y), b, new Vector2(a.X, b.Y)]);
    }

    // ==== INPUT ==== \\

    // Unhandled, so the toolbar's buttons get their clicks first.
    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
        {
            if (button.Pressed)
            {
                pressNode = Garden.NodeAt(ToSim(ToWorld(button.Position)), HoverSlop);
                if (pressNode == null) return;
                pressed = true;
                dragging = false;
                dragStartMouse = DisplayServer.MouseGetPosition();
                GetViewport().SetInputAsHandled();
            }
            else if (pressed) Release();
        }
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        {
            menu.Popup(new Rect2I(DisplayServer.MouseGetPosition(), Vector2I.Zero));
        }
        else if (e is InputEventMouseMotion && pressed && !dragging)
        {
            if (((Vector2)(DisplayServer.MouseGetPosition() - dragStartMouse)).Length() > DragThreshold * Dpi)
            {
                dragging = true;
                toolbar.HideAway();
                Garden.Grab(pressNode!);
            }
        }
    }

    // A tap (a press without a drag) opens the part's toolbar, or closes it if it was already open.
    void Release()
    {
        if (!dragging && pressNode != null && Garden.Nodes.Contains(pressNode))
        {
            if (toolbar.Target == pressNode) toolbar.HideAway();
            else toolbar.ShowFor(pressNode);
        }
        if (Garden.Held != null) Garden.LetGo();
        pressed = dragging = false;
        pressNode = null;
    }

    // Picking a tool does its job and closes the toolbar.
    void Use(Tool tool)
    {
        if (toolbar.Target is not { } node) return;
        switch (tool)
        {
            case Tool.Grow: Garden.SproutFrom(node); break;
            case Tool.Pin: Garden.TogglePin(node); break;
            case Tool.Prune: main.View.Drop(Garden.Prune(node)); break;
        }
        toolbar.HideAway();
    }

    void OnMenu(long id)
    {
        switch ((MenuId)id)
        {
            case MenuId.AlwaysOnTop:
                var window = GetWindow();
                window.AlwaysOnTop = !window.AlwaysOnTop;
                menu.SetItemChecked(menu.GetItemIndex((int)MenuId.AlwaysOnTop), window.AlwaysOnTop);
                break;
            case MenuId.Speed1: main.GrowthSpeed = 1; break;
            case MenuId.Speed60: main.GrowthSpeed = 60; break;
            case MenuId.Speed3600: main.GrowthSpeed = 3600; break;
            case MenuId.Reset:
                toolbar.HideAway();
                main.NewGarden();
                break;
            case MenuId.Quit: main.SaveAndQuit(); break;
            case >= MenuId.Size0: PlantScale = Sizes[(int)id - (int)MenuId.Size0]; break;
        }
        CheckMenu();
    }

    public void CheckMenu()
    {
        menu.SetItemChecked(menu.GetItemIndex((int)MenuId.Speed1), main.GrowthSpeed == 1);
        menu.SetItemChecked(menu.GetItemIndex((int)MenuId.Speed60), main.GrowthSpeed == 60);
        menu.SetItemChecked(menu.GetItemIndex((int)MenuId.Speed3600), main.GrowthSpeed == 3600);
        for (int i = 0; i < Sizes.Length; i++)
            menu.SetItemChecked(menu.GetItemIndex((int)MenuId.Size0 + i), Mathf.IsEqualApprox(Sizes[i], plantScale));
    }

    // ==== COORDINATES ==== \\

    // Polled rather than taken from events: the window gets no mouse events while clicks are falling through it.
    Vector2 PointerInViewport() => (Vector2)(DisplayServer.MouseGetPosition() - GetWindow().Position);

    Vector2 ToWorld(Vector2 viewportPoint) => GetViewport().GetCanvasTransform().AffineInverse() * viewportPoint;

    Vector2 ToViewport(Num.Vector2 world) => GetViewport().GetCanvasTransform() * GardenView.V(world);

    static Num.Vector2 ToSim(Vector2 v) => new(v.X, v.Y);
}
