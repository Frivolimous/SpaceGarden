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
// Resting the pointer on a part rings it, and nothing more. Tapping it (a click without a drag) selects it, opens its
// toolbar and shows its card. The selection stays (ringed, its card open) after the toolbar closes, until the part is
// tapped again, another is tapped, it's pruned, a click lands off the plant, or Escape. Tapping the core also opens
// its menu (CoreMenu) under it, with its toolbar (its buds) above it: Info and Build switch the garden's card (shown
// above the part's while tending) and build mode on and off, Almanac and Research open or close the journal's window
// and the upgrade tracks' panel, and the rest pick Ambient, Minimised or the settings.
// Dragging a part pulls it to the pointer by its weight and the links drag the rest after it; letting go keeps the
// momentum. Right-click opens the settings. It drops to a low frame rate when nothing's happening.
//
// Display modes (DESIGN.md, Tending commands): Active, the default, shows it at full opacity with its controls (the
// toolbar, the card, the core's menu); Ambient fades the whole window and lets every click fall through but on the
// core, which brightens under the pointer and wakes the garden (Active, with the core's menu open) on a tap; Minimised
// minimises the window (to the taskbar) and stops the garden as if the screen were locked, catching up on return.
// The hotkey (Hotkey.Name) toggles Active and Ambient, and brings a minimised garden back; so does its taskbar
// button or a click on the tray icon (whose right-click menu is the settings). Minimising the window any other way
// (Win+D, say) counts as Minimised too.
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

    // How long a change of mode takes to fade.
    const float FadeSeconds = 0.3f;

    // Ambient mode's own settings (Jeremy, Nov 3; the Settings menu's Ambient submenu, saved): its opacity, its size
    // as a share of the garden's Size, and its idle frame rate.
    public static readonly float[] AmbientOpacities = [0.25f, 0.4f, 0.55f, 0.7f, 0.85f, 1f];
    public static readonly float[] AmbientScales = [0.5f, 0.75f, 1f];
    public float AmbientOpacity { get; private set; } = 0.55f;
    public float AmbientScale { get; private set; } = 1;
    public int AmbientIdleFps { get; private set; } = 30;

    // Sets them (any not among the menu's choices is left as it was), applying the size at once if in Ambient.
    public void SetAmbient(float opacity, float scale, int idleFps)
    {
        if (AmbientOpacities.Any(o => Mathf.IsEqualApprox(o, opacity))) AmbientOpacity = opacity;
        if (IdleRates.Contains(idleFps)) AmbientIdleFps = idleFps;
        if (AmbientScales.Any(s => Mathf.IsEqualApprox(s, scale))) Rescale(() => AmbientScale = scale);
        if (idling) OS.LowProcessorUsageModeSleepUsec = 1_000_000 / IdleRate;
        CheckMenu();
    }

    // The idle frame rate now: Ambient's own while in it.
    int IdleRate => Mode == DisplayMode.Ambient ? AmbientIdleFps : idleFps;

    // The settings (right-click, the core menu's Settings, the tray icon's right-click), and in debug builds their
    // Developer submenu.
    enum MenuId
    {
        AlwaysOnTop, Speed1, Speed60, Speed3600, Reset, Quit, FruitBorrows, Export, Size0 = 100, Idle0 = 200,
        AmbientOpacity0 = 300, AmbientScale0 = 400, AmbientIdle0 = 500,
    }

    public int IdleFps
    {
        get => idleFps;
        set
        {
            idleFps = value;
            OS.LowProcessorUsageModeSleepUsec = 1_000_000 / IdleRate; // takes effect at once if idling
            CheckMenu();
        }
    }

    // EXPERIMENT: the cheaper fruit physics (Garden.FruitBorrowsPush), switchable from the menu to compare the feel.
    bool fruitBorrowsPush = true;

    readonly Main main;
    readonly PopupMenu menu = new();
    readonly PopupMenu devMenu = new();
    readonly PopupMenu ambientMenu = new();
    readonly ConfirmationDialog confirmNew = new()
    {
        Title = "New garden",
        DialogText = "Start a new garden? This one can't be brought back.",
        OkButtonText = "Start a new garden",
    };
    // Cancelling or changing a forming bud asks every time (Jeremy, Nov 3: its growth is lost): a pill over the
    // toolbar, what it would do and Confirm. It goes with the toolbar, or when another tool is picked.
    readonly PanelContainer askPill = new() { MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
    readonly Label askText = new() { MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
    Action? askAction;
    PlantNode? askFor;
    readonly Toolbar toolbar = new();
    readonly CanvasLayer toolbarLayer = new() { Layer = 10 };
    readonly Card partCard = new(), gardenCard = new();
    readonly VBoxContainer cards = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
    // The cards share one panel (Jeremy, Nov 3), dragged anywhere on it; once moved it stays where it was left.
    readonly PanelContainer info = new() { MouseFilter = Control.MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.Move };
    readonly ColorRect infoDivider = new() { Color = new Color(1, 1, 1, 0.12f), CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore };
    Vector2? infoGrab; // while held: where on the panel the pointer took it
    // Build mode's Move garden handle: while it's held, the whole garden follows the pointer (gardenGrab: where the
    // pointer was, in garden units, updated as it moves).
    Num.Vector2? gardenGrab;
    bool infoMoved;
    bool gardenInfo; // the core menu's Info is on: the garden's card shows above the part's (see ShowGardenCard)
    readonly Almanac almanac = new(); // its own window, open until its slot or ✕ closes it (see SetAlmanac)
    readonly ResearchPanel research = new(); // its own window too (see SetResearch)
    readonly HubPanel hubPanel = new(); // the Hub's, opened from its toolbar (see Use)
    readonly UndoPill undo = new();
    readonly CoreMenu coreMenu = new();
    readonly EvolutionMenu evolutionMenu = new(); // a seedling's choice (see Tap)
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
    bool undoPlaced;
    // Where the card was set down as it opened (its top corner on the side it's on), kept while it's open.
    Vector2? cardAnchor;
    bool cardOnLeft;

    public float Dpi { get; private set; } = 1;
    float plantScale = 0.75f;

    Vector2I dragStartMouse;
    PlantNode? pressNode;
    PlantNode? selected;
    bool selectedWasChoosing; // the selected part was a seedling waiting for its choice, last frame
    // Dev aids for --screenshot runs (see DevFlags): open a card, the core's menu, Ambient with the pointer on the core.
    bool devFlagsDone, devCoreHover, devTapped;
    bool pressed, dragging;
    double leaveTime, regionTimer, activeTime;

    public Widget(Main main) => this.main = main;

    Garden Garden => main.Garden;

    // Screen pixels per garden unit: the Size, times Ambient's own share of it while in Ambient.
    float Scale => Dpi * ViewScale;
    float ViewScale => plantScale * (Mode == DisplayMode.Ambient ? AmbientScale : 1);

    public float PlantScale
    {
        get => plantScale;
        set
        {
            Rescale(() => plantScale = value);
            CheckMenu();
        }
    }

    // Changes the scale (by change), keeping things' distance from the screen's edges (Jeremy, Nov 3): the plant grows
    // or shrinks from the corner of its outline nearest the screen's nearest corner, which stays put, so a plant in
    // the bottom right keeps its gap to the taskbar and the right edge rather than shrinking towards the middle.
    void Rescale(Action change)
    {
        var (min, max) = Garden.Bounds();
        var screen = (Vector2)GetWindow().Size;
        var middle = GardenView.V((min + max) / 2) * Scale;
        // That corner of the outline, on the screen.
        var corner = new Num.Vector2(middle.X > screen.X / 2 ? max.X : min.X, middle.Y > screen.Y / 2 ? max.Y : min.Y);
        var cornerOnScreen = GardenView.V(corner) * Scale;
        // The shaping tools stay where they are on the screen, each keeping its own distance from the edges (a trellis
        // in the top right isn't carried across the screen by a plant in the bottom right); what's tied to them follows.
        var tools = Garden.Shaping.Select(t => (Tool: t, OnScreen: GardenView.V(t.Position) * Scale)).ToList();
        change();
        Garden.Translate(ToSim(cornerOnScreen / Scale) - corner);
        foreach (var (tool, onScreen) in tools) tool.Position = ToSim(onScreen / Scale);
        ApplyTransform();
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
        toolbarLayer.AddChild(undo);
        undo.Pressed += UndoPrune;
        cards.AddThemeConstantOverride("separation", 0);
        gardenCard.Inline();
        partCard.Inline();
        cards.AddChild(gardenCard);
        cards.AddChild(infoDivider);
        cards.AddChild(partCard);
        var infoStyle = Toolbar.PillStyle();
        infoStyle.SetCornerRadiusAll(10);
        infoStyle.ContentMarginLeft = infoStyle.ContentMarginRight = infoStyle.ContentMarginTop = infoStyle.ContentMarginBottom = 0;
        info.AddThemeStyleboxOverride("panel", infoStyle);
        info.TooltipText = "";
        info.AddChild(cards);
        info.Visible = false;
        info.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press) return;
            infoGrab = press.Position;
            info.AcceptEvent();
        };
        toolbarLayer.AddChild(info);
        toolbarLayer.AddChild(almanac);
        toolbarLayer.AddChild(research);
        toolbarLayer.AddChild(hubPanel);
        toolbarLayer.AddChild(coreMenu); // over the windows, so its names show over them
        almanac.CloseRequested += () => SetAlmanac(false);
        research.CloseRequested += () => SetResearch(false);
        research.BuyPressed += Buy;
        hubPanel.CloseRequested += () => hubPanel.Close();
        hubPanel.BuyPressed += BuyHubLevel;
        hubPanel.RespecPressed += () =>
        {
            if (!Garden.RespecHub()) return;
            hubPanel.RefreshNow();
            partCard.RefreshNow();
            gardenCard.RefreshNow();
        };
        almanac.RespecPressed += evolution =>
        {
            if (!Garden.Respec(evolution)) return;
            partCard.RefreshNow();
            gardenCard.RefreshNow();
        };
        coreMenu.Picked += PickFromCoreMenu;
        toolbarLayer.AddChild(evolutionMenu);
        evolutionMenu.Picked += ChooseEvolution;
        toolbar.CanGrow = (node, kind) => Garden.CanSproutFrom(node, kind);
        toolbar.CanChange = (node, kind) => Garden.CanChangeBud(node, kind);
        toolbar.HubPanelOpen = () => hubPanel.IsOpen;
        toolbar.Offered = kind => kind switch
        {
            NodeKind.Seedling => Garden.FreeSeedlingSlots > 0,
            NodeKind.Hub => Garden.CanPlantHub,
            _ => true,
        };
        toolbarLayer.AddChild(shapingBar);
        shapingBar.Started += TakeFromBar;
        shapingBar.Done += () => SetBuilding(false);
        shapingBar.MoveStarted += () => gardenGrab = ToSim(ToWorld(PointerInViewport()));
        shapingBar.HeatPicked += view =>
        {
            main.View.Heat = view;
            Garden.TrackTraffic = view == HeatView.Blobs;
        };
        // A click anywhere off the plant lands in another window, which takes the focus: close the toolbar, the
        // core's menu and the card then (but not for the garden's own settings or dialog).
        window.FocusExited += () =>
        {
            if (menu.Visible || devMenu.Visible || confirmNew.Visible) return;
            toolbar.HideAway();
            coreMenu.Close();
            evolutionMenu.Close();
            Select(null);
        };

        menu.AddSeparator("Size");
        for (int i = 0; i < Sizes.Length; i++) menu.AddRadioCheckItem($"{Sizes[i] * 100:0}%", (int)MenuId.Size0 + i);
        menu.AddSeparator("Idle frame rate");
        for (int i = 0; i < IdleRates.Length; i++)
            menu.AddRadioCheckItem($"{IdleRates[i]} fps{(IdleRates[i] == 30 ? " (default)" : "")}", (int)MenuId.Idle0 + i);
        // Ambient mode's own opacity, size (a share of Size) and idle frame rate.
        ambientMenu.AddSeparator("Opacity");
        for (int i = 0; i < AmbientOpacities.Length; i++)
            ambientMenu.AddRadioCheckItem($"{AmbientOpacities[i] * 100:0}%{(i == 2 ? " (default)" : "")}", (int)MenuId.AmbientOpacity0 + i);
        ambientMenu.AddSeparator("Size");
        for (int i = 0; i < AmbientScales.Length; i++)
            ambientMenu.AddRadioCheckItem($"{AmbientScales[i] * 100:0}% of Size{(AmbientScales[i] == 1 ? " (default)" : "")}", (int)MenuId.AmbientScale0 + i);
        ambientMenu.AddSeparator("Idle frame rate");
        for (int i = 0; i < IdleRates.Length; i++)
            ambientMenu.AddRadioCheckItem($"{IdleRates[i]} fps{(IdleRates[i] == 30 ? " (default)" : "")}", (int)MenuId.AmbientIdle0 + i);
        ambientMenu.IdPressed += OnMenu;
        menu.AddSeparator();
        menu.AddSubmenuNodeItem("Ambient", ambientMenu);
        menu.AddSeparator();
        menu.AddItem("New garden...", (int)MenuId.Reset);
        // Developer settings, only in debug builds (running from the project, not an export).
        if (OS.IsDebugBuild())
        {
            devMenu.AddCheckItem("Always on top", (int)MenuId.AlwaysOnTop);
            devMenu.AddSeparator("Growth speed");
            devMenu.AddRadioCheckItem("Real time (1x)", (int)MenuId.Speed1);
            devMenu.AddRadioCheckItem("Fast (60x)", (int)MenuId.Speed60);
            devMenu.AddRadioCheckItem("Very fast (3600x)", (int)MenuId.Speed3600);
            devMenu.AddSeparator("Physics");
            devMenu.AddCheckItem("Fruit borrows its parent's push (cheaper)", (int)MenuId.FruitBorrows);
            devMenu.AddSeparator("Tracking");
            devMenu.AddItem("Export history and totals (CSV)", (int)MenuId.Export);
            devMenu.IdPressed += OnMenu;
            menu.AddSubmenuNodeItem("Developer", devMenu);
        }
        menu.AddItem("Quit", (int)MenuId.Quit);
        menu.IdPressed += OnMenu;
        AddChild(menu);

        confirmNew.Confirmed += () =>
        {
            toolbar.HideAway();
            coreMenu.Close();
            evolutionMenu.Close();
            Select(null);
            main.NewGarden();
        };
        AddChild(confirmNew);
        var askStyle = Toolbar.PillStyle();
        askStyle.ContentMarginLeft = 12;
        askStyle.ContentMarginRight = 4;
        askStyle.ContentMarginTop = askStyle.ContentMarginBottom = 3;
        askPill.AddThemeStyleboxOverride("panel", askStyle);
        var askRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        askRow.AddThemeConstantOverride("separation", 8);
        askText.LabelSettings = new LabelSettings { FontSize = 12 };
        askRow.AddChild(askText);
        var confirm = new Button { Text = "Confirm", FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        var confirmStyle = new StyleBoxFlat { BgColor = new Color(1f, 0.45f, 0.4f, 0.9f), ContentMarginLeft = 10, ContentMarginRight = 10 };
        confirmStyle.SetCornerRadiusAll(10);
        confirm.AddThemeStyleboxOverride("normal", confirmStyle);
        var confirmHover = (StyleBoxFlat)confirmStyle.Duplicate();
        confirmHover.BgColor = new Color(1f, 0.6f, 0.55f);
        confirm.AddThemeStyleboxOverride("hover", confirmHover);
        confirm.AddThemeStyleboxOverride("pressed", confirmHover);
        confirm.AddThemeFontSizeOverride("font_size", 12);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color" })
            confirm.AddThemeColorOverride(state, new Color(0.06f, 0.07f, 0.09f));
        confirm.Pressed += () =>
        {
            var act = askAction;
            Unask();
            act?.Invoke();
            partCard.RefreshNow();
            gardenCard.RefreshNow();
            toolbar.HideAway();
        };
        askRow.AddChild(confirm);
        askPill.AddChild(askRow);
        toolbarLayer.AddChild(askPill);

        tray.Icon = TrayIcon();
        tray.Tooltip = $"Space Garden: click to show or hide, right-click for settings. {Hotkey.Name} to tend.";
        tray.Pressed += (button, position) =>
        {
            if (button == (long)MouseButton.Left) SetMode(Mode == DisplayMode.Minimised ? DisplayMode.Active : DisplayMode.Minimised);
            else if (button == (long)MouseButton.Right) OpenSettings(position);
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
        Rescale(() => Mode = mode); // (Ambient has a size of its own)
        if (idling) OS.LowProcessorUsageModeSleepUsec = 1_000_000 / IdleRate; // (and an idle rate)
        almanac.SetAway(mode != DisplayMode.Active); // only the core takes clicks outside Active
        research.SetAway(mode != DisplayMode.Active);
        hubPanel.SetAway(mode != DisplayMode.Active);
        if (mode != DisplayMode.Active)
        {
            SetBuilding(false);
            toolbar.HideAway();
            coreMenu.Close();
            evolutionMenu.Close();
            Select(null);
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
        undoPlaced = false;
        undo.RefreshNow();
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
        if (!on)
        {
            gardenGrab = null;
            main.View.Heat = HeatView.None;
            Garden.TrackTraffic = false;
        }
        if (toolbar.TargetTool != null) toolbar.HideAway(); // a part's (the core's, under its menu) stays
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
    // opens its toolbar, a new one tapped off the bar is set down just under it, one dragged onto the bar is put
    // away, and an empty pot dropped on a part plants it.
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
        else if (toolTap && movingPart is { } part) Tap(part);
        else if (toolTap)
        {
            if (toolbar.TargetTool == tool) toolbar.HideAway();
            else toolbar.ShowFor(tool);
        }
        else if (shapingBar.Visible && shapingBar.GetRect().Grow(4).HasPoint(movePointer / Dpi)) Garden.Remove(tool);
        else if (movingHandle == ShapingHandle.Body) Garden.PlantUnder(tool, HoverSlop); // an empty pot dropped on a part plants it
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
        if (!Tuning.UndoPruneOn || Garden.UndoPrune() is not { } back) return;
        main.View.Restored(back); // growing back in, but not new: no pops or glints
        toolbar.HideAway();
        undo.RefreshNow();
        partCard.RefreshNow();
        gardenCard.RefreshNow();
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
        menu.ContentScaleFactor = devMenu.ContentScaleFactor = ambientMenu.ContentScaleFactor = confirmNew.ContentScaleFactor = Dpi;
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
        // Every frame (a new garden gets a new view). While nobody can see the garden (a full-screen app over it, too)
        // nothing of it shows: it's hidden, not left frozen on top (Jeremy, Nov 3).
        main.View.Visible = Mode != DisplayMode.Minimised && main.HiddenReason == null;
        toolbarLayer.Visible = main.HiddenReason == null;
        main.View.Building = Building;
        main.View.ToolsWanted = Mode != DisplayMode.Active ? 0 : Building ? 1 : ShapingFaint;

        // While nobody can see the garden (Main.HiddenReason, also while Minimised): wake only a few times a second,
        // to notice it's back.
        if (main.HiddenReason != null)
        {
            undo.Visible = false;
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

        if (!devFlagsDone) DevFlags();

        var pointer = PointerInViewport();
        var world = ToSim(ToWorld(pointer));
        bool inWindow = GetViewport().GetVisibleRect().HasPoint(pointer);

        // Ambient: every click falls through but on the core, which brightens and is ringed under the pointer (a tap
        // wakes the garden: see _UnhandledInput).
        if (Mode != DisplayMode.Active)
        {
            undo.Visible = false;
            info.Visible = false; // (the cards' panel is Active's only)
            bool overCore = devCoreHover || (inWindow && Garden.NodeAt(world, HoverSlop) == Garden.Core);
            main.View.CoreHover = overCore;
            main.View.Highlight = overCore ? Garden.Core : null; // ringed, as a part is in Active
            if (ClickThrough.Supported) ClickThrough.Set(!overCore);
            else UpdateRegion(delta, coreOnly: true);
            activeTime = fading || overCore || Garden.Speed > SettledSpeed || main.View.Moving ? SettleSeconds : activeTime - delta;
            SetIdle(activeTime <= 0);
            return;
        }
        main.View.CoreHover = false;

        if (selected != null && !Garden.Nodes.Contains(selected)) Select(null); // pruned, or a new garden
        // (The ask pill goes with the toolbar it's over: gone if the toolbar closed or moved to another part.)
        if (askPill.Visible && (!toolbar.Visible || toolbar.Target != askFor)) Unask();
        bool overToolbar = toolbar.Visible && (toolbar.GetRect().Grow(4).HasPoint(pointer / Dpi)
            || (askPill.Visible && askPill.GetRect().Grow(4).HasPoint(pointer / Dpi)));
        bool overUndo = undo.Catches(pointer / Dpi);
        bool overMenu = coreMenu.Over(pointer / Dpi) || evolutionMenu.Over(pointer / Dpi);
        bool overBar = shapingBar.Visible && shapingBar.GetRect().Grow(4).HasPoint(pointer / Dpi);
        bool overAlmanac = almanac.Catches(pointer / Dpi);
        bool overResearch = research.Catches(pointer / Dpi);
        bool overHub = hubPanel.Catches(pointer / Dpi);
        bool overInfo = info.Visible && info.GetGlobalRect().HasPoint(pointer / Dpi);
        bool free = inWindow && !overToolbar && !overUndo && !overBar && !overMenu && !overAlmanac && !overResearch && !overHub
            && !overInfo;
        var handle = free ? HandleAt(world) : null;
        var under = free && handle == null ? Garden.NodeAt(world, HoverSlop) : null;
        var underTool = handle ?? (Building && free && under == null ? Garden.ShapingAt(world, HoverSlop) : null);
        bool onBackdrop = Building && inWindow && main.View.OnBackdrop(world);
        MoveGarden();
        var viewport = GetViewport().GetVisibleRect().Size / Dpi;
        if (Building)
        {
            shapingBar.Refresh(Garden);
            // Set down over the backdrop as build mode opens, then still (a bar that glides is hard to hit).
            if (main.View.BackdropShown) barAt ??= ToViewport(main.View.BackdropTop) / Dpi;
            if (barAt is { } bar) shapingBar.Follow(bar, viewport);
        }
        if (movingTool != null) MoveTool();
        PlaceUndo(delta);
        partCard.Update(Garden, main.GrowthSpeed, delta);
        gardenCard.Update(Garden, main.GrowthSpeed, delta);
        almanac.Update(Garden, pointer / Dpi, viewport, delta);
        research.Update(Garden, main.GrowthSpeed, pointer / Dpi, viewport, delta);
        if (hubPanel.IsOpen && !Garden.HubPlanted) hubPanel.Close(); // pruned: its levels sleep
        hubPanel.Update(Garden, main.GrowthSpeed, pointer / Dpi, viewport, delta);
        coreMenu.SetActive(Tool.Info, gardenInfo);
        coreMenu.SetActive(Tool.Almanac, almanac.IsOpen);
        coreMenu.SetActive(Tool.Research, research.IsOpen);
        coreMenu.SetDot(Tool.Research, main.View.Alerts && Garden.UpgradeAffordable && !research.IsOpen);
        coreMenu.SetActive(Tool.Build, Building);
        PlaceCard();
        if (coreMenu.IsOpen) coreMenu.Follow(ToViewport(Garden.Core.Position) / Dpi, Garden.Core.Spec.Radius * ViewScale, viewport);
        // A selected seedling that fills while it's selected offers its evolutions as it does, without another tap
        // (Jeremy, Nov 3).
        // (Only as it fills: closed by Escape or a click elsewhere, it stays closed until the seedling is tapped.)
        bool choosing = selected is { Choosing: true } && partCard.Part == selected;
        if (choosing && !selectedWasChoosing && !evolutionMenu.IsOpen && Mode == DisplayMode.Active && Garden.Nodes.Contains(selected!))
            evolutionMenu.Open(Garden, selected!);
        selectedWasChoosing = choosing;
        // A seedling's choice follows it, and goes once it's chosen, bloomed or pruned.
        if (evolutionMenu.IsOpen)
        {
            if (evolutionMenu.Seedling is { } seedling && Garden.Nodes.Contains(seedling) && seedling.Choosing)
                evolutionMenu.Follow(ToViewport(seedling.Position) / Dpi, seedling.Spec.Radius * ViewScale, viewport);
            else evolutionMenu.Close();
        }

        if (pressed && !Input.IsMouseButtonPressed(MouseButton.Left)) Release();

        // The core's toolbar stays while its menu is open.
        bool overTarget = toolbar.Target != null
            ? under == toolbar.Target || (coreMenu.IsOpen && toolbar.Target == Garden.Core)
            : underTool?.Tool == toolbar.TargetTool;
        if (dragging) Garden.HoldTarget = world;
        else UpdateToolbar(delta, overTarget, overToolbar || overMenu);

        if (toolbar.Target is { } target)
        {
            if (!Garden.Nodes.Contains(target)) toolbar.HideAway();
            else
            {
                bool below = (target == Garden.Core && coreMenu.IsOpen && coreMenu.Above)
                    || (target == evolutionMenu.Seedling && evolutionMenu.IsOpen && evolutionMenu.Above);
                toolbar.Follow(ToViewport(target.Position) / Dpi, target.Spec.Radius * ViewScale, viewport, below);
                toolbar.Refresh();
                if (askPill.Visible)
                {
                    // Centred just over the toolbar, kept on the screen.
                    var size = askPill.GetCombinedMinimumSize();
                    var at = toolbar.Position + new Vector2((toolbar.Size.X - size.X) / 2, -size.Y - 4);
                    askPill.Position = at.Clamp(new Vector2(4, 4), (viewport - size - new Vector2(4, 4)).Max(new Vector2(4, 4)));
                    askPill.Size = size;
                }
            }
        }
        else if (toolbar.TargetTool is { } tool)
        {
            if (!Garden.Shaping.Contains(tool) || !Building) toolbar.HideAway();
            else toolbar.Follow(ToViewport(tool.Position) / Dpi, 20 * ViewScale, viewport);
        }
        main.View.Highlight = dragging || pressed ? pressNode : under ?? toolbar.Target ?? selected;
        main.View.HotTool = dragging ? DropTarget(world) : movingTool ?? underTool?.Tool ?? toolbar.TargetTool;
        main.View.HotHandle = dragging ? ShapingHandle.Body
            : movingTool != null ? movingHandle
            : underTool?.Handle ?? ShapingHandle.Body;

        bool catchMouse = pressed || overToolbar || overUndo || overMenu || under != null || overBar || onBackdrop
            || underTool != null || movingTool != null || gardenGrab != null || overAlmanac || almanac.Dragging
            || overResearch || research.Dragging || overHub || hubPanel.Dragging || overInfo || infoGrab != null;
        if (ClickThrough.Supported) ClickThrough.Set(!catchMouse);
        else UpdateRegion(delta);
        bool active = catchMouse || fading || toolbar.Visible || coreMenu.Visible || evolutionMenu.Visible || Garden.Held != null
            || Garden.Speed > SettledSpeed || main.View.Moving;
        activeTime = active ? SettleSeconds : activeTime - delta;
        SetIdle(activeTime <= 0);
    }

    // While the Move garden handle is held: the whole garden follows the pointer, by the pointer's movement since
    // last frame (no fling: Garden.Translate moves the parts' last positions too).
    void MoveGarden()
    {
        if (gardenGrab is not { } from) return;
        if (!Building || !Input.IsMouseButtonPressed(MouseButton.Left))
        {
            gardenGrab = null;
            return;
        }
        var to = ToSim(ToWorld(PointerInViewport()));
        Garden.Translate(to - from);
        gardenGrab = to;
    }

    // The window minimised or brought back from outside (its taskbar button, Win+D) changes the mode to match.
    void FollowWindow()
    {
        bool minimised = GetWindow().Mode == Window.ModeEnum.Minimized;
        if (minimised && Mode != DisplayMode.Minimised) SetMode(DisplayMode.Minimised);
        else if (!minimised && Mode == DisplayMode.Minimised) SetMode(DisplayMode.Active);
    }

    // The Undo pill sits centred under the plant (over it if there's no room below), kept on the screen, and glides
    // after the plant as it grows and sways rather than jittering with it.
    void PlaceUndo(double delta)
    {
        undo.Update(Garden, delta);
        if (!undo.Visible)
        {
            undoPlaced = false;
            return;
        }
        var (min, max) = Garden.Bounds();
        var screen = GetViewport().GetVisibleRect().Size / Dpi;
        var size = undo.GetCombinedMinimumSize();
        var below = ToViewport(new Num.Vector2(Garden.Core.Position.X, max.Y)) / Dpi + new Vector2(-size.X / 2, 14);
        if (below.Y + size.Y > screen.Y - 4) below.Y = ToViewport(min).Y / Dpi - 14 - size.Y;
        var target = below.Clamp(new Vector2(4, 4), (screen - size - new Vector2(4, 4)).Max(new Vector2(4, 4)));
        undo.Position = undoPlaced ? undo.Position.Lerp(target, 1 - Mathf.Exp(-6 * (float)delta)) : target;
        undo.Size = size;
        undoPlaced = true;
    }

    // The cards (the garden's above the part's) are set down as they open beside the plant and the core's menu, level
    // with the core: to the right if they fit there, else the left if they fit there, else whichever side has more room
    // (the plant usually lives at the screen's bottom right). Then they stay put while open, their top corner on that
    // side fixed (only pushed back onto the screen past an edge).
    void PlaceCard()
    {
        info.Visible = partCard.Visible || gardenCard.Visible;
        infoDivider.Visible = partCard.Visible && gardenCard.Visible;
        if (!Input.IsMouseButtonPressed(MouseButton.Left)) infoGrab = null;
        if (!info.Visible)
        {
            if (!infoMoved) cardAnchor = null; // (moved by hand, it opens where it was left)
            return;
        }
        var screen = GetViewport().GetVisibleRect().Size / Dpi;
        var size = info.GetCombinedMinimumSize();
        if (infoGrab is { } grab)
        {
            // Held: it follows the pointer, set down by its top left from now on.
            cardAnchor = PointerInViewport() / Dpi - grab;
            cardOnLeft = false;
            infoMoved = true;
        }
        if (cardAnchor == null) (cardAnchor, cardOnLeft) = Beside(size, top: 60);
        var at = cardAnchor.Value - new Vector2(cardOnLeft ? size.X : 0, 0);
        info.Position = at.Clamp(new Vector2(4, 4), (screen - size - new Vector2(4, 4)).Max(new Vector2(4, 4)));
        info.Size = size;
    }

    // Where something this size goes beside the plant and the core's menu (see PlaceCard), its top this far above the
    // core: the top corner on the side it's on, and which side.
    (Vector2 Anchor, bool OnLeft) Beside(Vector2 size, float top)
    {
        const float gap = 16;
        var screen = GetViewport().GetVisibleRect().Size / Dpi;
        var (min, max) = Garden.Bounds();
        float left = ToViewport(min).X / Dpi, right = ToViewport(max).X / Dpi;
        if (coreMenu.Extent() is { } menu) // a young plant is narrower than the menu's ring
        {
            left = Mathf.Min(left, menu.Position.X);
            right = Mathf.Max(right, menu.End.X);
        }
        var core = ToViewport(Garden.Core.Position) / Dpi;
        float roomRight = screen.X - right - gap, roomLeft = left - gap;
        float wanted = Mathf.Max(size.X, 200);
        bool onLeft = roomRight < wanted && (roomLeft >= wanted || roomLeft > roomRight);
        return (new Vector2(onLeft ? left - gap : right + gap, core.Y - top), onLeft);
    }

    // A tapped part's toolbar goes once the pointer has been off both the part and the toolbar for a while (see
    // Release and _Ready for the other ways it closes).
    void UpdateToolbar(double delta, bool overTarget, bool overToolbar)
    {
        if ((toolbar.Target != null || toolbar.TargetTool != null) && !overTarget && !overToolbar && !devTapped)
        {
            leaveTime += delta;
            if (leaveTime >= LeaveDelay) toolbar.HideAway();
        }
        else leaveTime = 0;
    }

    // Fallback for other platforms: a passthrough region around the plant (in Ambient, just the core). (On Windows
    // nothing outside it would be drawn, hence ClickThrough there.)
    void UpdateRegion(double delta, bool coreOnly = false)
    {
        regionTimer -= delta;
        if (regionTimer > 0) return;
        regionTimer = 0.25;
        var (min, max) = Garden.Bounds();
        if (coreOnly)
        {
            var r = new Num.Vector2(Garden.Core.Spec.Radius);
            (min, max) = (Garden.Core.Position - r, Garden.Core.Position + r);
        }
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
        OS.LowProcessorUsageModeSleepUsec = 1_000_000 / IdleRate;
        OS.LowProcessorUsageMode = idle;
    }

    // Dev aids for --screenshot runs, once the garden is set up: --inspect (a lab's card, or the core's if it has
    // none), --inspect=core or --inspect=garden; --coremenu opens the core's menu (--coremenu=settings with that
    // slot's name showing); --tap=full or --tap=forming taps a stem with every link taken, or a part forming a bud
    // (starting a lab bud on a stem if none is), its toolbar staying open, or --tap=seedling a seedling (its evolutions
    // open if it waits for its choice, the first one's card showing); --almanac opens the Almanac (--almanac=codex or
    // =evolutions at that page), --research the Research panel (--research=hint with the Stems card's hint showing),
    // --hubpanel the Hub tapped (its card, toolbar and panel);
    // --ambient starts in Ambient, and --corehover acts as if the pointer were on the core.
    void DevFlags()
    {
        devFlagsDone = true;
        var args = OS.GetCmdlineUserArgs();
        string? Value(string name) => args.FirstOrDefault(a => a == $"--{name}" || a.StartsWith($"--{name}="))
            is { } a ? (a.Contains('=') ? a[(a.IndexOf('=') + 1)..] : "") : null;
        if (Value("coremenu") is { } slot)
        {
            OpenCoreMenu();
            if (Enum.TryParse<Tool>(slot, true, out var named)) coreMenu.Labelled = named;
        }
        switch (Value("inspect"))
        {
            case "garden":
                gardenInfo = true;
                Select(Garden.Nodes.FirstOrDefault(n => n.Spec.Kind == NodeKind.Lab) ?? Garden.Core);
                break;
            case "core": Select(Garden.Core); break;
            case "wild": Select(Garden.Nodes.FirstOrDefault(n => n.Spec.IsWild) ?? Garden.Core); break;
            case "" or "lab": Select(Garden.Nodes.FirstOrDefault(n => n.Spec.Kind == NodeKind.Lab) ?? Garden.Core); break;
        }
        // As if a part were tapped: its toolbar and card. A stem with every link taken, or a part forming a bud.
        PlantNode? tapped = Value("tap") switch
        {
            "full" => Garden.Nodes.FirstOrDefault(n => n.Spec.Kind == NodeKind.Stem && n.Links >= n.MaxLinks && !n.FormingBud),
            "seedling" => Garden.Nodes.FirstOrDefault(n => n.Choosing) ?? Garden.Nodes.FirstOrDefault(n => n.IsSeedling),
            "forming" => Garden.Nodes.FirstOrDefault(n => n != Garden.Core && n.FormingBud)
                ?? Garden.Nodes.FirstOrDefault(n => n.Spec.Kind == NodeKind.Stem && Garden.SproutFrom(n, NodeKind.Lab)),
            _ => null,
        };
        if (tapped != null)
        {
            Tap(tapped);
            devTapped = true; // its toolbar stays, with no pointer to keep it
            if (evolutionMenu.IsOpen) evolutionMenu.Labelled = 0; // the first evolution's card showing
        }
        if (Value("almanac") is { } page)
        {
            SetAlmanac(true);
            if (page == "codex") almanac.ShowPage(Almanac.Page.Codex);
            else if (page == "evolutions") almanac.ShowPage(Almanac.Page.Evolutions);
        }
        if (Value("research") is { } shown)
        {
            SetResearch(true);
            if (shown == "hint") research.Hinting = Track.Stems; // as if the pointer were on its (i)
        }
        // As if the Hub were tapped and its upgrades button pressed: its card, toolbar and panel.
        if (Value("hubpanel") != null && Garden.Hub is { } hub)
        {
            Tap(hub);
            devTapped = true;
            OpenHubPanel();
        }
        if (Value("ambient") != null)
        {
            SetMode(DisplayMode.Ambient);
            opacity = AmbientOpacity; // already faded
            devCoreHover = Value("corehover") != null;
        }
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
        // Escape closes one thing at a time: the core's menu, then build mode, then the card.
        if (e is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            if (evolutionMenu.IsOpen) evolutionMenu.Close();
            else if (coreMenu.IsOpen) CloseCoreMenu();
            else if (Building) SetBuilding(false);
            else if (partCard.IsOpen || gardenCard.IsOpen) Select(null);
            else return;
            GetViewport().SetInputAsHandled();
            return;
        }
        // In Ambient only the core takes clicks (see _Process): a tap on it wakes the garden, with its menu open.
        if (Mode == DisplayMode.Ambient && e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } wake
            && Garden.NodeAt(ToSim(ToWorld(wake.Position)), HoverSlop) == Garden.Core)
        {
            SetMode(DisplayMode.Active);
            OpenCoreMenu();
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
                // A press anywhere but the core (its own buttons take their clicks first) closes its menu.
                if (pressNode != Garden.Core) CloseCoreMenu();
                if (pressNode != evolutionMenu.Seedling) evolutionMenu.Close();
                // A potted part is locked: taking it takes its pot, in build mode or not (a tap still opens the part's
                // toolbar).
                if (pressNode?.TiedTo is { Kind: ShapingKind.Pot } held)
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
            OpenSettings(DisplayServer.MouseGetPosition());
        }
        else if (e is InputEventMouseMotion && pressed && !dragging)
        {
            if (((Vector2)(DisplayServer.MouseGetPosition() - dragStartMouse)).Length() > DragThreshold * Dpi)
            {
                dragging = true;
                toolbar.HideAway();
                CloseCoreMenu();
                Garden.Grab(pressNode!);
            }
        }
    }

    // A tap is a press without a drag (see Tap). In build mode, a part dragged and let go over a pot or trellis is
    // tied to it.
    void Release()
    {
        if (!dragging && pressNode != null && Garden.Nodes.Contains(pressNode)) Tap(pressNode);
        if (dragging && pressNode != null && DropTarget(ToSim(ToWorld(PointerInViewport()))) is { } tool)
            Garden.Tie(pressNode, tool);
        if (Garden.Held != null) Garden.LetGo();
        pressed = dragging = false;
        pressNode = null;
    }

    // A tapped part is selected (its card shows) and its toolbar opens. Tapping the selected part again closes both
    // (the core: its menu too). A seedling waiting for its choice opens its evolutions too (see EvolutionMenu).
    void Tap(PlantNode node)
    {
        bool again = node == Garden.Core ? coreMenu.IsOpen : selected == node && partCard.Part == node;
        if (again)
        {
            CloseCoreMenu();
            evolutionMenu.Close();
            if (toolbar.Target == node) toolbar.HideAway();
            Select(null);
        }
        else if (node == Garden.Core) OpenCoreMenu();
        else
        {
            Select(node);
            toolbar.ShowFor(node);
            if (node.Choosing) evolutionMenu.Open(Garden, node);
        }
    }

    // The Hub's panel opens from the upgrades button on the Hub's toolbar and stays until that button or its ✕ closes it
    // (or the Hub is pruned): the first time beside the plant, like the Research panel, then wherever it was left.
    void OpenHubPanel()
    {
        var size = hubPanel.GetCombinedMinimumSize();
        var (anchor, onLeft) = Beside(size, top: size.Y / 2);
        hubPanel.Open(anchor - new Vector2(onLeft ? size.X : 0, 0));
    }

    // The Hub panel's buy: a kind's next level, if the points cover it (its effect rises at once).
    void BuyHubLevel(NodeKind kind)
    {
        if (!Garden.BuyHubLevel(kind)) return;
        hubPanel.RefreshNow();
        partCard.RefreshNow();
    }

    // A pick from a seedling's evolutions: chosen (its store rises to the target), and the menu goes.
    void ChooseEvolution(EvolutionKind kind)
    {
        if (evolutionMenu.Seedling is { } seedling) Garden.Choose(seedling, kind);
        evolutionMenu.Close();
        partCard.RefreshNow();
        gardenCard.RefreshNow();
    }

    void OpenCoreMenu()
    {
        Select(Garden.Core);
        toolbar.ShowFor(Garden.Core);
        coreMenu.Open();
        coreMenu.Follow(ToViewport(Garden.Core.Position) / Dpi, Garden.Core.Spec.Radius * ViewScale,
            GetViewport().GetVisibleRect().Size / Dpi);
    }

    void CloseCoreMenu()
    {
        if (!coreMenu.IsOpen) return;
        coreMenu.Close();
        if (toolbar.Target == Garden.Core) toolbar.HideAway();
        ShowGardenCard();
    }

    // The core menu's slots: Info (the garden card) and Build (build mode) switch on or off, lit while on, and leave
    // the menu and the core's toolbar open; the rest close it and do their job.
    void PickFromCoreMenu(Tool tool)
    {
        switch (tool)
        {
            case Tool.Info:
                gardenInfo = !gardenInfo;
                coreMenu.SetActive(Tool.Info, gardenInfo);
                ShowGardenCard();
                return;
            case Tool.Build:
                SetBuilding(!Building);
                coreMenu.SetActive(Tool.Build, Building);
                return;
            case Tool.Almanac:
                SetAlmanac(!almanac.IsOpen);
                return;
            case Tool.Research:
                SetResearch(!research.IsOpen);
                return;
        }
        CloseCoreMenu();
        switch (tool)
        {
            case Tool.Ambient: SetMode(DisplayMode.Ambient); break;
            case Tool.Minimise: SetMode(DisplayMode.Minimised); break;
            case Tool.Settings: OpenSettings(DisplayServer.MouseGetPosition()); break;
        }
    }

    // The part's card, or with none, no part card (and the garden's goes with it).
    void Select(PlantNode? node)
    {
        if (node != null) partCard.ShowPart(node);
        else partCard.Close();
        selected = node;
        ShowGardenCard();
    }

    // The Almanac's window opens and closes only from its slot or its ✕. The first time, it's set down beside the plant
    // (on the side with room, its top level with the core's); then it opens wherever it was left.
    void SetAlmanac(bool on)
    {
        coreMenu.SetActive(Tool.Almanac, on);
        if (!on)
        {
            almanac.Close();
            return;
        }
        var size = new Vector2(Almanac.Width, Almanac.Height);
        var (anchor, onLeft) = Beside(size, top: size.Y / 2);
        almanac.Open(anchor - new Vector2(onLeft ? size.X : 0, 0));
    }

    // The Research panel opens and closes only from its slot or its ✕, like the Almanac: the first time beside the
    // plant, its middle level with the core, then wherever it was left.
    void SetResearch(bool on)
    {
        coreMenu.SetActive(Tool.Research, on);
        if (!on)
        {
            research.Close();
            return;
        }
        var size = research.GetCombinedMinimumSize();
        var (anchor, onLeft) = Beside(size, top: size.Y / 2);
        research.Open(anchor - new Vector2(onLeft ? size.X : 0, 0));
    }

    // The Research panel's Buy: the track's next upgrade, if the balance covers it (the caps rise at once, and the
    // garden card shows the new ones).
    void Buy(Track track)
    {
        if (!Garden.Buy(track)) return;
        research.RefreshNow();
        gardenCard.RefreshNow();
        partCard.RefreshNow();
    }

    // With Info on, the garden's card shows above the part's while the player is tending: a part selected, or the
    // core's menu open. A click off the plant puts it away; the next tap on it brings it back, until Info is off.
    void ShowGardenCard()
    {
        if (gardenInfo && (selected != null || coreMenu.IsOpen)) gardenCard.ShowGarden();
        else gardenCard.Close();
    }

    void OpenSettings(Vector2I at)
    {
        CheckMenu();
        menu.Popup(new Rect2I(at, Vector2I.Zero));
    }

    // Shows the ask pill over the toolbar for this part: what picking it would do, and Confirm, which does it.
    void Ask(PlantNode part, string text, Action action)
    {
        askFor = part;
        askText.Text = text;
        askAction = action;
        askPill.Visible = true;
        askPill.Size = Vector2.Zero; // (shrinks to the new text)
    }

    void Unask()
    {
        askPill.Visible = false;
        askAction = null;
        askFor = null;
    }

    // Picking a tool does its job and closes the toolbar (and the core's menu, for the core's).
    void Use(Tool tool)
    {
        Unask(); // (another pick forgets what was being asked)
        if (toolbar.TargetTool is { } shaping)
        {
            if (tool == Tool.Remove) Garden.Remove(shaping);
            toolbar.HideAway();
            return;
        }
        if (toolbar.Target is not { } node) return;
        // The Hub's upgrades button opens or closes its panel, its toolbar staying (as the core menu's Research slot).
        if (tool == Tool.HubPanel)
        {
            if (hubPanel.IsOpen) hubPanel.Close();
            else OpenHubPanel();
            return;
        }
        // A bud: grown, or while one forms here, that one cancelled (its own button) or changed (another's).
        if (Toolbar.KindOf(tool) is { } kind)
        {
            var at = node.Spec.IsFruit && node.Parent != null ? node.Parent : node;
            // (Cancelling or changing a forming bud asks first, over the toolbar: its growth would be lost.)
            if (at.Forming is { } forming && at.FormingBud)
            {
                string name = NodeSpecs.All[forming].Name.ToLowerInvariant(), growth = $"{at.Growth / Tuning.BudGrowth * 100:0}%";
                if (Garden.IsBudOf(forming, kind)) Ask(at, $"Cancel the {name} bud ({growth} grown)?", () => Garden.CancelBud(at));
                else if (Garden.CanChangeBud(at, kind))
                    Ask(at, $"Change the {name} bud ({growth} grown) to a new {NodeSpecs.All[kind].Name.ToLowerInvariant()}?",
                        () => Garden.ChangeBud(at, kind));
                return;
            }
            Garden.SproutFrom(at, kind);
        }
        switch (tool)
        {
            case Tool.Unhook: Garden.Untie(node); break;
            case Tool.Prune:
                Garden.Prune(node); // it drifts off and fades (see Garden.Cut)
                undo.RefreshNow(); // the Undo pill, at once
                break;
        }
        partCard.RefreshNow();
        gardenCard.RefreshNow();
        toolbar.HideAway();
        CloseCoreMenu();
    }

    void OnMenu(long id)
    {
        switch ((MenuId)id)
        {
            case MenuId.AlwaysOnTop:
                var window = GetWindow();
                window.AlwaysOnTop = !window.AlwaysOnTop;
                break;
            case MenuId.Speed1: main.GrowthSpeed = 1; break;
            case MenuId.Speed60: main.GrowthSpeed = 60; break;
            case MenuId.Speed3600: main.GrowthSpeed = 3600; break;
            case MenuId.Reset:
                confirmNew.PopupCentered();
                break;
            case MenuId.Quit: main.SaveAndQuit(); break;
            case MenuId.FruitBorrows: fruitBorrowsPush = !fruitBorrowsPush; break;
            case MenuId.Export: main.ExportTracking(null); break;
            case >= MenuId.AmbientIdle0:
                SetAmbient(AmbientOpacity, AmbientScale, IdleRates[(int)id - (int)MenuId.AmbientIdle0]);
                break;
            case >= MenuId.AmbientScale0:
                SetAmbient(AmbientOpacity, AmbientScales[(int)id - (int)MenuId.AmbientScale0], AmbientIdleFps);
                break;
            case >= MenuId.AmbientOpacity0:
                SetAmbient(AmbientOpacities[(int)id - (int)MenuId.AmbientOpacity0], AmbientScale, AmbientIdleFps);
                break;
            case >= MenuId.Idle0: IdleFps = IdleRates[(int)id - (int)MenuId.Idle0]; break;
            case >= MenuId.Size0: PlantScale = Sizes[(int)id - (int)MenuId.Size0]; break;
        }
        CheckMenu();
    }

    public void CheckMenu()
    {
        Check(devMenu, MenuId.AlwaysOnTop, GetWindow().AlwaysOnTop);
        Check(devMenu, MenuId.Speed1, main.GrowthSpeed == 1);
        Check(devMenu, MenuId.Speed60, main.GrowthSpeed == 60);
        Check(devMenu, MenuId.Speed3600, main.GrowthSpeed == 3600);
        Check(devMenu, MenuId.FruitBorrows, fruitBorrowsPush);
        for (int i = 0; i < Sizes.Length; i++) Check(menu, MenuId.Size0 + i, Mathf.IsEqualApprox(Sizes[i], plantScale));
        for (int i = 0; i < IdleRates.Length; i++) Check(menu, MenuId.Idle0 + i, IdleRates[i] == idleFps);
        for (int i = 0; i < AmbientOpacities.Length; i++)
            Check(ambientMenu, MenuId.AmbientOpacity0 + i, Mathf.IsEqualApprox(AmbientOpacities[i], AmbientOpacity));
        for (int i = 0; i < AmbientScales.Length; i++)
            Check(ambientMenu, MenuId.AmbientScale0 + i, Mathf.IsEqualApprox(AmbientScales[i], AmbientScale));
        for (int i = 0; i < IdleRates.Length; i++) Check(ambientMenu, MenuId.AmbientIdle0 + i, IdleRates[i] == AmbientIdleFps);
    }

    // (The Developer submenu is empty in a release build.)
    static void Check(PopupMenu on, MenuId id, bool value)
    {
        int index = on.GetItemIndex((int)id);
        if (index >= 0) on.SetItemChecked(index, value);
    }

    // ==== COORDINATES ==== \\

    // Polled rather than taken from events: the window gets no mouse events while clicks are falling through it.
    Vector2 PointerInViewport() => (Vector2)(DisplayServer.MouseGetPosition() - GetWindow().Position);

    Vector2 ToWorld(Vector2 viewportPoint) => GetViewport().GetCanvasTransform().AffineInverse() * viewportPoint;

    Vector2 ToViewport(Num.Vector2 world) => GetViewport().GetCanvasTransform() * GardenView.V(world);

    static Num.Vector2 ToSim(Vector2 v) => new(v.X, v.Y);
}
