using Godot;

namespace SpaceGarden;

// The Research panel (DESIGN.md, Upgrade tracks): a window of its own (see FloatingWindow), opened and closed by the
// Research slot in the core's menu or its ✕. Three cards, Stems, Generators and Core, each in its role's hue with a
// glyph of its part, showing only its track's next upgrade, its cost and a Buy button, greyed while the gene balance
// falls short; the balance shows in the header. A card the balance covers glows softly. Buying is the rules'
// (Garden.Buy): the widget does it on BuyPressed.
public partial class ResearchPanel : FloatingWindow
{
    public event Action<Track>? BuyPressed;

    const double RefreshSeconds = 0.25;
    const float CardWidth = 148;
    const float GlowSeconds = 2.4f; // one swell and fade of an affordable card's glow

    static readonly Color GeneInk = new(0.82f, 0.74f, 1f); // the genes' purple, lightened
    static readonly Color Back = new(0.06f, 0.07f, 0.09f); // the cards' dark ground, as the parts' fill

    sealed record TrackCard(Color Hue, StyleBoxFlat Style, TrackGlyph Glyph, Label Name, HBoxContainer Cost, Label Price,
        Button Buy)
    {
        public bool Affordable;
    }

    readonly Dictionary<Track, TrackCard> cards = new();
    double refreshTimer;
    float time;

    public ResearchPanel() : base("Research", "Close Research")
    {
        Note.LabelSettings.FontColor = GeneInk;
        Note.LabelSettings.FontSize = 14;
        Note.HorizontalAlignment = HorizontalAlignment.Right;
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        Column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 2), MouseFilter = MouseFilterEnum.Ignore });
        Column.AddChild(row);
        row.AddChild(Card(Track.Stems, "Stems", GardenView.Rgb(NodeSpecs.All[NodeKind.Stem].Rgb).Lightened(0.15f)));
        row.AddChild(Card(Track.Generators, "Generators", GardenView.Rgb(NodeSpecs.All[NodeKind.Generator].Rgb)));
        row.AddChild(Card(Track.Core, "Core", GeneInk.Darkened(0.05f)));
        Column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8), MouseFilter = MouseFilterEnum.Ignore });
    }

    // A track's card: its glyph and title, then its next upgrade, the cost and Buy.
    Control Card(Track track, string title, Color hue)
    {
        var style = new StyleBoxFlat
        {
            BgColor = Back.Lerp(hue, 0.07f),
            BorderColor = hue with { A = 0.3f },
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 12,
            ShadowColor = hue with { A = 0 },
            ShadowSize = 10,
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(12);
        var card = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(CardWidth, 0) };
        card.AddThemeStyleboxOverride("panel", style);
        var page = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        page.AddThemeConstantOverride("separation", 4);
        card.AddChild(page);

        var glyph = new TrackGlyph(track, hue) { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        page.AddChild(glyph);
        page.AddChild(Text(title.ToUpperInvariant(), 11, hue.Lightened(0.2f) with { A = 0.85f }));
        var line = new ColorRect { Color = hue with { A = 0.18f }, CustomMinimumSize = new Vector2(0, 1), MouseFilter = MouseFilterEnum.Ignore };
        page.AddChild(new Control { CustomMinimumSize = new Vector2(0, 2), MouseFilter = MouseFilterEnum.Ignore });
        page.AddChild(line);
        page.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4), MouseFilter = MouseFilterEnum.Ignore });
        var name = Text("", 16, Ink);
        name.MouseFilter = MouseFilterEnum.Stop; // for its tooltip
        page.AddChild(name);

        // The cost: a gene, then how many.
        var cost = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        cost.AddThemeConstantOverride("separation", 5);
        cost.AddChild(new GeneDot { SizeFlagsVertical = SizeFlags.ShrinkCenter });
        var price = Text("", 14, GeneInk);
        cost.AddChild(price);
        page.AddChild(cost);
        page.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6), MouseFilter = MouseFilterEnum.Ignore });

        var buy = new Button
        {
            Text = "Buy",
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(0, 28),
        };
        StyleBoxFlat Fill(Color colour)
        {
            var s = new StyleBoxFlat { BgColor = colour, ContentMarginTop = 3, ContentMarginBottom = 4 };
            s.SetCornerRadiusAll(14);
            return s;
        }
        buy.AddThemeStyleboxOverride("normal", Fill(hue with { A = 0.85f }));
        buy.AddThemeStyleboxOverride("hover", Fill(hue.Lightened(0.25f)));
        buy.AddThemeStyleboxOverride("pressed", Fill(hue.Lightened(0.45f)));
        var short_ = Fill(hue with { A = 0.07f });
        short_.BorderColor = hue with { A = 0.2f };
        short_.SetBorderWidthAll(1);
        buy.AddThemeStyleboxOverride("disabled", short_);
        buy.AddThemeFontSizeOverride("font_size", 13);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color" })
            buy.AddThemeColorOverride(state, Back);
        buy.AddThemeColorOverride("font_disabled_color", hue.Lightened(0.3f) with { A = 0.45f });
        buy.Pressed += () => BuyPressed?.Invoke(track);
        page.AddChild(buy);

        cards[track] = new TrackCard(hue, style, glyph, name, cost, price, buy);
        return card;
    }

    static Label Text(string text, int size, Color colour) => new()
    {
        Text = text,
        HorizontalAlignment = HorizontalAlignment.Center,
        MouseFilter = MouseFilterEnum.Ignore,
        LabelSettings = new LabelSettings { FontSize = size, FontColor = colour },
    };

    protected override void Opened() => refreshTimer = 0;

    // Shows at once: an upgrade just bought.
    public void RefreshNow() => refreshTimer = 0;

    // Every frame: follows the pointer while its header is held, keeps it on the screen, breathes the glow of the
    // cards the balance covers, and refreshes the cards a few times a second.
    public void Update(Garden garden, Vector2 pointer, Vector2 screen, double delta)
    {
        Place(pointer, screen, GetCombinedMinimumSize());
        if (!IsOpen) return;
        time = (time + (float)delta) % GlowSeconds;
        float swell = 0.5f - 0.5f * Mathf.Cos(time / GlowSeconds * Mathf.Tau);
        foreach (var card in cards.Values)
        {
            float glow = card.Affordable ? 0.35f + 0.45f * swell : 0;
            card.Style.BorderColor = card.Hue with { A = 0.3f + 0.5f * glow };
            card.Style.ShadowColor = card.Hue with { A = 0.22f * glow };
            card.Glyph.Glow = glow;
        }

        if ((refreshTimer -= delta) > 0) return;
        refreshTimer = RefreshSeconds;
        Note.Text = $"{Math.Floor(garden.GeneBalance):#,0} genes";
        foreach (var (track, card) in cards)
        {
            var next = garden.Next(track);
            card.Glyph.Done = next == null;
            card.Name.Text = next?.Name ?? "All bought";
            card.Name.TooltipText = next?.Does ?? "";
            card.Cost.Visible = card.Buy.Visible = next != null;
            card.Affordable = next != null && garden.CanSpend(next.Cost);
            if (next == null) continue;
            card.Price.Text = $"{next.Cost:#,0}";
            card.Buy.Disabled = !card.Affordable;
            card.Buy.Text = card.Affordable ? "Buy" : $"{Math.Ceiling(next.Cost - garden.GeneBalance):#,0} more";
            card.Buy.TooltipText = card.Affordable ? next.Does : "Not enough genes yet";
        }
    }
}

// A track's glyph, in its parts' outline look (dark fill, a crisp outline in the hue) over a soft halo: a stem's
// circle, a generator's pentagon, or the core, ringed. Glow (0 to 1) brightens the halo; Done ticks it.
public partial class TrackGlyph : Control
{
    readonly Track track;
    readonly Color hue;
    float glow;
    bool done;

    public float Glow
    {
        get => glow;
        set
        {
            if (Mathf.IsEqualApprox(value, glow)) return;
            glow = value;
            QueueRedraw();
        }
    }

    public bool Done
    {
        get => done;
        set
        {
            if (value == done) return;
            done = value;
            QueueRedraw();
        }
    }

    public TrackGlyph(Track track, Color hue)
    {
        this.track = track;
        this.hue = hue;
        CustomMinimumSize = new Vector2(52, 46);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        var c = Size / 2;
        // The halo: a few soft rings, brighter as it glows.
        for (int i = 4; i >= 1; i--)
            DrawCircle(c, 12 + i * 4, hue with { A = (0.025f + 0.05f * glow) * (5 - i) / 4 });
        var fill = new Color(0.06f, 0.07f, 0.09f);
        var line = done ? hue with { A = 0.5f } : hue;
        switch (track)
        {
            case Track.Stems:
                // A stem's three links (its parent's and two branches), each ending in a small bud.
                for (int i = 0; i < 3; i++)
                {
                    var u = Vector2.FromAngle(Mathf.Pi / 2 + i * Mathf.Tau / 3);
                    DrawLine(c + u * 9, c + u * 18, line with { A = line.A * 0.7f }, 1.5f, true);
                    DrawCircle(c + u * 20, 2.6f, fill);
                    DrawArc(c + u * 20, 2.6f, 0, Mathf.Tau, 12, line, 1.3f, true);
                }
                DrawCircle(c, 9, fill);
                DrawArc(c, 9, 0, Mathf.Tau, 40, line, 2, true);
                break;
            case Track.Generators:
                var points = new Vector2[6];
                for (int i = 0; i < 5; i++) points[i] = c + Vector2.FromAngle(-Mathf.Pi / 2 + i * Mathf.Tau / 5) * 14;
                points[5] = points[0];
                DrawColoredPolygon(points[..5], fill);
                DrawPolyline(points, line, 2, true);
                break;
            default:
                DrawCircle(c, 13, fill);
                DrawArc(c, 13, 0, Mathf.Tau, 48, line, 2, true);
                DrawArc(c, 18, 0, Mathf.Tau, 48, line with { A = line.A * 0.4f }, 1, true);
                DrawCircle(c, 3.5f, line);
                break;
        }
        if (done) DrawPolyline([c + new Vector2(-5, 0), c + new Vector2(-1.5f, 4), c + new Vector2(6, -5)], hue, 2.2f, true);
    }
}

// A gene, as the cost's unit: a small purple dot with a bright core.
public partial class GeneDot : Control
{
    public GeneDot()
    {
        CustomMinimumSize = new Vector2(10, 10);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        var c = Size / 2;
        DrawCircle(c, 5, new Color(0.58f, 0.44f, 0.88f, 0.35f));
        DrawCircle(c, 3.2f, new Color(0.75f, 0.62f, 1f));
        DrawCircle(c, 1.3f, new Color(1, 1, 1, 0.9f));
    }
}
