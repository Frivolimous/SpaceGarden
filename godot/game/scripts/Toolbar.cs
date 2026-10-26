using Godot;

namespace SpaceGarden;

// A bud of each plantable kind (DESIGN.md, Tending commands: "New sprout"), then pin and prune.
public enum Tool { Stem, Generator, Lab, Grove, Pin, Prune }

// The little pill of tools that floats above a part while the pointer rests on it. Icons are drawn, not images,
// so they stay crisp at any scale. It fades in and out and follows its part as the plant sways.
public partial class Toolbar : PanelContainer
{
    public event Action<Tool>? Used;
    public PlantNode? Target { get; private set; }
    // Whether a part has room to grow a bud of a kind (its links, and the garden's capacity).
    public Func<PlantNode, NodeKind, bool> CanGrow = (_, _) => true;

    // The kind each bud tool grows.
    public static NodeKind? KindOf(Tool tool) => tool switch
    {
        Tool.Stem => NodeKind.Stem,
        Tool.Generator => NodeKind.Generator,
        Tool.Lab => NodeKind.Lab,
        Tool.Grove => NodeKind.Grove,
        _ => null,
    };

    readonly Dictionary<Tool, ToolButton> buttons = new();
    Tween? fade;

    public Toolbar()
    {
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        AddThemeStyleboxOverride("panel", PillStyle());
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 0);
        AddChild(row);
        foreach (var (tool, tip) in new[]
        {
            (Tool.Stem, "Grow a stem"), (Tool.Generator, "Grow a generator"), (Tool.Lab, "Grow a lab"),
            (Tool.Grove, "Grow a grove"), (Tool.Pin, "Pin in place"), (Tool.Prune, "Prune"),
        })
        {
            var button = new ToolButton(tool) { TooltipText = tip };
            button.Pressed += () => Used?.Invoke(tool);
            buttons[tool] = button;
            row.AddChild(button);
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

    public void ShowFor(PlantNode node)
    {
        bool wasShowing = Target != null;
        Target = node;
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
        if (Target == null) return;
        bool isCore = Target.Parent == null;
        foreach (var (tool, button) in buttons)
            if (KindOf(tool) is { } kind) button.Enabled = CanGrow(Target, kind);
        buttons[Tool.Pin].Enabled = !isCore;
        buttons[Tool.Pin].Active = !isCore && Target.Fixed;
        buttons[Tool.Prune].Enabled = !isCore;
    }

    public void HideAway()
    {
        if (!Visible || Target == null) return;
        Target = null;
        fade?.Kill();
        fade = CreateTween();
        fade.TweenProperty(this, "modulate:a", 0.0, 0.2);
        fade.TweenCallback(Callable.From(() => Visible = false));
    }

    // Sits centred just above the part (below it if there's no room), kept inside the window.
    public void Follow(Vector2 partOnScreen, float partRadiusOnScreen, Vector2 viewport)
    {
        var size = GetCombinedMinimumSize();
        var above = partOnScreen - new Vector2(size.X / 2, partRadiusOnScreen + 16 + size.Y);
        if (above.Y < 4) above.Y = partOnScreen.Y + partRadiusOnScreen + 16;
        Position = above.Clamp(new Vector2(4, 4), viewport - size - new Vector2(4, 4));
        Size = size;
    }
}

public partial class ToolButton : Control
{
    public event Action? Pressed;
    public bool Enabled = true;
    public bool Active;
    readonly Tool tool;
    bool hover;

    public ToolButton(Tool tool)
    {
        this.tool = tool;
        CustomMinimumSize = new Vector2(30, 30);
        MouseFilter = MouseFilterEnum.Stop;
        MouseEntered += () => { hover = true; QueueRedraw(); };
        MouseExited += () => { hover = false; QueueRedraw(); };
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

    public override void _Draw()
    {
        var c = Size / 2;
        if (hover && Enabled) DrawCircle(c, 13, new Color(1, 1, 1, 0.13f));
        var ink = !Enabled ? new Color(1, 1, 1, 0.25f) : Active ? new Color(1, 0.9f, 0.45f) : new Color(1, 1, 1, 0.9f);
        const float w = 1.8f;
        if (Toolbar.KindOf(tool) is { } kind)
        {
            // A bud: the kind's own shape in its own colour, greyed when there's no room for it.
            var spec = NodeSpecs.All[kind];
            var colour = GardenView.Rgb(spec.Rgb);
            if (!Enabled) colour = new Color(1, 1, 1, 0.25f);
            var points = GardenView.UnitPolygon(spec.Shape);
            for (int i = 0; i < points.Length; i++) points[i] = c + points[i].Rotated(-Mathf.Pi / 2) * 7;
            DrawColoredPolygon(points, colour);
            return;
        }
        switch (tool)
        {
            case Tool.Pin: // a map pin
                DrawCircle(c + new Vector2(0, -3), 4.5f, ink);
                DrawCircle(c + new Vector2(0, -3), 1.6f, new Color(0.07f, 0.09f, 0.13f));
                DrawLine(c + new Vector2(0, 1), c + new Vector2(0, 8), ink, w, true);
                break;
            case Tool.Prune: // scissors
                DrawArc(c + new Vector2(-3.5f, 4.5f), 2.6f, 0, Mathf.Tau, 16, ink, 1.5f, true);
                DrawArc(c + new Vector2(3.5f, 4.5f), 2.6f, 0, Mathf.Tau, 16, ink, 1.5f, true);
                DrawLine(c + new Vector2(-2, 2.5f), c + new Vector2(4, -7), ink, w, true);
                DrawLine(c + new Vector2(2, 2.5f), c + new Vector2(-4, -7), ink, w, true);
                break;
        }
    }
}
