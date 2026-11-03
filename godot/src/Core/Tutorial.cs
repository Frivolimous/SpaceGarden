namespace SpaceGarden.Core;

// The opening's tutorial (Jeremy, Nov 3): one step at a time, each shown once the last is done, on a new garden only
// (a garden from an older save has none). Every step is a check on the garden itself, so a step done early (a lab
// grown before it was asked for, an upgrade bought while waiting) is passed straight away. No skip: every step is
// something the player needs anyway. After Research, Done shows a last word and the UI then ends it (Finish).
public enum TutorialStep { Grove, Stem, Lab, Pot, Wait, Research, Done }

public static class Tutorial
{
    // The steps that count towards "Step n of N" (Done is the send-off, not a step).
    public const int Steps = (int)TutorialStep.Done;

    public static string Text(TutorialStep step) => step switch
    {
        TutorialStep.Grove => "Plant a grove: on the stem, pick the orange square. Groves send growth (orange blobs) that grows your garden.",
        TutorialStep.Stem => "Once the grove is out, grow a new stem the same way: the green circle. Stems carry power and make room for more parts.",
        TutorialStep.Lab => "On the new stem, grow a lab: the purple hexagon. Labs make genes, which buy upgrades.",
        TutorialStep.Pot => "Open Build mode from the core's menu and drag your Gravity Pot onto a part. It holds that part where you put it.",
        TutorialStep.Wait => "Genes are on their way to the core. Research lights up when there are enough. Grow more meanwhile, or just watch.",
        TutorialStep.Research => "Research is ready! Open it from the core's menu and buy your first upgrade.",
        _ => "Keep going, you're doing great! You're on your own from here!",
    };

    // Whether the step is done in this garden.
    public static bool Done(Garden garden, TutorialStep step) => step switch
    {
        TutorialStep.Grove => Count(garden, NodeKind.Grove) >= 1,
        TutorialStep.Stem => Count(garden, NodeKind.Stem) >= 2,
        TutorialStep.Lab => Count(garden, NodeKind.Lab) >= 1,
        TutorialStep.Pot => garden.Nodes.Any(n => n.TiedTo is { Kind: ShapingKind.Pot }),
        TutorialStep.Wait => garden.UpgradeAffordable || garden.Bought > 0,
        TutorialStep.Research => garden.Bought > 0,
        _ => false,
    };

    // Parts of the kind (or a wild flower standing in for it), grown or forming as a bud: a bud started counts, as the
    // step asked for it and it grows on its own.
    static int Count(Garden garden, NodeKind kind) =>
        garden.Nodes.Count(n => Garden.IsBudOf(n.Spec.Kind, kind))
        + garden.Nodes.Count(n => n.FormingBud && Garden.IsBudOf(n.Forming!.Value, kind));
}
