using Godot;

namespace SpaceGarden;

// A bud of each plantable kind (DESIGN.md, Tending commands: "New sprout"), then prune; Unhook takes a part off the
// pot or trellis holding it, and Remove takes a shaping tool away. The shaping bar (build mode) places a pot, a
// trellis or a well, and Done leaves build mode. The core's menu (CoreMenu) opens the garden card (Info), build mode,
// Ambient, Minimised and the settings.
public enum Tool
{
    Stem, Generator, Lab, Grove, Seedling, Hub, HubPanel, Prune, Unhook, Remove, Pot, Trellis, Well, Done,
    Info, Almanac, Research, Build, Ambient, Minimise, Settings,
}

// The little pill of tools that floats above a tapped part (or above a shaping tool tapped in build mode, with just
// Remove; or below the core, if its menu had no room under it). Icons are drawn, not images, so they stay crisp at any scale. It fades in and out and
// follows its part as the plant sways.
public partial class Toolbar : PanelContainer
{
    public event Action<Tool>? Used;
    public PlantNode? Target { get; private set; }
    public ShapingTool? TargetTool { get; private set; }
    // Whether a part has room to grow a bud of a kind (its links, and the garden's capacity).
    public Func<PlantNode, NodeKind, bool> CanGrow = (_, _) => true;
    // Whether the bud forming on a part could change to this kind (see Garden.CanChangeBud).
    public Func<PlantNode, NodeKind, bool> CanChange = (_, _) => false;
    readonly Dictionary<Tool, string> tips = new();

    static string Article(NodeKind kind)
    {
        var name = NodeSpecs.All[kind].Name.ToLowerInvariant();
        return ("aeiou".Contains(name[0]) ? "an " : "a ") + name;
    }
    // Whether a bud tool shows at all: the seedling's while a seedling slot is free, the Hub's while one can be planted.
    public Func<NodeKind, bool> Offered = _ => true;
    // Whether the Hub's panel is open (its button on the Hub's toolbar is lit then).
    public Func<bool> HubPanelOpen = () => false;
    // Whether the tutorial lets the player do this now (see Garden.TutorialAllows): held-back buttons are greyed.
    public Func<TutorialAction, NodeKind?, bool> Allows = (_, _) => true;

    // The kind each bud tool grows.
    public static NodeKind? KindOf(Tool tool) => tool switch
    {
        Tool.Stem => NodeKind.Stem,
        Tool.Generator => NodeKind.Generator,
        Tool.Lab => NodeKind.Lab,
        Tool.Grove => NodeKind.Grove,
        Tool.Seedling => NodeKind.Seedling,
        Tool.Hub => NodeKind.Hub,
        _ => null,
    };

    readonly Dictionary<Tool, ToolButton> buttons = new();
    // Over the bud buttons when every link of the part is taken: "3/3 Connections Used", covering them.
    readonly PanelContainer full;
    readonly Label fullText;
    Tween? fade;

    public Toolbar()
    {
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        AddThemeStyleboxOverride("panel", PillStyle());
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 0);
        AddChild(row);
        // The buds sit in a stack of their own, so the full note can lie over them (a MarginContainer overlaps its
        // children).
        var budStack = new MarginContainer { MouseFilter = MouseFilterEnum.Pass };
        var buds = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        buds.AddThemeConstantOverride("separation", 0);
        buds.Alignment = BoxContainer.AlignmentMode.Center;
        budStack.AddChild(buds);
        row.AddChild(budStack);
        fullText = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            LabelSettings = new LabelSettings { FontSize = 10 },
            MouseFilter = MouseFilterEnum.Ignore,
        };
        var fullStyle = PillStyle();
        fullStyle.BorderColor = new Color(1, 1, 1, 0.3f);
        fullStyle.ShadowSize = 0;
        fullStyle.ContentMarginLeft = fullStyle.ContentMarginRight = 6;
        fullStyle.ContentMarginTop = fullStyle.ContentMarginBottom = 1;
        full = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Stop, // the dimmed buds beneath take no clicks or tips
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            Visible = false,
        };
        full.AddThemeStyleboxOverride("panel", fullStyle);
        full.AddChild(fullText);
        budStack.AddChild(full);
        foreach (var (tool, tip) in new[]
        {
            (Tool.Stem, "Grow a stem"), (Tool.Generator, "Grow a generator"), (Tool.Lab, "Grow a lab"),
            (Tool.Grove, "Grow a grove"), (Tool.Seedling, "Plant a seedling (it uses a seedling slot)"),
            (Tool.Hub, "Plant the Hub (one at a time)"),
            (Tool.HubPanel, "Hub upgrades"), (Tool.Unhook, "Take it off its pot or trellis"),
            (Tool.Prune, "Prune"), (Tool.Remove, "Take this away"),
        })
        {
            var button = new ToolButton(tool) { TooltipText = tip };
            tips[tool] = tip;
            button.Pressed += () => Used?.Invoke(tool);
            buttons[tool] = button;
            (KindOf(tool) != null ? buds : row).AddChild(button);
        }
    }

    // The dark rounded pill the widget's few controls sit on (the toolbar, the undo button).
    public static StyleBoxFlat PillStyle() => new()
    {
        BgColor = new Color(0.07f, 0.09f, 0.13f, 0.88f),
        BorderColor = new Color(1, 1, 1, 0.14f),
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16, CornerRadiusBottomLeft = 16, CornerRadiusBottomRight = 16,
        ContentMarginLeft = 4, ContentMarginRight = 4, ContentMarginTop = 2, ContentMarginBottom = 2,
        ShadowColor = new Color(0, 0, 0, 0.25f),
        ShadowSize = 4,
        AntiAliasing = true,
    };

    // The tutorial's beckon on one of its buttons (or none).
    public void SetBeckon(Tool? tool)
    {
        foreach (var (t, button) in buttons) button.Beckon = t == tool;
    }

    public void ShowFor(PlantNode node) => Show(node, null);

    public void ShowFor(ShapingTool tool) => Show(null, tool);

    void Show(PlantNode? node, ShapingTool? tool)
    {
        bool wasShowing = Target != null || TargetTool != null;
        Target = node;
        TargetTool = tool;
        Refresh();
        if (wasShowing) return;
        if (!Visible) Modulate = new Color(1, 1, 1, 0);
        Visible = true;
        fade?.Kill();
        fade = CreateTween();
        fade.TweenProperty(this, "modulate:a", 1.0, 0.15);
    }

    public void Refresh()
    {
        foreach (var (tool, button) in buttons) button.Visible = (tool == Tool.Remove) == (TargetTool != null);
        // (A shaping tool's toolbar has no buds, so no note over them: it mustn't keep the last part's.)
        full.Visible = false;
        if (Target == null) return;
        buttons[Tool.Unhook].Visible = Target.TiedTo != null;
        // The Hub's own: its upgrades panel, as the core's menu has Research.
        buttons[Tool.HubPanel].Visible = Target.IsHub;
        buttons[Tool.HubPanel].Active = Target.IsHub && HubPanelOpen();
        // A part with room for only its parent's link (a generator, lab or grove) can't grow anything: no buds.
        bool buds = Target.MaxLinks > 1;
        // A core of one link (the start) offers the stem's button alone, while that link is free (or its bud forms),
        // so a garden that pruned its last stem can grow one again (Jeremy, Nov 3). With more, the core's as any part.
        bool lone = Target.Parent == null && Target.MaxLinks == 1;
        if (lone) buds = Target.CanBranch || Target.FormingBud;
        foreach (var (tool, button) in buttons)
            if (KindOf(tool) is { } kind)
            {
                // Its bud is growing here now (or a wild flower standing in for it).
                button.Forming = Target.Forming is { } forming && Target.FormingBud && Garden.IsBudOf(forming, kind);
                // (A seedling's or the Hub's shows only while one could be planted, or one forms here.)
                button.Visible = buds && (Offered(kind) || button.Forming) && (!lone || kind == NodeKind.Stem);
                // (While a bud forms: its own button cancels it, another changes it, starting over.)
                button.Enabled = button.Forming || Target.FormingBud ? Allows(TutorialAction.CancelBud, kind) && (button.Forming || CanChange(Target, kind))
                    : CanGrow(Target, kind) && Allows(TutorialAction.Bud, kind);
                button.TooltipText = button.Forming ? $"Cancel the {NodeSpecs.All[Target.Forming!.Value].Name.ToLowerInvariant()} bud (its growth is lost)"
                    : Target.FormingBud ? $"Change the bud to {Article(kind)} (it starts over)"
                    : tips[tool];
            }
        buttons[Tool.Prune].Visible = Target.Parent != null; // the core can't be pruned
        buttons[Tool.Prune].Enabled = Allows(TutorialAction.Prune, null);
        // Every link taken (a bud forming holds one, but then its button shows it growing instead).
        full.Visible = buds && Target.Links >= Target.MaxLinks && !Target.FormingBud;
        if (full.Visible) fullText.Text = $"{Target.Links}/{Target.MaxLinks} Connections Used";
    }

    public void HideAway()
    {
        if (!Visible || (Target == null && TargetTool == null)) return;
        Target = null;
        TargetTool = null;
        fade?.Kill();
        fade = CreateTween();
        fade.TweenProperty(this, "modulate:a", 0.0, 0.2);
        fade.TweenCallback(Callable.From(() => Visible = false));
    }

    // Sits centred just above the part (below it if there's no room, or if asked: the core's menu is above), kept
    // inside the window.
    public void Follow(Vector2 partOnScreen, float partRadiusOnScreen, Vector2 viewport, bool below = false)
    {
        var size = GetCombinedMinimumSize();
        var above = partOnScreen - new Vector2(size.X / 2, partRadiusOnScreen + 16 + size.Y);
        if (below || above.Y < 4) above.Y = partOnScreen.Y + partRadiusOnScreen + 16;
        Position = above.Clamp(new Vector2(4, 4), viewport - size - new Vector2(4, 4));
        Size = size;
    }
}

public partial class ToolButton : Control
{
    public event Action? Pressed;
    public bool Enabled = true;
    public bool Active;
    public bool Forming; // a bud tool whose kind is growing on the part now: outlined, pulsing
    public bool Dot; // an alert: a small red dot on the disc's rim, top right (something waits behind it)
    public bool Beckon; // the tutorial's next thing to touch: a pulsing gold ring around it
    public string? Badge; // a small count in the corner (how many more of a shaping tool can be placed)
    public readonly Tool Tool;
    // Drawn on a disc of its own, in the pill's look (the core's menu, where the buttons stand apart).
    public readonly bool Disc;
    public bool Hover { get; private set; }

    public const float DiscRadius = 16;
    const float FormingPulseSeconds = 1.2f;
    static readonly Color DiscFill = new(0.07f, 0.09f, 0.13f, 0.92f);

    public ToolButton(Tool tool, bool disc = false)
    {
        Tool = tool;
        Disc = disc;
        CustomMinimumSize = disc ? new Vector2(DiscRadius * 2 + 2, DiscRadius * 2 + 2) : new Vector2(30, 30);
        MouseFilter = MouseFilterEnum.Stop;
        MouseEntered += () => { Hover = true; QueueRedraw(); };
        MouseExited += () => { Hover = false; QueueRedraw(); };
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            AcceptEvent();
            if (Enabled) Pressed?.Invoke();
            QueueRedraw();
        }
    }

    public override void _Process(double delta)
    {
        if (IsVisibleInTree()) QueueRedraw(); // Enabled/Active change from outside
    }

    // The tutorial's beckon (see Widget.UpdateTutorial): a gold ring, breathing, on a soft dark edge so it shows over a
    // light window as well as a dark one.
    public static readonly Color BeckonGold = new(1, 0.85f, 0.35f);
    public const float BeckonSeconds = 1.4f;

    public static void DrawBeckon(CanvasItem on, Vector2 c, float radius)
    {
        float beat = 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 1000 * Mathf.Tau / BeckonSeconds);
        float r = radius + 1 + 3 * beat;
        on.DrawArc(c, r, 0, Mathf.Tau, 40, new Color(0, 0, 0, 0.3f), 4, true);
        on.DrawArc(c, r, 0, Mathf.Tau, 40, BeckonGold with { A = 0.5f + 0.5f * beat }, 2, true);
    }

    // Any part's shape, centred at c with this radius, in a colour (from its texture, so a wild flower's too).
    public static void DrawShape(CanvasItem on, Vector2 c, Shape shape, float radius, Color colour)
    {
        float d = Textures.ShapeDiameter(radius);
        on.DrawTextureRect(Textures.Shape(shape), new Rect2(c - new Vector2(d, d) / 2, new Vector2(d, d)), false, colour);
    }

    public override void _Draw()
    {
        var c = Size / 2;
        if (Disc)
        {
            DrawCircle(c, DiscRadius + 1, new Color(0, 0, 0, 0.25f)); // a soft shadow, as the pill's
            DrawCircle(c, DiscRadius, DiscFill);
            DrawArc(c, DiscRadius, 0, Mathf.Tau, 32, new Color(1, 1, 1, Hover ? 0.4f : 0.14f), 1, true);
        }
        if (Beckon) DrawBeckon(this, c, Disc ? DiscRadius + 2 : 14);
        if (Hover && Enabled) DrawCircle(c, 13, new Color(1, 1, 1, 0.13f));
        var ink = !Enabled ? new Color(1, 1, 1, 0.25f) : Active ? new Color(1, 0.9f, 0.45f) : new Color(1, 1, 1, 0.9f);
        const float w = 1.8f;
        var tool = Tool;
        if (Toolbar.KindOf(tool) is { } kind)
        {
            // A bud: the kind's own shape in its own colour, greyed when there's no room for it.
            var spec = NodeSpecs.All[kind];
            var colour = GardenView.Rgb(spec.Rgb);
            // While its bud grows on the part, it keeps its colour (the part has no room for another just then) and
            // a pulsing outline in it rings it.
            if (!Enabled && !Forming) colour = new Color(1, 1, 1, 0.25f);
            if (Textures.InAtlas(spec.Shape))
            {
                // A seedling's sprout, drawn from its texture (its ring a circle).
                DrawShape(this, c, spec.Shape, 8, colour);
                if (Forming)
                {
                    float beat = 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 1000 * Mathf.Tau / FormingPulseSeconds);
                    DrawArc(c, 11 + 1.5f * beat, 0, Mathf.Tau, 32, colour with { A = 0.35f + 0.55f * beat }, 1.4f, true);
                }
                return;
            }
            var points = GardenView.UnitPolygon(spec.Shape);
            for (int i = 0; i < points.Length; i++) points[i] = c + points[i].Rotated(-Mathf.Pi / 2) * 7;
            DrawColoredPolygon(points, colour);
            if (Forming)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 1000 * Mathf.Tau / FormingPulseSeconds);
                var ring = new Vector2[points.Length + 1];
                for (int i = 0; i < points.Length; i++) ring[i] = c + (points[i] - c) * (1.55f + 0.2f * pulse);
                ring[^1] = ring[0];
                DrawPolyline(ring, colour with { A = 0.35f + 0.55f * pulse }, 1.4f, true);
            }
            return;
        }
        switch (tool)
        {
            case Tool.Info: // an i in a ring
                DrawArc(c, 8, 0, Mathf.Tau, 24, ink, 1.4f, true);
                DrawCircle(c + new Vector2(0, -3.8f), 1.3f, ink);
                DrawLine(c + new Vector2(0, -0.8f), c + new Vector2(0, 4.5f), ink, w, true);
                break;
            case Tool.Almanac: // an open book
                DrawPolyline([c + new Vector2(-7, -4), c + new Vector2(0, -2.5f), c + new Vector2(7, -4), c + new Vector2(7, 5),
                    c + new Vector2(0, 6.5f), c + new Vector2(-7, 5), c + new Vector2(-7, -4)], ink, w, true);
                DrawLine(c + new Vector2(0, -2.5f), c + new Vector2(0, 6.5f), ink, w, true);
                break;
            case Tool.HubPanel: // two chevrons rising: upgrades
                DrawPolyline([c + new Vector2(-5.5f, 0), c + new Vector2(0, -5), c + new Vector2(5.5f, 0)], ink, w, true);
                DrawPolyline([c + new Vector2(-5.5f, 5.5f), c + new Vector2(0, 0.5f), c + new Vector2(5.5f, 5.5f)], ink, w, true);
                break;
            case Tool.Research: // a flask, a gene bubbling in it
                DrawPolyline([c + new Vector2(-2.5f, -7), c + new Vector2(-2.5f, -2), c + new Vector2(-7, 6),
                    c + new Vector2(7, 6), c + new Vector2(2.5f, -2), c + new Vector2(2.5f, -7)], ink, w, true);
                DrawLine(c + new Vector2(-4, -7), c + new Vector2(4, -7), ink, w, true);
                DrawCircle(c + new Vector2(0, 2.5f), 1.6f, ink);
                break;
            case Tool.Build: // the pot, as on the shaping bar, in the button's ink
                ShapingLook.DrawIcon(this, Tool.Pot, c + new Vector2(0, 1), ink);
                break;
            case Tool.Ambient: // a crescent moon: the garden faded back
                DrawArc(c + new Vector2(-1, 0), 7, Mathf.Pi * 0.3f, Mathf.Pi * 1.7f, 24, ink, w, true);
                DrawArc(c + new Vector2(2.2f, -1.2f), 5.2f, Mathf.Pi * 0.55f, Mathf.Pi * 1.45f, 20, ink, 1.4f, true);
                break;
            case Tool.Minimise: // down into the taskbar
                DrawPolyline([c + new Vector2(-4.5f, -4.5f), c + new Vector2(0, 0), c + new Vector2(4.5f, -4.5f)], ink, w, true);
                DrawLine(c + new Vector2(0, -7), c + new Vector2(0, 0), ink, w, true);
                DrawLine(c + new Vector2(-6.5f, 5), c + new Vector2(6.5f, 5), ink, w, true);
                break;
            case Tool.Settings: // a cog
                DrawArc(c, 4.6f, 0, Mathf.Tau, 20, ink, 2.2f, true);
                for (int i = 0; i < 8; i++)
                {
                    var u = Vector2.FromAngle(i * Mathf.Tau / 8);
                    DrawLine(c + u * 5.6f, c + u * 8, ink, 2.4f, true);
                }
                break;
            case Tool.Unhook: // a hook, opened
                DrawArc(c + new Vector2(0, 2), 4.5f, 0, Mathf.Pi * 1.25f, 16, ink, w, true);
                DrawLine(c + new Vector2(4.5f, 2), c + new Vector2(4.5f, -7), ink, w, true);
                DrawLine(c + new Vector2(-6.5f, -4), c + new Vector2(-2.5f, -8), ink, 1.4f, true);
                break;
            case Tool.Remove: // a cross
                DrawLine(c + new Vector2(-5, -5), c + new Vector2(5, 5), ink, w, true);
                DrawLine(c + new Vector2(5, -5), c + new Vector2(-5, 5), ink, w, true);
                break;
            case Tool.Done: // a tick
                DrawPolyline([c + new Vector2(-6, 0), c + new Vector2(-2, 4.5f), c + new Vector2(6, -5)], ink, w, true);
                break;
            case Tool.Pot or Tool.Trellis or Tool.Well:
                ShapingLook.DrawIcon(this, tool, c, Enabled ? ShapingLook.Tint : new Color(1, 1, 1, 0.25f));
                if (Badge != null)
                    DrawString(ThemeDB.FallbackFont, c + new Vector2(6, 14), Badge, HorizontalAlignment.Left, -1, 9,
                        Enabled ? new Color(1, 1, 1, 0.75f) : new Color(1, 1, 1, 0.3f));
                break;
            case Tool.Prune: // scissors
                DrawArc(c + new Vector2(-3.5f, 4.5f), 2.6f, 0, Mathf.Tau, 16, ink, 1.5f, true);
                DrawArc(c + new Vector2(3.5f, 4.5f), 2.6f, 0, Mathf.Tau, 16, ink, 1.5f, true);
                DrawLine(c + new Vector2(-2, 2.5f), c + new Vector2(4, -7), ink, w, true);
                DrawLine(c + new Vector2(2, 2.5f), c + new Vector2(-4, -7), ink, w, true);
                break;
        }
        if (Dot)
        {
            var dot = c + Vector2.FromAngle(-Mathf.Pi / 4) * (Disc ? DiscRadius - 2 : 11);
            DrawCircle(dot, 4.5f, DiscFill);
            DrawCircle(dot, 3.5f, new Color(0.95f, 0.3f, 0.28f));
        }
    }
}

// Build mode's bar (DESIGN.md, Shaping the garden): a pot, a trellis and a well to place, each with how many more the
// caps allow (greyed, with the garden size that brings it, until then), and Done. Pressing a tool starts carrying a
// new one off the bar: it follows the pointer until let go.
public partial class ShapingBar : PanelContainer
{
    public event Action<ShapingKind>? Started;
    public event Action? Done;
    // The special views (Jeremy, Nov 3), in a row under the tools: one at a time, or none.
    public event Action<HeatView>? HeatPicked;
    // Move garden (Jeremy, Nov 3): a handle; dragging it moves the whole garden, tools and all.
    public event Action? MoveStarted;

    readonly Dictionary<HeatView, Button> heatButtons = new();

    readonly Dictionary<ShapingKind, ToolButton> buttons = new();
    readonly MoveHandle handle;
    readonly ToolButton done;
    Tween? fade;
    bool showing;

    public ShapingBar()
    {
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        AddThemeStyleboxOverride("panel", Toolbar.PillStyle());
        // The Move garden handle on the left, the full height of the bar; then the tools over the views.
        var outer = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        outer.AddThemeConstantOverride("separation", 4);
        AddChild(outer);
        handle = new MoveHandle { TooltipText = "Drag to move the whole garden at once" };
        handle.Grabbed += () => MoveStarted?.Invoke();
        outer.AddChild(handle);
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        column.AddThemeConstantOverride("separation", 2);
        outer.AddChild(column);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 2);
        column.AddChild(row);
        var views = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass, Alignment = BoxContainer.AlignmentMode.Center };
        views.AddThemeConstantOverride("separation", 4);
        column.AddChild(views);
        foreach (var (view, text, tip) in new[]
        {
            (HeatView.Power, "Power", "Show each part's power as heat: blue empty, green half, red full"),
            (HeatView.Blobs, "Blob paths", "Show where gene and growth blobs have been going lately (counted from now)"),
        })
        {
            var toggle = new Button
            {
                Text = text, TooltipText = tip, ToggleMode = true, Flat = true,
                FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand,
            };
            toggle.AddThemeFontSizeOverride("font_size", 11);
            toggle.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.55f));
            toggle.AddThemeColorOverride("font_hover_color", new Color(1, 1, 1, 0.85f));
            toggle.AddThemeColorOverride("font_pressed_color", new Color(1, 0.9f, 0.45f));
            toggle.AddThemeColorOverride("font_hover_pressed_color", new Color(1, 0.95f, 0.6f));
            toggle.Toggled += on =>
            {
                foreach (var (other, button) in heatButtons) if (other != view) button.SetPressedNoSignal(false);
                HeatPicked?.Invoke(on ? view : HeatView.None);
            };
            heatButtons[view] = toggle;
            views.AddChild(toggle);
        }

        foreach (var (kind, tool) in new[] { (ShapingKind.Pot, Tool.Pot), (ShapingKind.Trellis, Tool.Trellis), (ShapingKind.Well, Tool.Well) })
        {
            var button = new ToolButton(tool);
            button.Pressed += () => Started?.Invoke(kind);
            buttons[kind] = button;
            row.AddChild(button);
        }
        done = new ToolButton(Tool.Done) { TooltipText = "Done arranging" };
        done.Pressed += () => Done?.Invoke();
        row.AddChild(done);
    }

    // The tutorial's beckon on a tool's button (or none).
    public void SetBeckon(ShapingKind? kind, bool done = false)
    {
        foreach (var (k, button) in buttons) button.Beckon = k == kind;
        this.done.Beckon = done;
    }

    // What each tool's button says: how many more can be placed, or the garden size that brings it.
    public void Refresh(Garden garden)
    {
        // (The tutorial's Tending Mode holds back every tool but the pot, and moving the garden.)
        handle.Visible = garden.TutorialAllows(TutorialAction.MoveGarden);
        foreach (var (kind, button) in buttons)
        {
            int cap = garden.Capacity.Of(kind), left = cap - garden.Shaping.Count(t => t.Kind == kind);
            button.Enabled = left > 0 && garden.TutorialAllows(TutorialAction.Shaping, shaping: kind);
            button.Badge = cap > 0 ? $"{left}" : null;
            string name = ShapingLook.Name(kind);
            int size = garden.Tracking.MostMainNodes;
            string more = Tuning.NextToolSize(kind, size) is { } at ? $" Another at garden size {at}." : "";
            button.TooltipText = cap == 0 ? $"{name}: comes at garden size {Tuning.NextToolSize(kind, size)} (now {size}: "
                    + $"the most stems and flowers it has had, the core too). {ShapingLook.Does(kind)}"
                : left > 0 ? $"{name}: drag one into the garden.{more} {ShapingLook.Does(kind)}"
                : $"{name}: all placed.{more} {ShapingLook.Does(kind)}";
        }
    }

    public void Appear(bool show)
    {
        if (show == showing) return;
        showing = show;
        if (!show) foreach (var button in heatButtons.Values) button.SetPressedNoSignal(false); // (they end with build mode)
        fade?.Kill();
        if (show)
        {
            if (!Visible) Modulate = new Color(1, 1, 1, 0);
            Visible = true;
        }
        fade = CreateTween();
        fade.TweenProperty(this, "modulate:a", show ? 1.0 : 0.0, 0.2);
        if (!show) fade.TweenCallback(Callable.From(() => Visible = false));
    }

    // Centred just above this point (the top of build mode's backdrop), kept inside the window.
    public void Follow(Vector2 above, Vector2 viewport)
    {
        var size = GetCombinedMinimumSize();
        Position = (above - new Vector2(size.X / 2, size.Y + 6)).Clamp(new Vector2(4, 4), viewport - size - new Vector2(4, 4));
        Size = size;
    }
}

// Build mode's Move garden handle (Jeremy, Nov 3): four arrows from a centre, on the left of the shaping bar; a press
// on it starts moving the whole garden (the widget follows the pointer while it's held).
public partial class MoveHandle : Control
{
    public event Action? Grabbed;

    public MoveHandle()
    {
        CustomMinimumSize = new Vector2(30, 0);
        SizeFlagsVertical = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Move;
        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
        GuiInput += e =>
        {
            if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;
            AcceptEvent();
            Grabbed?.Invoke();
        };
    }

    public override void _Draw()
    {
        var c = Size / 2;
        bool hovered = GetGlobalRect().HasPoint(GetGlobalMousePosition());
        var ink = new Color(1, 1, 1, hovered ? 0.95f : 0.6f);
        if (hovered) DrawCircle(c, 13, new Color(1, 1, 1, 0.13f));
        const float arm = 9, head = 3.2f;
        foreach (var dir in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
        {
            var tip = c + dir * arm;
            DrawLine(c, tip, ink, 1.6f, true);
            var side = new Vector2(-dir.Y, dir.X);
            DrawLine(tip, tip - dir * head + side * head, ink, 1.6f, true);
            DrawLine(tip, tip - dir * head - side * head, ink, 1.6f, true);
        }
    }
}

// The tutorial's beckon around a line or a button (a card's Forming line, Research's Buy): a gold frame breathing just
// outside its parent, drawn over it. Laid on a control that isn't a container, so it sits where it's anchored.
public partial class BeckonFrame : Control
{
    public BeckonFrame()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        SetAnchorsPreset(LayoutPreset.FullRect);
        OffsetLeft = OffsetTop = -4;
        OffsetRight = OffsetBottom = 4;
    }

    public override void _Process(double delta)
    {
        if (Visible) QueueRedraw();
    }

    public override void _Draw()
    {
        float beat = 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 1000 * Mathf.Tau / ToolButton.BeckonSeconds);
        var rect = new Rect2(Vector2.Zero, Size).Grow(beat * 2);
        DrawRect(rect, new Color(0, 0, 0, 0.3f), false, 4);
        DrawRect(rect, ToolButton.BeckonGold with { A = 0.5f + 0.5f * beat }, false, 2);
    }
}
