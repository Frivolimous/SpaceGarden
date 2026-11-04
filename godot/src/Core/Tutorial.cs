namespace SpaceGarden.Core;

// The opening's tutorial (Jeremy's script, Nov 3), on a new garden only (an older save has none). One step at a time,
// each passed once it's done: by a check on the garden (a bud started, a part out, the core in a pot, an upgrade
// affordable or bought), or by the player (Continue, Research opened: see Act). It keeps the player on the rails:
// until the lab is planted only the step's own action is open (see Allows). No skip.
// (Grove, Stem, Lab, Pot, Wait, Research and Done keep their names from the first build, whose saves hold them.)
public enum TutorialStep { Welcome, Grove, GroveWait, Stem, StemWait, Lab, Pot, Tools, Genes, Wait, Research, Done }

// What the player tries that a step may hold back.
public enum TutorialAction { Bud, CancelBud, Prune, Build, Research, Shaping, MoveGarden, Tie }

public static class Tutorial
{
    // The header's pips: a step and its wait share one (Grove and Wait for the Grove; both Tending Mode steps; both
    // Wait for Genes steps), and the welcome and the send-off have none (Jeremy, Nov 3).
    public const int Pips = 6;

    // The pip a step lights: 0 to Pips - 1, or -1 before the first (the welcome), Pips after the last (the send-off).
    public static int Pip(TutorialStep step) => step switch
    {
        TutorialStep.Welcome => -1,
        TutorialStep.Grove or TutorialStep.GroveWait => 0,
        TutorialStep.Stem or TutorialStep.StemWait => 1,
        TutorialStep.Lab => 2,
        TutorialStep.Pot or TutorialStep.Tools => 3,
        TutorialStep.Genes or TutorialStep.Wait => 4,
        TutorialStep.Research => 5,
        _ => Pips,
    };

    public static string Title(TutorialStep step) => step switch
    {
        TutorialStep.Welcome => "Welcome to Space Garden!",
        TutorialStep.Grove => "Grow a grove",
        TutorialStep.GroveWait => "Wait for the grove",
        TutorialStep.Stem => "Grow a stem",
        TutorialStep.StemWait => "Wait for the stem",
        TutorialStep.Lab => "Grow a lab",
        TutorialStep.Pot or TutorialStep.Tools => "Tending mode",
        TutorialStep.Genes or TutorialStep.Wait => "Wait for genes",
        TutorialStep.Research => "Expand your stems",
        _ => "You're on your own!",
    };

    // The step's text. A mark ("{Growth}", "{Genes}") is the UI's to draw: a dot in that blob's colour.
    public static string Text(TutorialStep step) => step switch
    {
        TutorialStep.Welcome => "Guide your garden's growth and watch it bloom, expand and flourish.",
        TutorialStep.Grove or TutorialStep.GroveWait => "Groves send growth {Growth} that helps your garden grow faster.",
        TutorialStep.Stem or TutorialStep.StemWait => "Stems give you more space to grow flowers, and carry power between them.",
        TutorialStep.Lab => "Labs send genes {Genes} to the core, which are used to buy upgrades.",
        TutorialStep.Pot => "While you wait, let's arrange your garden! Open tending mode from the core's menu and set your core in a Gravity Pot.",
        TutorialStep.Tools => "Tending tools are just used to arrange your garden and don't have any other in-game effect. You'll unlock more tools as your garden grows.",
        TutorialStep.Genes => "Labs send genes {Genes} that are absorbed by the core. When you have enough, you can start upgrading your garden.",
        TutorialStep.Wait => "Explore your options and keep growing your garden while your genes accumulate!",
        TutorialStep.Research => "This is how your garden grows!",
        _ => "You're doing great! Keep growing your garden and soon you'll unlock the next layer.",
    };

    // The kind of part a step is about (its title's mark, and the bud it asks for), if any.
    public static NodeKind? Kind(TutorialStep step) => step switch
    {
        TutorialStep.Grove or TutorialStep.GroveWait => NodeKind.Grove,
        TutorialStep.Stem or TutorialStep.StemWait => NodeKind.Stem,
        TutorialStep.Lab => NodeKind.Lab,
        _ => null,
    };

    // Whether the step asks the player to start a bud (Grove, Stem, Lab), not to wait for one.
    public static bool AsksForBud(TutorialStep step) => step is TutorialStep.Grove or TutorialStep.Stem or TutorialStep.Lab;

    // Whether the step waits for the player (Continue, or opening Research) rather than a check on the garden.
    // (Tools: build mode closed.)
    public static bool ByAct(TutorialStep step) => step is TutorialStep.Welcome or TutorialStep.Tools or TutorialStep.Genes or TutorialStep.Done;

    // Whether the step is done in this garden (a step passed by the player never is: see Act).
    public static bool Done(Garden garden, TutorialStep step) => step switch
    {
        TutorialStep.Grove => Count(garden, NodeKind.Grove, buds: true) >= 1,
        TutorialStep.GroveWait => Count(garden, NodeKind.Grove, buds: false) >= 1,
        TutorialStep.Stem => Count(garden, NodeKind.Stem, buds: true) >= 2,
        TutorialStep.StemWait => Count(garden, NodeKind.Stem, buds: false) >= 2,
        TutorialStep.Lab => Count(garden, NodeKind.Lab, buds: true) >= 1,
        TutorialStep.Pot => garden.Core.TiedTo is { Kind: ShapingKind.Pot },
        TutorialStep.Wait => garden.Next(Track.Stems) is { } stem && garden.GeneBalance >= stem.Cost || garden.StemsBought > 0,
        TutorialStep.Research => garden.StemsBought > 0,
        _ => false,
    };

    // Whether a step holds this back. Through the lab: only the step's own bud (none while waiting for one, or at the
    // welcome), no cancelling, no pruning, no build mode, no Research. Tending Mode (both its steps): build mode for the pot alone (no
    // other tool, no moving the garden, only the core potted), still no pruning. From then on, nothing.
    public static bool Allows(TutorialStep? step, TutorialAction action, NodeKind? kind = null, ShapingKind? shaping = null)
    {
        if (step is not { } s || s > TutorialStep.Tools) return true;
        if (s is TutorialStep.Pot or TutorialStep.Tools)
            return action switch
            {
                TutorialAction.Prune or TutorialAction.MoveGarden => false,
                TutorialAction.Shaping => shaping == ShapingKind.Pot,
                TutorialAction.Tie => kind == NodeKind.Core, // (the core it asks for)
                _ => true,
            };
        return action == TutorialAction.Bud && AsksForBud(s) && kind == Kind(s);
    }

    // Where the step's bud can be started now: a stem (the core too, for a stem) with a free link and no bud forming,
    // the furthest out first (the lab goes on the new stem), the newest among equals. Null if none can.
    public static PlantNode? Spot(Garden garden, NodeKind kind) => garden.Nodes
        .Where(n => (Garden.IsBudOf(n.Spec.Kind, NodeKind.Stem) || (kind == NodeKind.Stem && n == garden.Core))
            && !n.FormingBud && garden.CanSproutFrom(n, kind))
        .Select((n, i) => (n, i)).OrderByDescending(p => p.n.Depth).ThenByDescending(p => p.i)
        .Select(p => p.n).FirstOrDefault();

    // The part where the step's bud is forming, if one is (a wild stand-in too).
    public static PlantNode? Forming(Garden garden, NodeKind kind) =>
        garden.Nodes.LastOrDefault(n => n.FormingBud && Garden.IsBudOf(n.Forming!.Value, kind));

    // Parts of the kind (or a wild flower standing in for it), and with buds, those forming as a bud too.
    static int Count(Garden garden, NodeKind kind, bool buds) =>
        garden.Nodes.Count(n => Garden.IsBudOf(n.Spec.Kind, kind))
        + (buds ? garden.Nodes.Count(n => n.FormingBud && Garden.IsBudOf(n.Forming!.Value, kind)) : 0);
}
