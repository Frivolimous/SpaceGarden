using Godot;

namespace SpaceGarden;

// The Almanac (DESIGN.md, Step 4): the garden's journal of moments, newest first, each with the garden's age when it
// happened. Opened from its slot in the core's menu, it's a larger panel in the cards' fixed spot (they give way to it)
// and closes the way they do: a click off the plant, Escape, a tap on a part, or its slot again. No stats page and no
// catalogue: just the moments. The list scrolls under the wheel, so unlike the cards it takes the pointer.
public partial class Almanac : PanelContainer
{
    const double RefreshSeconds = 1;
    const float Width = 320, ListHeight = 300;

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

    public bool IsOpen { get; private set; }

    public Almanac()
    {
        MouseFilter = MouseFilterEnum.Stop; // a click on it stays on it, and the wheel scrolls it
        Visible = false;
        CustomMinimumSize = new Vector2(Width, 0);
        var style = Toolbar.PillStyle();
        style.SetCornerRadiusAll(10);
        style.ContentMarginLeft = style.ContentMarginRight = 12;
        style.ContentMarginTop = 6;
        style.ContentMarginBottom = 8;
        AddThemeStyleboxOverride("panel", style);
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 2);
        AddChild(column);
        column.AddChild(new Label
        {
            Text = "Almanac",
            MouseFilter = MouseFilterEnum.Ignore,
            LabelSettings = new LabelSettings { FontSize = 16, FontColor = Tint },
        });
        age = new Label { MouseFilter = MouseFilterEnum.Ignore, LabelSettings = new LabelSettings { FontSize = 12, FontColor = Dim } };
        column.AddChild(age);
        scroll = new ScrollContainer
        {
            MouseFilter = MouseFilterEnum.Pass,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, ListHeight),
        };
        column.AddChild(scroll);
        list = new VBoxContainer { MouseFilter = MouseFilterEnum.Pass, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(list);
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        refreshTimer = 0;
        scroll.ScrollVertical = 0; // the newest at the top
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
        fade.TweenProperty(this, "modulate:a", 0.0, 0.2);
        fade.TweenCallback(Callable.From(() => Visible = false));
    }

    // Whether the pointer is on it (in its layer's units).
    public bool Catches(Vector2 point) => Visible && GetGlobalRect().HasPoint(point);

    public void Update(Garden garden, double delta)
    {
        if (!IsOpen || (refreshTimer -= delta) > 0) return;
        refreshTimer = RefreshSeconds;
        var tracking = garden.Tracking;
        age.Text = $"Garden age {Tracking.AgeText(tracking.Age)}";
        if (tracking == shownFor && tracking.Journal.Count == shownEntries) return;
        shownFor = tracking;
        shownEntries = tracking.Journal.Count;
        foreach (var child in list.GetChildren()) child.QueueFree();
        for (int i = tracking.Journal.Count - 1; i >= 0; i--)
        {
            var entry = tracking.Journal[i];
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(new Label
            {
                Text = Tracking.AgeText(entry.Age),
                CustomMinimumSize = new Vector2(52, 0),
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
                CustomMinimumSize = new Vector2(Width - 100, 0),
                MouseFilter = MouseFilterEnum.Pass,
                LabelSettings = new LabelSettings { FontSize = 13, FontColor = entry.Kind == JournalKind.Unlock ? UnlockInk : Ink },
            });
            list.AddChild(row);
        }
    }
}
