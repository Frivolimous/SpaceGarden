using Godot;

namespace SpaceGarden;

// A seedling's choice (DESIGN.md, Tending commands: "Select evolution"): once it has taken in 200, tapping it opens a
// half ring of round buttons under it (over it if there's no room below), in the core menu's look, one for each
// evolution it offers, each showing the shape of the flower it blooms into, in its role hue. The one under the pointer
// shows a small card beside the ring: its name, what it does, and the wild flower it blooms into. A pick chooses it;
// another tap, a tap elsewhere or Escape closes it (the widget sees to those).
public partial class EvolutionMenu : Control
{
    public event Action<EvolutionKind>? Picked;

    const float Gap = 12; // between the seedling's edge and the slots' inner edge
    const float Step = 44; // degrees between neighbouring slots

    readonly List<EvolutionButton> buttons = new();
    readonly PanelContainer card;
    readonly Label name, does, becomes;
    Tween? fade;

    public bool IsOpen { get; private set; }
    // Whether it opened over the seedling (no room below): its toolbar goes below it then.
    public bool Above { get; private set; }
    public PlantNode? Seedling { get; private set; }
    // Dev aid (--tap=seedling): the slot whose card shows as if the pointer were on it, for a screenshot.
    public int? Labelled;

    public EvolutionMenu()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        var style = Toolbar.PillStyle();
        style.SetCornerRadiusAll(10);
        style.ContentMarginLeft = style.ContentMarginRight = 10;
        style.ContentMarginTop = 5;
        style.ContentMarginBottom = 7;
        card = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        card.AddThemeStyleboxOverride("panel", style);
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 0);
        card.AddChild(column);
        Label Line(int size, Color colour)
        {
            var label = new Label
            {
                MouseFilter = MouseFilterEnum.Ignore,
                LabelSettings = new LabelSettings { FontSize = size, FontColor = colour },
            };
            column.AddChild(label);
            return label;
        }
        name = Line(15, Colors.White);
        does = Line(13, new Color(1, 1, 1, 0.9f));
        becomes = Line(12, new Color(1, 1, 1, 0.6f));
        AddChild(card);
    }

    // Opens on a seedling waiting for its choice, with a slot for each evolution it offers.
    public void Open(Garden garden, PlantNode seedling)
    {
        Seedling = seedling;
        foreach (var b in buttons) b.QueueFree();
        buttons.Clear();
        foreach (var kind in seedling.Offers ?? [])
        {
            var button = new EvolutionButton(kind, garden.Taken(kind) + 1);
            button.Pressed += () => Picked?.Invoke(kind);
            buttons.Add(button);
            AddChild(button);
        }
        MoveChild(card, -1);
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
        Seedling = null;
        Labelled = null;
        fade?.Kill();
        fade = CreateTween();
        fade.TweenProperty(this, "modulate:a", 0.0, 0.15);
        fade.TweenCallback(Callable.From(() => Visible = false));
    }

    // Whether the pointer is on one of its slots (in its layer's units).
    public bool Over(Vector2 point) => IsOpen && buttons.Any(b => b.GetGlobalRect().Grow(2).HasPoint(point));

    // Lays the slots out round the seedling, below it if they fit there, and shows the card of the one under the
    // pointer.
    public void Follow(Vector2 at, float radius, Vector2 viewport)
    {
        int n = buttons.Count;
        float slot = EvolutionButton.Radius;
        float ring = Mathf.Max(radius + Gap + slot, n > 1 ? (slot + 3) / Mathf.Sin(Mathf.DegToRad(Step) / 2) : 0);
        // Below if the slots and the card beyond them fit there.
        bool above = Above = at.Y + ring + slot + 8 + card.GetCombinedMinimumSize().Y + 8 > viewport.Y;
        float sign = above ? -1 : 1;
        for (int i = 0; i < n; i++)
        {
            float degrees = 90 + (n - 1) * Step / 2 - i * Step;
            var spot = at + new Vector2(Mathf.Cos(Mathf.DegToRad(degrees)), sign * Mathf.Sin(Mathf.DegToRad(degrees))) * ring;
            var size = buttons[i].CustomMinimumSize;
            buttons[i].Position = (spot - size / 2).Clamp(new Vector2(4, 4), viewport - size - new Vector2(4, 4));
            buttons[i].Size = size;
        }

        var hovered = buttons.FirstOrDefault(b => b.Hover)
            ?? (Labelled is { } l && l < buttons.Count ? buttons[l] : null);
        card.Visible = hovered != null;
        if (hovered == null) return;
        var spec = Evolutions.All[hovered.Kind];
        name.Text = Evolutions.Name(hovered.Kind, hovered.Level);
        name.LabelSettings.FontColor = GardenView.Rgb(spec.Rgb).Lightened(0.3f);
        does.Text = spec.Effect.At(hovered.Level).Text; // in all, at that level (a repeat adds level I's again)
        string flower = NodeSpecs.All[spec.Becomes].Name;
        becomes.Text = $"Blooms into {Tracking.Article(flower).ToLowerInvariant()} {flower}"
            + (spec.Unlocks && hovered.Level == 1 ? $"; {flower}s can then grow wild" : "");
        var cardSize = card.GetCombinedMinimumSize();
        // Beyond the ring, on the far side from the seedling.
        var centre = hovered.Position + hovered.Size / 2;
        var place = new Vector2(centre.X - cardSize.X / 2, above ? centre.Y - slot - 8 - cardSize.Y : centre.Y + slot + 8);
        card.Position = place.Clamp(new Vector2(4, 4), viewport - cardSize - new Vector2(4, 4));
        card.Size = cardSize;
    }
}

// One evolution's slot: a disc in the core menu's look, the shape of its flower in its role hue.
public partial class EvolutionButton : Control
{
    public event Action? Pressed;
    public readonly EvolutionKind Kind;
    public readonly int Level;
    public bool Hover { get; private set; }

    public const float Radius = 18;
    static readonly Color DiscFill = new(0.07f, 0.09f, 0.13f, 0.92f);

    public EvolutionButton(EvolutionKind kind, int level)
    {
        Kind = kind;
        Level = level;
        CustomMinimumSize = new Vector2(Radius * 2 + 2, Radius * 2 + 2);
        MouseFilter = MouseFilterEnum.Stop;
        MouseEntered += () => { Hover = true; QueueRedraw(); };
        MouseExited += () => { Hover = false; QueueRedraw(); };
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;
        AcceptEvent();
        Pressed?.Invoke();
    }

    public override void _Draw()
    {
        var c = Size / 2;
        var spec = Evolutions.All[Kind];
        var hue = GardenView.Rgb(spec.Rgb);
        DrawCircle(c, Radius + 1, new Color(0, 0, 0, 0.25f));
        DrawCircle(c, Radius, DiscFill);
        DrawArc(c, Radius, 0, Mathf.Tau, 40, hue with { A = Hover ? 0.75f : 0.35f }, Hover ? 1.6f : 1, true);
        ToolButton.DrawShape(this, c, NodeSpecs.All[spec.Becomes].Shape, 9, hue);
        // A repeat's level, small in the corner.
        if (Level > 1)
            DrawString(ThemeDB.FallbackFont, c + new Vector2(7, 15), Level == 2 ? "II" : Level == 3 ? "III" : $"{Level}",
                HorizontalAlignment.Left, -1, 10, new Color(1, 1, 1, 0.75f));
    }
}
