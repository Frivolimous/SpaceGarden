using Godot;

namespace SpaceGarden;

// A few quiet lines under the plant in Active mode, so the garden can be read without a menu: its power (made and
// used an hour, how full it is), its genes and what the next unlock gives, and how much room is left. Each line is
// tinted by its role's hue (DESIGN.md, Visual language). While the last prune can be undone, an Undo pill sits above
// them, counting down. The lines show only while the pointer rests on the plant (TextAlpha, faded by the widget);
// the Undo pill stays while it can be used.
public partial class Readout : VBoxContainer
{
    public event Action? UndoPressed;

    const double RefreshSeconds = 0.25;

    static readonly Color PowerTint = new(1f, 0.96f, 0.62f);
    static readonly Color GeneTint = new(0.82f, 0.74f, 1f);
    static readonly Color RoomTint = new(0.78f, 0.95f, 0.78f);

    readonly Label power, genes, room, hint, undoText;
    readonly Label[] lines;
    readonly PanelContainer undo;
    double refreshTimer;

    public Readout()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        AddThemeConstantOverride("separation", 0);

        undo = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, Visible = false, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        var style = Toolbar.PillStyle();
        style.ContentMarginLeft = style.ContentMarginRight = 12;
        style.ContentMarginTop = style.ContentMarginBottom = 4;
        undo.AddThemeStyleboxOverride("panel", style);
        undoText = new Label { LabelSettings = new LabelSettings { FontSize = 13 }, MouseFilter = MouseFilterEnum.Ignore };
        undo.AddChild(undoText);
        undo.TooltipText = "Put the last pruned branch back (Ctrl+Z)";
        undo.MouseEntered += () => undo.Modulate = new Color(1.25f, 1.25f, 1.25f);
        undo.MouseExited += () => undo.Modulate = Colors.White;
        undo.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;
            undo.AcceptEvent();
            UndoPressed?.Invoke();
        };
        AddChild(undo);
        AddChild(new Control { CustomMinimumSize = new Vector2(0, 6), MouseFilter = MouseFilterEnum.Ignore });

        power = Line(PowerTint);
        genes = Line(GeneTint);
        room = Line(RoomTint);
        hint = Line(new Color(1, 1, 1, 0.7f));
        lines = [power, genes, room, hint];
        TextAlpha = 0;
    }

    Label Line(Color tint)
    {
        var label = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
            LabelSettings = new LabelSettings { FontSize = 13, FontColor = tint with { A = 0.9f }, OutlineSize = 4, OutlineColor = new Color(0, 0, 0, 0.6f) },
        };
        AddChild(label);
        return label;
    }

    // Whether the pointer is over the part that takes clicks (the Undo pill), in the readout's layer's units.
    public bool Catches(Vector2 point) => Visible && undo.Visible && undo.GetGlobalRect().Grow(4).HasPoint(point);

    // Whether the pointer is over the lines while they show (so they stay while being read).
    public bool Over(Vector2 point) => Visible && TextAlpha > 0 && GetGlobalRect().Grow(6).HasPoint(point);

    // How much the lines show, 0-1.
    public float TextAlpha
    {
        get => power.Modulate.A;
        set
        {
            foreach (var line in lines) line.Modulate = new Color(1, 1, 1, value);
        }
    }

    public void Update(Garden garden, double delta)
    {
        if ((refreshTimer -= delta) > 0) return;
        refreshTimer = RefreshSeconds;

        var (made, used) = garden.PowerBalance();
        float held = 0, store = 0;
        int wilting = 0, stems = 0, generators = 0, labs = 0;
        foreach (var n in garden.Nodes)
        {
            if (n.Spec.IsFruit) continue;
            held += n.Power;
            store += n.Store;
            if (n.Wilting) wilting++;
            if (n.Spec.Kind == NodeKind.Stem) stems++;
            else if (n.Spec.Kind == NodeKind.Generator) generators++;
            else if (n.Spec.Kind == NodeKind.Lab) labs++;
        }
        power.Text = $"Power {made:0.#} an hour, {used:0.#} used · {held / store * 100:0}% full"
            + (wilting > 0 ? $" · {wilting} wilting" : "");

        genes.Text = garden.NextUnlock is { } next
            ? $"Genes {garden.Genes:#,0} of {next.Genes:#,0} for {Gives(next)}"
            : $"Genes {garden.Genes:#,0} · every unlock reached";

        var caps = garden.Capacity;
        hint.Text = Hotkey.Registered ? $"{Hotkey.Name} fades the garden into the background" : "";
        room.Text = $"Stems {stems}{Of(caps.Stems)} · generators {generators}{Of(caps.Generators)} · labs {labs}";

        bool canUndo = garden.CanUndoPrune;
        undo.Visible = canUndo;
        if (canUndo) undoText.Text = $"Undo prune  {Math.Ceiling(garden.UndoSecondsLeft):0} s";
    }

    // Shows at once: the readout appearing, or the Undo pill after a prune.
    public void RefreshNow() => refreshTimer = 0;

    static string Of(int cap) => cap == int.MaxValue ? "" : $"/{cap}";

    static string Gives(UnlockStep step)
    {
        var parts = new List<string>();
        if (step.Stems > 0) parts.Add($"+{step.Stems} stem{(step.Stems == 1 ? "" : "s")}");
        if (step.Generators > 0) parts.Add($"+{step.Generators} generator{(step.Generators == 1 ? "" : "s")}");
        if (step.CoreLinks > 0) parts.Add($"+{step.CoreLinks} core branch{(step.CoreLinks == 1 ? "" : "es")}");
        return string.Join(", ", parts);
    }
}
