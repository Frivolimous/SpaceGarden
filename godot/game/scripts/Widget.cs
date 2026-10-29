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
// Resting the pointer on a part rings it; tapping it (a click without a drag) selects it and opens its toolbar. The
// selection stays (ringed, and shown in the readout's panel) after the toolbar closes, until another part is tapped,
// the part is pruned, or a click lands off the plant. Dragging a part
// pulls it to the pointer by its weight and the links drag the rest after it; letting go keeps the momentum.
// Right-click opens the menu. It drops to a low frame rate when nothing's happening.
//
// Display modes (DESIGN.md, Tending commands): Active, the default, shows it at full opacity with its controls (the
// toolbar, the readout, the menu); Ambient fades the whole window and lets every click fall through; Minimised
// minimises the window (to the taskbar) and stops the garden as if the screen were locked, catching up on return.
// The hotkey (Hotkey.Name) toggles Active and Ambient, and brings a minimised garden back; so does its taskbar
// button or a click on the tray icon, whose right-click menu (the same as the plant's) picks any mode. Minimising
// the window any other way (Win+D, say) counts as Minimised too.
public enum DisplayMode { Active, Ambient, Minimised }

public partial class Widget : Node
{
    // Idle it draws at the Idle frame rate from the menu (30 fps by default); while anything's happening, at the
    // monitor's rate (no cap, vsync holds it there). It counts as settled once no part has moved more than
    // SettledSpeed a tick for SettleSeconds. Idle pacing is Godot's low-processor mode (it sleeps between frames)
    // rather than the max_fps cap, which spends CPU between frames to keep its timing: 4.5% of a core instead of
    // 7-9% for an empty garden at 30 fps. The idle cost is mostly per frame, so it scales with the idle rate.
    public static readonly int[] IdleRates = [15, 20, 30, 60];
    int idleFps = 30;
    bool idling, hidden;
    const int HiddenSleepUsec = 250_000; // 4 wake-ups a second while the garden can't be seen
    const float SettledSpeed = 0.45f; // idle sway peaks at 0.2-0.4 (bigger plants higher); a fling is 0.5+ for 2 s
    const double SettleSeconds = 1;
    const float DragThreshold = 4;
    const float HoverSlop = 5;
    const double LeaveDelay = 1.5;
    const float RegionMargin = 14; // the passthrough region's margin where click-through isn't supported

    public static readonly float[] Sizes = [0.5f, 0.75f, 1f, 1.25f];

    // Ambient mode's opacity, and how long a change of mode takes to fade.
    const float AmbientOpacity = 0.55f;
    const float FadeSeconds = 0.3f;

    enum MenuId
    {
        AlwaysOnTop, Speed1, Speed60, Speed3600, Reset, Quit, FruitBorrows, Build,
        Active = 10, Ambient, Minimised, Size0 = 100, Idle0 = 200,
    }

    public int IdleFps
    {
        get => idleFps;
        set
        {
            idleFps = value;
            OS.LowProcessorUsageModeSleepUsec = 1_000_000 / idleFps; // takes effect at once if idling
            CheckMenu();
        }
    }

    // EXPERIMENT: the cheaper fruit physics (Garden.FruitBorrowsPush), switchable from the menu to compare the feel.
    bool fruitBorrowsPush = true;

    readonly Main main;
    readonly PopupMenu menu = new();
    readonly Toolbar toolbar = new();
    readonly CanvasLayer toolbarLayer = new() { Layer = 10 };
    readonly Readout readout = new();
    readonly StatusIndicator tray = new();
    readonly ShapingBar shapingBar = new();

    public DisplayMode Mode { get; private set; } = DisplayMode.Active;

    // Build mode (DESIGN.md, Shaping the garden), within Active: a dark backdrop round the plant, the shaping bar to
    // place tools, and the tools themselves to take hold of. Dragging a tool's body moves it, its knob or ends turn
    // and stretch it, a tap opens its toolbar (Remove), and dragging one onto the bar puts it away. A part dragged
    // and let go over a pot or trellis is tied to it. Outside build mode the tools show faintly and the pointer
    // passes through them.
    public bool Building { get; private set; }
    const float ShapingFaint = 0.45f; // the tools' opacity while tending, outside build mode
    ShapingTool? movingTool;
    ShapingHandle movingHandle;
    Num.Vector2 moveOffset;
    bool toolTap, fromBar; // the press on it hasn't moved yet (a tap); it was just taken off the bar
    Vector2? barAt; // where the shaping bar was set down (centred above it), this time in build mode
    Vector2 movePointer, moveStart; // where the pointer is, and was when it took the tool (viewport pixels)
    bool moveReleased;
    PlantNode? movingPart; // the potted part pressed to take its pot, if that's how it was taken
    float opacity = 1;
    bool readoutPlaced;

    // The readout's lines fade in over this while the pointer rests on the plant, and out after it leaves.
    const float ReadoutFadeSeconds = 1;

    public float Dpi { get; private set; } = 1;
    float plantScale = 0.75f;

    Vector2I dragStartMouse;
    PlantNode? pressNode;
    PlantNode? selected;
    // Dev aid, "-- --inspect": the readout shows as if the pointer rested on the plant, with a lab (or the core)
    // selected, so a --screenshot run can show both.
    readonly bool inspect = OS.GetCmdlineUserArgs().Contains("--inspect");
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
        toolbarLayer.AddChild(readout);
        readout.UndoPressed += UndoPrune;
        toolbar.CanGrow = (node, kind) => Garden.CanSproutFrom(node, kind);
        toolbarLayer.AddChild(shapingBar);
        shapingBar.Started += TakeFromBar;
        shapingBar.Done += () => SetBuilding(false);
        // A click anywhere off the plant lands in another window, which takes the focus: close the toolbar then.
        window.FocusExited += () =>
        {
            toolbar.HideAway();
            if (!menu.Visible) Select(null);
        };

        menu.AddSeparator("Display");
        menu.AddRadioCheckItem($"Active ({Hotkey.Name})", (int)MenuId.Active);
        menu.AddRadioCheckItem("Ambient", (int)MenuId.Ambient);
        menu.AddRadioCheckItem("Minimised (to the tray)", (int)MenuId.Minimised);
        menu.AddCheckItem("Build mode: arrange the garden (Ctrl+B)", (int)MenuId.Build);
        menu.AddSeparator();
        menu.AddCheckItem("Always on top", (int)MenuId.AlwaysOnTop);
        menu.SetItemChecked(menu.GetItemIndex((int)MenuId.AlwaysOnTop), true);
        menu.AddSeparator("Size");
        for (int i = 0; i < Sizes.Length; i++) menu.AddRadioCheckItem($"{Sizes[i] * 100:0}%", (int)MenuId.Size0 + i);
        menu.AddSeparator("Growth speed");
        menu.AddRadioCheckItem("Real time (1x)", (int)MenuId.Speed1);
        menu.AddRadioCheckItem("Fast (60x)", (int)MenuId.Speed60);
        menu.AddRadioCheckItem("Very fast (3600x)", (int)MenuId.Speed3600);
        menu.AddSeparator("Idle frame rate");
        for (int i = 0; i < IdleRates.Length; i++)
            menu.AddRadioCheckItem($"{IdleRates[i]} fps{(IdleRates[i] == 30 ? " (default)" : "")}", (int)MenuId.Idle0 + i);
        menu.AddSeparator("Physics");
        menu.AddCheckItem("Fruit borrows its parent's push (cheaper)", (int)MenuId.FruitBorrows);
        menu.AddSeparator();
        menu.AddItem("New garden", (int)MenuId.Reset);
        menu.AddItem("Quit", (int)MenuId.Quit);
        menu.IdPressed += OnMenu;
        AddChild(menu);

        tray.Icon = TrayIcon();
        tray.Tooltip = $"Space Garden: click to show or hide, right-click for options. {Hotkey.Name} to tend.";
        tray.Pressed += (button, position) =>
        {
            if (button == (long)MouseButton.Left) SetMode(Mode == DisplayMode.Minimised ? DisplayMode.Active : DisplayMode.Minimised);
            else if (button == (long)MouseButton.Right) menu.Popup(new Rect2I(position, Vector2I.Zero));
        };
        AddChild(tray);
        Hotkey.Register();
        ClickThrough.Opacity(opacity);
    }

    public override void _ExitTree() => Hotkey.Unregister();

    // The core in miniature: a soft yellow disc with a lighter rim, drawn rather than loaded.
    static ImageTexture TrayIcon()
    {
        const int size = 32;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var core = GardenView.Rgb(NodeSpecs.All[NodeKind.Core].Rgb);
        var c = new Vector2(size / 2f - 0.5f, size / 2f - 0.5f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x, y).DistanceTo(c);
                float a = Mathf.Clamp(13.5f - d, 0, 1);
                var colour = d > 11 ? core.Lightened(0.5f) : core;
                image.SetPixel(x, y, colour with { A = a });
            }
        return ImageTexture.CreateFromImage(image);
    }

    // Switches display mode. Leaving Active puts its controls away and lets go of anything held.
    public void SetMode(DisplayMode mode)
    {
        if (mode == Mode) return;
        Mode = mode;
        if (mode != DisplayMode.Active)
        {
            SetBuilding(false);
            toolbar.HideAway();
            menu.Hide();
            if (Garden.Held != null) Garden.LetGo();
            pressed = dragging = false;
            pressNode = null;
        }
        // A real minimise, so the taskbar button brings it back (see FollowWindow).
        var window = GetWindow();
        if (mode == DisplayMode.Minimised) window.Mode = Window.ModeEnum.Minimized;
        else if (window.Mode == Window.ModeEnum.Minimized)
        {
            window.Mode = Window.ModeEnum.Windowed;
            CoverScreen(plantScale);
        }
        readoutPlaced = false;
        readout.TextAlpha = 0;
        readout.RefreshNow();
        activeTime = SettleSeconds; // full frame rate for the fade
        main.CheckVisibilityNow();
        CheckMenu();
    }

    // Into or out of build mode (only from Active). Leaving puts down any tool being carried.
    public void SetBuilding(bool on)
    {
        if (on && Mode != DisplayMode.Active) return;
        if (on == Building) return;
        Building = on;
        barAt = null;
        shapingBar.Appear(on);
        toolbar.HideAway();
        movingTool = null;
        fromBar = false;
        activeTime = SettleSeconds;
        CheckMenu();
    }

    // A tool pressed on the bar: a new one at the pointer, carried until let go (see MoveTool).
    void TakeFromBar(ShapingKind kind)
    {
        var at = ToSim(ToWorld(PointerInViewport()));
        if (Garden.Place(kind, at) is not { } tool) return;
        StartMoving(tool, ShapingHandle.Body, at);
        fromBar = true;
    }

    void StartMoving(ShapingTool tool, ShapingHandle handle, Num.Vector2 at)
    {
        movingTool = tool;
        movingHandle = handle;
        moveOffset = tool.Position - at;
        toolTap = true;
        movePointer = moveStart = PointerInViewport();
        moveReleased = false;
        movingPart = null;
    }

    // A held tool follows the pointer as the window's own mouse events give it, so the button's release and where it
    // happened arrive in order (polling the pointer could see it move on before the release).
    public override void _Input(InputEvent e)
    {
        if (movingTool == null) return;
        if (e is InputEventMouseMotion motion) movePointer = motion.Position;
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } up)
        {
            movePointer = up.Position;
            moveReleased = true;
        }
    }

    // Each frame a tool is held: once the pointer has moved, its body follows, or its knob or end. Let go, a tap
    // opens its toolbar, a new one tapped off the bar is set down just under it, and one dragged onto the bar is put
    // away.
    void MoveTool()
    {
        var tool = movingTool!;
        var at = ToSim(ToWorld(movePointer));
        if (toolTap && movePointer.DistanceTo(moveStart) > DragThreshold * Dpi)
        {
            toolTap = false;
            toolbar.HideAway();
        }
        if (!toolTap)
            switch (movingHandle)
            {
                case ShapingHandle.Body: tool.Position = at + moveOffset; break;
                case ShapingHandle.Turn: tool.TurnTo(at); break;
                default: tool.MoveEnd(movingHandle, at); break;
            }
        if (!moveReleased && Input.IsMouseButtonPressed(MouseButton.Left)) return;

        movingTool = null;
        if (toolTap && fromBar)
            tool.Position = ToSim(ToWorld((shapingBar.Position + new Vector2(shapingBar.Size.X / 2, shapingBar.Size.Y + 40)) * Dpi));
        else if (toolTap && movingPart is { } part)
        {
            Select(part);
            if (toolbar.Target == part) toolbar.HideAway();
            else toolbar.ShowFor(part);
        }
        else if (toolTap)
        {
            if (toolbar.TargetTool == tool) toolbar.HideAway();
            else toolbar.ShowFor(tool);
        }
        else if (shapingBar.Visible && shapingBar.GetRect().Grow(4).HasPoint(movePointer / Dpi)) Garden.Remove(tool);
        fromBar = false;
    }

    // In build mode, a tool's handle under the pointer (a pot's knob, a trellis's end): these come before the parts,
    // as a pot's knob sits where its branch grows.
    (ShapingTool Tool, ShapingHandle Handle)? HandleAt(Num.Vector2 at) =>
        Building && Garden.ShapingAt(at, HoverSlop) is { } hit && hit.Handle != ShapingHandle.Body ? hit : null;

    // The pot or trellis a part dragged in build mode would be tied to if let go here (a well holds nothing).
    ShapingTool? DropTarget(Num.Vector2 at) =>
        Building && Garden.ShapingAt(at, HoverSlop) is { } hit && hit.Tool.Kind != ShapingKind.Well ? hit.Tool : null;

    void UndoPrune()
    {
        if (Garden.UndoPrune() is not { } back) return;
        main.View.Restored(back); // growing back in, but not new: no pops or glints
        toolbar.HideAway();
        readout.RefreshNow();
        activeTime = SettleSeconds;
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
        Garden.FruitBorrowsPush = fruitBorrowsPush; // every frame, so it carries over to a new garden too
        FollowWindow();
        if (Hotkey.TakePress()) SetMode(Mode == DisplayMode.Active ? DisplayMode.Ambient : DisplayMode.Active);
        main.View.Visible = Mode != DisplayMode.Minimised; // every frame: a new garden gets a new view
        main.View.Building = Building;
        main.View.ToolsWanted = Mode != DisplayMode.Active ? 0 : Building ? 1 : ShapingFaint;

        // While nobody can see the garden (Main.HiddenReason, also while Minimised): wake only a few times a second,
        // to notice it's back.
        if (main.HiddenReason != null)
        {
            readout.Visible = false;
            if (!hidden)
            {
                hidden = true;
                toolbar.HideAway();
                pressed = dragging = false;
                pressNode = null;
                main.View.Highlight = null;
                if (ClickThrough.Supported) ClickThrough.Set(true);
            }
            OS.LowProcessorUsageModeSleepUsec = HiddenSleepUsec;
            OS.LowProcessorUsageMode = true;
            return;
        }
        if (hidden)
        {
            hidden = false;
            idling = false; // back at full rate for a moment, then idle as usual
            OS.LowProcessorUsageMode = false;
            activeTime = SettleSeconds;
        }

        float wanted = Mode == DisplayMode.Active ? 1 : AmbientOpacity;
        bool fading = opacity != wanted;
        opacity = Mathf.MoveToward(opacity, wanted, (float)delta / FadeSeconds);
        if (ClickThrough.Supported) ClickThrough.Opacity(opacity);
        else main.View.Modulate = new Color(1, 1, 1, opacity);

        // Ambient: nothing to point at, every click falls through.
        if (Mode != DisplayMode.Active)
        {
            readout.Visible = false;
            main.View.Highlight = null;
            if (ClickThrough.Supported) ClickThrough.Set(true);
            else UpdateRegion(delta, passAll: true);
            activeTime = fading || Garden.Speed > SettledSpeed ? SettleSeconds : activeTime - delta;
            SetIdle(activeTime <= 0);
            return;
        }

        if (selected != null && !Garden.Nodes.Contains(selected)) Select(null); // pruned, or a new garden
        if (inspect && selected == null) Select(Garden.Nodes.FirstOrDefault(n => n.Spec.Kind == NodeKind.Lab) ?? Garden.Core);
        var pointer = PointerInViewport();
        var world = ToSim(ToWorld(pointer));
        bool overToolbar = toolbar.Visible && toolbar.GetRect().Grow(4).HasPoint(pointer / Dpi);
        bool overReadout = readout.Catches(pointer / Dpi);
        bool overBar = shapingBar.Visible && shapingBar.GetRect().Grow(4).HasPoint(pointer / Dpi);
        bool inWindow = GetViewport().GetVisibleRect().HasPoint(pointer);
        bool free = inWindow && !overToolbar && !overReadout && !overBar;
        var handle = free ? HandleAt(world) : null;
        var under = free && handle == null ? Garden.NodeAt(world, HoverSlop) : null;
        var underTool = handle ?? (Building && free && under == null ? Garden.ShapingAt(world, HoverSlop) : null);
        bool onBackdrop = Building && inWindow && main.View.OnBackdrop(world);
        if (Building)
        {
            shapingBar.Refresh(Garden);
            // Set down over the backdrop as build mode opens, then still (a bar that glides is hard to hit).
            if (main.View.BackdropShown) barAt ??= ToViewport(main.View.BackdropTop) / Dpi;
            if (barAt is { } bar) shapingBar.Follow(bar, GetViewport().GetVisibleRect().Size / Dpi);
        }
        if (movingTool != null) MoveTool();
        bool resting = inspect || under != null || pressed || overToolbar || readout.Over(pointer / Dpi);
        float textWanted = resting ? 1 : 0;
        bool textFading = readout.TextAlpha != textWanted;
        readout.TextAlpha = Mathf.MoveToward(readout.TextAlpha, textWanted, (float)delta / ReadoutFadeSeconds);
        fading |= textFading;
        PlaceReadout(delta);

        if (pressed && !Input.IsMouseButtonPressed(MouseButton.Left)) Release();

        if (dragging) Garden.HoldTarget = world;
        else UpdateToolbar(delta, toolbar.Target != null ? under == toolbar.Target : underTool?.Tool == toolbar.TargetTool, overToolbar);

        var viewport = GetViewport().GetVisibleRect().Size / Dpi;
        if (toolbar.Target is { } target)
        {
            if (!Garden.Nodes.Contains(target)) toolbar.HideAway();
            else
            {
                toolbar.Follow(ToViewport(target.Position) / Dpi, target.Spec.Radius * plantScale, viewport);
                toolbar.Refresh();
            }
        }
        else if (toolbar.TargetTool is { } tool)
        {
            if (!Garden.Shaping.Contains(tool) || !Building) toolbar.HideAway();
            else toolbar.Follow(ToViewport(tool.Position) / Dpi, 20 * plantScale, viewport);
        }
        main.View.Highlight = dragging || pressed ? pressNode : under ?? toolbar.Target ?? selected;
        main.View.HotTool = dragging ? DropTarget(world) : movingTool ?? underTool?.Tool ?? toolbar.TargetTool;

        bool catchMouse = pressed || overToolbar || overReadout || under != null || overBar || onBackdrop
            || underTool != null || movingTool != null;
        if (ClickThrough.Supported) ClickThrough.Set(!catchMouse);
        else UpdateRegion(delta);
        bool active = catchMouse || fading || toolbar.Visible || Garden.Held != null || Garden.Speed > SettledSpeed;
        activeTime = active ? SettleSeconds : activeTime - delta;
        SetIdle(activeTime <= 0);
    }

    // The window minimised or brought back from outside (its taskbar button, Win+D) changes the mode to match.
    void FollowWindow()
    {
        bool minimised = GetWindow().Mode == Window.ModeEnum.Minimized;
        if (minimised && Mode != DisplayMode.Minimised) SetMode(DisplayMode.Minimised);
        else if (!minimised && Mode == DisplayMode.Minimised) SetMode(DisplayMode.Active);
    }

    // The readout sits centred under the plant (over it if there's no room below), kept on the screen, and glides
    // after the plant as it grows and sways rather than jittering with it.
    void PlaceReadout(double delta)
    {
        readout.Visible = true;
        readout.Update(Garden, selected, main.GrowthSpeed, delta);
        var (min, max) = Garden.Bounds();
        var screen = GetViewport().GetVisibleRect().Size / Dpi;
        var size = readout.GetCombinedMinimumSize();
        var below = ToViewport(new Num.Vector2(Garden.Core.Position.X, max.Y)) / Dpi + new Vector2(-size.X / 2, 14);
        if (below.Y + size.Y > screen.Y - 4) below.Y = ToViewport(min).Y / Dpi - 14 - size.Y;
        var target = below.Clamp(new Vector2(4, 4), (screen - size - new Vector2(4, 4)).Max(new Vector2(4, 4)));
        readout.Position = readoutPlaced ? readout.Position.Lerp(target, 1 - Mathf.Exp(-6 * (float)delta)) : target;
        readout.Size = size;
        readoutPlaced = true;
    }

    // A tapped part's toolbar goes once the pointer has been off both the part and the toolbar for a while (see
    // Release and _Ready for the other ways it closes).
    void UpdateToolbar(double delta, bool overTarget, bool overToolbar)
    {
        if ((toolbar.Target != null || toolbar.TargetTool != null) && !overTarget && !overToolbar)
        {
            leaveTime += delta;
            if (leaveTime >= LeaveDelay) toolbar.HideAway();
        }
        else leaveTime = 0;
    }

    // Fallback for other platforms: a passthrough region around the plant. (On Windows nothing outside it would be
    // drawn, hence ClickThrough there.)
    void UpdateRegion(double delta, bool passAll = false)
    {
        regionTimer -= delta;
        if (regionTimer > 0) return;
        regionTimer = 0.25;
        if (passAll)
        {
            DisplayServer.WindowSetMousePassthrough([Vector2.Zero, Vector2.Right, Vector2.One]); // a speck
            return;
        }
        var (min, max) = Garden.Bounds();
        foreach (var tool in Building ? Garden.Shaping : [])
        {
            min = Num.Vector2.Min(min, tool.Position - new Num.Vector2(60));
            max = Num.Vector2.Max(max, tool.Position + new Num.Vector2(60));
        }
        var toWindow = GetViewport().GetCanvasTransform();
        var a = toWindow * (GardenView.V(min) - new Vector2(RegionMargin, RegionMargin));
        var b = toWindow * (GardenView.V(max) + new Vector2(RegionMargin, RegionMargin));
        DisplayServer.WindowSetMousePassthrough([a, new Vector2(b.X, a.Y), b, new Vector2(a.X, b.Y)]);
    }

    void SetIdle(bool idle)
    {
        if (idle == idling) return;
        idling = idle;
        OS.LowProcessorUsageModeSleepUsec = 1_000_000 / idleFps;
        OS.LowProcessorUsageMode = idle;
    }

    // ==== INPUT ==== \\

    // Unhandled, so the toolbar's buttons get their clicks first.
    public override void _UnhandledInput(InputEvent e)
    {
        // Keys reach the window only once it has the focus (a click on the plant in Active mode). Off Windows there's
        // no system-wide hotkey, so the same keys toggle the mode here.
        if (e is InputEventKey { Pressed: true, Echo: false, CtrlPressed: true } key)
        {
            if (key.Keycode == Key.Z) UndoPrune();
            else if (key.Keycode == Key.B) SetBuilding(!Building);
            else if (key.Keycode == Key.G && key.AltPressed && !Hotkey.Registered)
                SetMode(Mode == DisplayMode.Active ? DisplayMode.Ambient : DisplayMode.Active);
            else return;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (e is InputEventKey { Pressed: true, Keycode: Key.Escape } && Building)
        {
            SetBuilding(false);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (Mode != DisplayMode.Active) return;
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
        {
            if (button.Pressed)
            {
                var at = ToSim(ToWorld(button.Position));
                var handle = HandleAt(at);
                pressNode = handle == null ? Garden.NodeAt(at, HoverSlop) : null;
                // A potted part is locked: in build mode, taking it takes its pot (a tap still opens the part's toolbar).
                if (Building && pressNode?.TiedTo is { Kind: ShapingKind.Pot } held)
                {
                    StartMoving(held, ShapingHandle.Body, at);
                    movingPart = pressNode;
                    pressNode = null;
                    GetViewport().SetInputAsHandled();
                    return;
                }
                if (pressNode == null)
                {
                    if ((handle ?? (Building ? Garden.ShapingAt(at, HoverSlop) : null)) is { } hit)
                    {
                        StartMoving(hit.Tool, hit.Handle, at);
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                    Select(null); // a click off the plant (where clicks fall through, losing the focus does this)
                    if (Building)
                    {
                        toolbar.HideAway();
                        GetViewport().SetInputAsHandled();
                    }
                    return;
                }
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

    // A tap (a press without a drag) selects the part and opens its toolbar, or closes it if it was already open.
    void Release()
    {
        if (!dragging && pressNode != null && Garden.Nodes.Contains(pressNode))
        {
            Select(pressNode);
            if (toolbar.Target == pressNode) toolbar.HideAway();
            else toolbar.ShowFor(pressNode);
        }
        // In build mode, a part let go over a pot or trellis is tied to it.
        if (dragging && pressNode != null && DropTarget(ToSim(ToWorld(PointerInViewport()))) is { } tool)
            Garden.Tie(pressNode, tool);
        if (Garden.Held != null) Garden.LetGo();
        pressed = dragging = false;
        pressNode = null;
    }

    void Select(PlantNode? node)
    {
        if (node == selected) return;
        selected = node;
        readout.RefreshNow();
    }

    // Picking a tool does its job and closes the toolbar.
    void Use(Tool tool)
    {
        if (toolbar.TargetTool is { } shaping)
        {
            if (tool == Tool.Remove) Garden.Remove(shaping);
            toolbar.HideAway();
            return;
        }
        if (toolbar.Target is not { } node) return;
        if (Toolbar.KindOf(tool) is { } kind) Garden.SproutFrom(node, kind);
        switch (tool)
        {
            case Tool.Pin: Garden.TogglePin(node); break;
            case Tool.Unhook: Garden.Untie(node); break;
            case Tool.Prune:
                Garden.Prune(node); // it drifts off and fades (see Garden.Cut)
                readout.RefreshNow(); // the Undo pill, at once
                break;
        }
        toolbar.HideAway();
    }

    void OnMenu(long id)
    {
        switch ((MenuId)id)
        {
            case MenuId.Active: SetMode(DisplayMode.Active); break;
            case MenuId.Ambient: SetMode(DisplayMode.Ambient); break;
            case MenuId.Minimised: SetMode(DisplayMode.Minimised); break;
            case MenuId.Build:
                SetMode(DisplayMode.Active);
                SetBuilding(!Building);
                break;
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
            case MenuId.FruitBorrows: fruitBorrowsPush = !fruitBorrowsPush; break;
            case >= MenuId.Idle0: IdleFps = IdleRates[(int)id - (int)MenuId.Idle0]; break;
            case >= MenuId.Size0: PlantScale = Sizes[(int)id - (int)MenuId.Size0]; break;
        }
        CheckMenu();
    }

    public void CheckMenu()
    {
        menu.SetItemChecked(menu.GetItemIndex((int)MenuId.Active), Mode == DisplayMode.Active);
        menu.SetItemChecked(menu.GetItemIndex((int)MenuId.Ambient), Mode == DisplayMode.Ambient);
        menu.SetItemChecked(menu.GetItemIndex((int)MenuId.Minimised), Mode == DisplayMode.Minimised);
        menu.SetItemChecked(menu.GetItemIndex((int)MenuId.Build), Building);
        menu.SetItemChecked(menu.GetItemIndex((int)MenuId.Speed1), main.GrowthSpeed == 1);
        menu.SetItemChecked(menu.GetItemIndex((int)MenuId.Speed60), main.GrowthSpeed == 60);
        menu.SetItemChecked(menu.GetItemIndex((int)MenuId.Speed3600), main.GrowthSpeed == 3600);
        menu.SetItemChecked(menu.GetItemIndex((int)MenuId.FruitBorrows), fruitBorrowsPush);
        for (int i = 0; i < Sizes.Length; i++)
            menu.SetItemChecked(menu.GetItemIndex((int)MenuId.Size0 + i), Mathf.IsEqualApprox(Sizes[i], plantScale));
        for (int i = 0; i < IdleRates.Length; i++)
            menu.SetItemChecked(menu.GetItemIndex((int)MenuId.Idle0 + i), IdleRates[i] == idleFps);
    }

    // ==== COORDINATES ==== \\

    // Polled rather than taken from events: the window gets no mouse events while clicks are falling through it.
    Vector2 PointerInViewport() => (Vector2)(DisplayServer.MouseGetPosition() - GetWindow().Position);

    Vector2 ToWorld(Vector2 viewportPoint) => GetViewport().GetCanvasTransform().AffineInverse() * viewportPoint;

    Vector2 ToViewport(Num.Vector2 world) => GetViewport().GetCanvasTransform() * GardenView.V(world);

    static Num.Vector2 ToSim(Vector2 v) => new(v.X, v.Y);
}
