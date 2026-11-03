using Godot;

namespace SpaceGarden;

// The Research panel (DESIGN.md, Upgrade tracks): a window of its own (see FloatingWindow), opened and closed by the
// Research slot in the core's menu or its ✕. Three cards, Stems, Generators and Core, each in its role's hue with a
// glyph of its part, showing only its track's next upgrade, its cost and a Buy button, greyed while the gene balance
// falls short; the balance shows in the header, with the genes a second reaching the core. A card the balance covers
// glows softly; under the pointer, a Buy it doesn't cover says how long until it will at that rate. Buying is the
// rules' (Garden.Buy): the widget does it on BuyPressed.
public partial class ResearchPanel : FloatingWindow
{
    public event Action<Track>? BuyPressed;

    const double RefreshSeconds = 0.25;
    const float CardWidth = 148;
    const float GlowSeconds = 2.4f; // one swell and fade of an affordable card's glow

    static readonly Color GeneInk = new(0.82f, 0.74f, 1f); // the genes' purple, lightened
    static readonly Color CoreGrey = new(0.78f, 0.78f, 0.87f); // the Core card's hue: light grey
    static readonly Color Back = new(0.06f, 0.07f, 0.09f); // the cards' dark ground, as the parts' fill

    sealed record TrackCard(Color Hue, StyleBoxFlat Style, TrackGlyph Glyph, InfoMark Info, Label Name, HBoxContainer Cost,
        Label Price, Control BuyRow, BuyFill Fill, Button Buy)
    {
        public bool Affordable, Hovered;
    }

    readonly Dictionary<Track, TrackCard> cards = new();
    // The (i)'s hint, shown at once while the pointer is on one (a tooltip waits half a second), under it.
    readonly PanelContainer hint;
    readonly Label hintText;
    InfoMark? hinted;

    // Dev aid (--research=hint): a card whose hint shows as if the pointer were on its (i), for a screenshot.
    public Track? Hinting;
    double refreshTimer;
    float time;

    public ResearchPanel() : base("Research", "Close Research")
    {
        Note.LabelSettings.FontColor = GeneInk;
        Note.LabelSettings.FontSize = 14;
        Note.HorizontalAlignment = HorizontalAlignment.Right;
        hint = new PanelContainer { TopLevel = true, Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        var hintStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.05f, 0.07f),
            BorderColor = new Color(1, 1, 1, 0.18f),
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 6, ContentMarginBottom = 7,
        };
        hintStyle.SetBorderWidthAll(1);
        hintStyle.SetCornerRadiusAll(8);
        hint.AddThemeStyleboxOverride("panel", hintStyle);
        hintText = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(200, 0),
            LabelSettings = new LabelSettings { FontSize = 12, FontColor = Ink },
        };
        hint.AddChild(hintText);
        AddChild(hint);
        var row = full = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 0);
        var cardsRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        cardsRow.AddThemeConstantOverride("separation", 10);
        row.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6), MouseFilter = MouseFilterEnum.Ignore });
        row.AddChild(cardsRow);
        row.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12), MouseFilter = MouseFilterEnum.Ignore });
        Column.AddChild(row);
        var stemHue = GardenView.Rgb(NodeSpecs.All[NodeKind.Stem].Rgb).Lightened(0.15f);
        var generatorHue = GardenView.Rgb(NodeSpecs.All[NodeKind.Generator].Rgb);
        cardsRow.AddChild(Card(Track.Stems, "Stems", stemHue));
        cardsRow.AddChild(Card(Track.Generators, "Generators", generatorHue));
        cardsRow.AddChild(Card(Track.Core, "Core", CoreGrey));

        // Minimised (Jeremy, Nov 3): only the header (the genes) and a small Buy per track, saying how many genes it
        // still needs while it falls short.
        // (As wide as the cards' row, three equal Buys, so the panel keeps its width either way and as their text changes.)
        compact = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore, Visible = false, CustomMinimumSize = new Vector2(3 * CardWidth + 2 * 10, 32),
        };
        compact.AddThemeConstantOverride("separation", 6);
        compact.AddChild(SmallBuy(Track.Stems, stemHue));
        compact.AddChild(SmallBuy(Track.Generators, generatorHue));
        compact.AddChild(SmallBuy(Track.Core, CoreGrey));
        Column.AddChild(compact);
        minimise = HeaderButton("–", "Minimise");
        minimise.Pressed += () => SetMinimised(!Minimised);
    }

    readonly VBoxContainer full;
    readonly HBoxContainer compact;
    readonly Button minimise;
    readonly Dictionary<Track, (Button Buy, BuyFill Fill)> smallBuys = new();
    Track? smallHovered;

    public bool Minimised { get; private set; }

    public void SetMinimised(bool value)
    {
        Minimised = value;
        full.Visible = !value;
        compact.Visible = value;
        minimise.Text = value ? "□" : "–";
        minimise.TooltipText = value ? "Show the cards" : "Minimise";
        ShowHint(null);
        refreshTimer = 0;
        Size = Vector2.Zero; // shrinks to fit
    }

    // A track's small Buy, for the minimised panel: in its hue over a fill showing how close the balance is (as the
    // cards' Buy), dim while it falls short; under the pointer, how long until it won't.
    Control SmallBuy(Track track, Color hue)
    {
        // (A container, so it grows to fit the button's text and the fill sits behind it, the same size.)
        var holder = new MarginContainer
        {
            MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, 24), SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var fill = new BuyFill(hue);
        holder.AddChild(fill);
        var buy = new Button
        {
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            ClipText = true, // (its text never widens it)
        };
        holder.AddChild(buy);
        buy.MouseEntered += () => { smallHovered = track; refreshTimer = 0; };
        buy.MouseExited += () => { if (smallHovered == track) smallHovered = null; refreshTimer = 0; };
        StyleBoxFlat Fill(Color colour)
        {
            var s = new StyleBoxFlat { BgColor = colour, ContentMarginLeft = 8, ContentMarginRight = 8 };
            s.SetCornerRadiusAll(12);
            return s;
        }
        buy.AddThemeStyleboxOverride("normal", Fill(hue with { A = 0.85f }));
        buy.AddThemeStyleboxOverride("hover", Fill(hue.Lightened(0.25f)));
        buy.AddThemeStyleboxOverride("pressed", Fill(hue.Lightened(0.45f)));
        var short_ = Fill(Colors.Transparent);
        short_.BorderColor = hue with { A = 0.3f };
        short_.SetBorderWidthAll(1);
        buy.AddThemeStyleboxOverride("disabled", short_);
        buy.AddThemeFontSizeOverride("font_size", 11);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color" })
            buy.AddThemeColorOverride(state, Back);
        buy.AddThemeColorOverride("font_disabled_color", hue.Lightened(0.5f) with { A = 0.9f });
        buy.Pressed += () => BuyPressed?.Invoke(track);
        smallBuys[track] = (buy, fill);
        return holder;
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
        // The title, and beside it an (i) whose hint says what the next upgrade gives.
        var heading = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        heading.AddThemeConstantOverride("separation", 5);
        heading.AddChild(Text(title.ToUpperInvariant(), 11, hue.Lightened(0.2f) with { A = 0.85f }));
        var info = new InfoMark(hue) { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        info.MouseEntered += () => ShowHint(info);
        info.MouseExited += () => ShowHint(null);
        heading.AddChild(info);
        page.AddChild(heading);
        var line = new ColorRect { Color = hue with { A = 0.18f }, CustomMinimumSize = new Vector2(0, 1), MouseFilter = MouseFilterEnum.Ignore };
        page.AddChild(new Control { CustomMinimumSize = new Vector2(0, 2), MouseFilter = MouseFilterEnum.Ignore });
        page.AddChild(line);
        page.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4), MouseFilter = MouseFilterEnum.Ignore });
        var name = Text("", 16, Ink);
        page.AddChild(name);

        // The cost: a gene, then how many.
        var cost = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        cost.AddThemeConstantOverride("separation", 5);
        cost.AddChild(new GeneDot { SizeFlagsVertical = SizeFlags.ShrinkCenter });
        var price = Text("", 14, GeneInk);
        cost.AddChild(price);
        page.AddChild(cost);
        page.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6), MouseFilter = MouseFilterEnum.Ignore });

        // Buy, over a fill that grows left to right as the balance nears the cost (seen while it falls short).
        var buyRow = new Control { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, BuyFill.Height) };
        var fill = new BuyFill(hue);
        fill.SetAnchorsPreset(LayoutPreset.FullRect);
        buyRow.AddChild(fill);
        var buy = new Button
        {
            Text = "Buy",
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        buy.SetAnchorsPreset(LayoutPreset.FullRect);
        StyleBoxFlat Fill(Color colour)
        {
            var s = new StyleBoxFlat { BgColor = colour, ContentMarginTop = 3, ContentMarginBottom = 4 };
            s.SetCornerRadiusAll((int)BuyFill.Height / 2);
            return s;
        }
        buy.AddThemeStyleboxOverride("normal", Fill(hue with { A = 0.85f }));
        buy.AddThemeStyleboxOverride("hover", Fill(hue.Lightened(0.25f)));
        buy.AddThemeStyleboxOverride("pressed", Fill(hue.Lightened(0.45f)));
        var short_ = Fill(Colors.Transparent);
        short_.BorderColor = hue with { A = 0.25f };
        short_.SetBorderWidthAll(1);
        buy.AddThemeStyleboxOverride("disabled", short_);
        buy.AddThemeFontSizeOverride("font_size", 13);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color" })
            buy.AddThemeColorOverride(state, Back);
        buy.AddThemeColorOverride("font_disabled_color", hue.Lightened(0.5f) with { A = 0.9f });
        buy.Pressed += () => BuyPressed?.Invoke(track);
        buyRow.AddChild(buy);
        var beckon = new BeckonFrame();
        buyRow.AddChild(beckon);
        beckons[track] = beckon;
        page.AddChild(buyRow);

        var made = new TrackCard(hue, style, glyph, info, name, cost, price, buyRow, fill, buy);
        cards[track] = made;
        // Under the pointer, a Buy the balance falls short of shows how long until it won't (see Update).
        buy.MouseEntered += () => { made.Hovered = true; refreshTimer = 0; };
        buy.MouseExited += () => { made.Hovered = false; refreshTimer = 0; };
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

    // Shows an (i)'s hint at once, under it (or none).
    void ShowHint(InfoMark? mark)
    {
        hinted = mark;
        hint.Visible = mark != null;
        if (mark == null) return;
        hintText.Text = mark.Hint;
        hint.Size = Vector2.Zero; // shrinks to the new text
        PlaceHint(mark, GetViewportRect().Size);
    }

    // Centred under its (i), kept on the screen.
    void PlaceHint(InfoMark mark, Vector2 screen)
    {
        var size = hint.GetCombinedMinimumSize();
        var at = mark.GlobalPosition + new Vector2(mark.Size.X / 2 - size.X / 2, mark.Size.Y + 6);
        hint.GlobalPosition = at.Clamp(new Vector2(4, 4), (screen - size - new Vector2(4, 4)).Max(new Vector2(4, 4)));
    }

    // A small rate: one decimal under 10, whole above.
    static string PerSecond(double rate) => rate < 10 ? $"{rate:0.0}" : $"{rate:#,0}";

    // Shows at once: an upgrade just bought.
    public void RefreshNow() => refreshTimer = 0;

    // Every frame: follows the pointer while its header is held, keeps it on the screen, breathes the glow of the
    // cards the balance covers, and refreshes the cards a few times a second.
    // The tutorial's beckon on a track's Buy (Research Stems), or none.
    readonly Dictionary<Track, BeckonFrame> beckons = new();

    public void Beckon(Track? track)
    {
        foreach (var (t, frame) in beckons) frame.Visible = t == track;
    }

    public void Update(Garden garden, double growthSpeed, Vector2 pointer, Vector2 screen, double delta)
    {
        Place(pointer, screen, GetCombinedMinimumSize());
        if (!IsOpen || !Visible)
        {
            ShowHint(null);
            return;
        }
        if (Hinting is { } dev && hinted == null && cards[dev].Info.Hint != "") ShowHint(cards[dev].Info);
        if (hinted != null) PlaceHint(hinted, screen); // the window may be moving
        time =(time + (float)delta) % GlowSeconds;
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
        // Genes a real second, reaching the core now (the garden card's rate: GardenStats.GenesPerHour).
        double perSecond = GardenStats.Of(garden).GenesPerHour / 3600 * growthSpeed;
        Note.Text = $"{Math.Floor(garden.GeneBalance):#,0} genes  ({PerSecond(perSecond)}/s)";
        foreach (var (track, card) in cards)
        {
            var next = garden.Next(track);
            card.Glyph.Done = next == null;
            card.Name.Text = next?.Name ?? "All bought";
            card.Info.Hint = next?.Does ?? "Every upgrade on this track is bought.";
            if (hinted == card.Info) ShowHint(hinted); // just bought: the next one's hint
            card.Cost.Visible = card.BuyRow.Visible = next != null;
            card.Affordable = next != null && garden.CanSpend(next.Cost);
            if (next == null) continue;
            card.Price.Text = $"{next.Cost:#,0}";
            card.Buy.Disabled = !card.Affordable;
            double lacking = next.Cost - garden.GeneBalance;
            card.Buy.Text = card.Affordable ? "Buy"
                : !card.Hovered ? $"{Math.Ceiling(lacking):#,0} more"
                : perSecond > 0 ? $"in {SpaceGarden.Card.Eta(lacking / perSecond)}" : "no genes coming";
            card.Fill.Progress = card.Affordable ? 1 : (float)Math.Clamp(garden.GeneBalance / next.Cost, 0, 1);
            card.Fill.Visible = !card.Affordable;
        }
        foreach (var (track, (buy, fill)) in smallBuys)
        {
            var next = garden.Next(track);
            string name = track switch { Track.Stems => "Stem", Track.Generators => "Generator", _ => "Core" };
            buy.Disabled = next == null || !garden.CanSpend(next.Cost);
            double lacking = next == null ? 0 : next.Cost - garden.GeneBalance;
            buy.Text = next == null ? $"{name}: done" : !buy.Disabled ? $"{name}: Buy"
                : smallHovered == track ? (perSecond > 0 ? $"{name}: in {SpaceGarden.Card.Eta(lacking / perSecond)}" : $"{name}: no genes coming")
                : $"{name}: {Math.Ceiling(lacking):#,0} more";
            fill.Visible = next != null && buy.Disabled;
            fill.Progress = next == null ? 1 : (float)Math.Clamp(garden.GeneBalance / next.Cost, 0, 1);
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

// Behind a Buy button the balance falls short of: the button's track, dim, and over it a fill in the hue growing left
// to right with Progress (the balance against the cost), so how close the upgrade is reads at a glance.
public partial class BuyFill : Control
{
    public const float Height = 28;

    readonly Color hue;
    float progress;

    public float Progress
    {
        get => progress;
        set
        {
            if (Mathf.IsEqualApprox(value, progress)) return;
            progress = value;
            QueueRedraw();
        }
    }

    public BuyFill(Color hue)
    {
        this.hue = hue;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        Bar(Size.X, hue with { A = 0.07f });
        // Never narrower than its own height, so its rounded ends stay round; past that it grows with the progress.
        if (progress > 0) Bar(Mathf.Lerp(Size.Y, Size.X, progress), hue with { A = 0.32f });
    }

    void Bar(float width, Color colour)
    {
        var box = new StyleBoxFlat { BgColor = colour };
        box.SetCornerRadiusAll((int)(Size.Y / 2));
        DrawStyleBox(box, new Rect2(0, 0, width, Size.Y));
    }
}

// A small (i) in a ring, in a card's hue: under the pointer, the panel shows its Hint, what the track's next
// upgrade gives.
public partial class InfoMark : Control
{
    readonly Color hue;

    // What its next upgrade gives, shown at once in the panel's hint while the pointer is on it.
    public string Hint = "";

    public InfoMark(Color hue)
    {
        this.hue = hue;
        CustomMinimumSize = new Vector2(13, 13);
        MouseFilter = MouseFilterEnum.Stop; // for its hint
        MouseDefaultCursorShape = CursorShape.Help;
        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
    }

    public override void _Draw()
    {
        var c = Size / 2;
        bool hovered = GetGlobalRect().HasPoint(GetGlobalMousePosition());
        var ink = hue.Lightened(0.3f) with { A = hovered ? 1 : 0.6f };
        DrawArc(c, 5.8f, 0, Mathf.Tau, 24, ink, 1.1f, true);
        DrawCircle(c + new Vector2(0, -2.6f), 0.9f, ink);
        DrawLine(c + new Vector2(0, -0.6f), c + new Vector2(0, 3.2f), ink, 1.3f, true);
    }
}
