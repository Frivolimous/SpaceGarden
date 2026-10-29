using Godot;

namespace SpaceGarden;

// The core's menu (DESIGN.md, Information display): a half ring of round buttons under the core (over it if there's
// no room below), so the space above stays clear for the core's toolbar and the shaping bar. Each slot's name shows
// beside it while the pointer is on it. Slots are spread evenly round the half ring and it widens as more join (the
// Almanac and Research, later). It fades in and out; the widget opens it on a tap of the core and closes it on
// another, a tap anywhere else, Escape or a pick.
public partial class CoreMenu : Control
{
    public event Action<Tool>? Picked;

    // The slots, in order round the ring from left to right.
    static readonly (Tool Tool, string Label)[] Slots =
    [
        (Tool.Info, "Garden info"), (Tool.Build, "Build mode"), (Tool.Ambient, "Ambient"), (Tool.Minimise, "Minimise"),
        (Tool.Settings, "Settings"),
    ];

    const float Gap = 14; // between the core's edge and the slots' inner edge
    const float Spread = 150; // the most of the half ring the slots fill, in degrees
    const float MaxStep = 36; // degrees between neighbouring slots, at most

    readonly List<ToolButton> buttons = new();
    readonly Label label;
    Tween? fade;

    public bool IsOpen { get; private set; }

    // Dev aid (--coremenu=NAME): a slot whose name shows as if the pointer were on it, for a screenshot.
    public Tool? Labelled;

    // Whether it opened over the core (no room below).
    public bool Above { get; private set; }

    public CoreMenu()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        foreach (var (tool, _) in Slots)
        {
            var button = new ToolButton(tool, disc: true);
            button.Pressed += () => Picked?.Invoke(tool);
            buttons.Add(button);
            AddChild(button);
        }
        label = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
            LabelSettings = new LabelSettings { FontSize = 13, OutlineSize = 4, OutlineColor = new Color(0, 0, 0, 0.7f) },
        };
        AddChild(label);
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        if (!Visible) Modulate = new Color(1, 1, 1, 0);
        Visible = true;
        fade?.Kill();
        fade = CreateTween();
        fade.TweenProperty(this, "modulate:a", 1.0, 0.15);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        fade?.Kill();
        fade = CreateTween();
        fade.TweenProperty(this, "modulate:a", 0.0, 0.15);
        fade.TweenCallback(Callable.From(() => Visible = false));
    }

    // A slot that switches something on (Info, Build) is lit while it's on.
    public void SetActive(Tool tool, bool on)
    {
        foreach (var button in buttons)
            if (button.Tool == tool) button.Active = on;
    }

    // Whether the pointer is on one of its slots (in the menu's layer's units).
    public bool Over(Vector2 point) => IsOpen && buttons.Any(b => b.GetGlobalRect().Grow(2).HasPoint(point));

    // Lays the slots out round the core, below it if they fit there, and shows the name of the one under the pointer.
    public void Follow(Vector2 core, float coreRadius, Vector2 viewport)
    {
        int n = buttons.Count;
        float step = n > 1 ? Mathf.Min(MaxStep, Spread / (n - 1)) : 0;
        float slot = ToolButton.DiscRadius;
        // Far enough out that neighbouring slots don't touch.
        float ring = Mathf.Max(coreRadius + Gap + slot, n > 1 ? (slot + 3) / Mathf.Sin(Mathf.DegToRad(step) / 2) : 0);
        Above = core.Y + ring + slot + 24 > viewport.Y;
        float sign = Above ? -1 : 1;
        for (int i = 0; i < n; i++)
        {
            // From the left round the bottom to the right (or round the top, if above).
            float degrees = 90 + (n - 1) * step / 2 - i * step;
            var at = core + new Vector2(Mathf.Cos(Mathf.DegToRad(degrees)), sign * Mathf.Sin(Mathf.DegToRad(degrees))) * ring;
            var size = buttons[i].CustomMinimumSize;
            buttons[i].Position = (at - size / 2).Clamp(new Vector2(4, 4), viewport - size - new Vector2(4, 4));
            buttons[i].Size = size;
        }

        var hovered = buttons.FirstOrDefault(b => b.Hover) ?? buttons.FirstOrDefault(b => b.Tool == Labelled);
        label.Visible = hovered != null;
        if (hovered == null) return;
        label.Text = Slots[buttons.IndexOf(hovered)].Label;
        var labelSize = label.GetCombinedMinimumSize();
        var centre = hovered.Position + hovered.Size / 2;
        // Just outside the ring, straight out from the core.
        var outward = (centre - core).Normalized();
        var spot = centre + outward * (slot + 4 + labelSize.Y / 2) - labelSize / 2;
        if (Mathf.Abs(outward.X) > 0.6f) spot.X += outward.X * labelSize.X / 2;
        label.Position = spot.Clamp(new Vector2(4, 4), viewport - labelSize - new Vector2(4, 4));
        label.Size = labelSize;
    }
}
