using System.Numerics;

namespace SpaceGarden.Core.Tests;

// The opening's tutorial (Jeremy's script, Nov 3): welcome, grove, its wait, stem, its wait, lab, Tending Mode (the core
// in a pot), Research opened, waiting for the first stem upgrade, buying it, then the send-off.
public class TutorialTests
{
    // A new garden as the game starts one: the core, its first stem, and the tutorial.
    static (Garden Garden, PlantNode Stem) Opening()
    {
        var garden = Garden.New(Layout.Radial, seed: 7);
        var stem = garden.GrowBranch(garden.Core, NodeKind.Stem)!;
        garden.StartTutorial();
        return (garden, stem);
    }

    static void GrowUntil(Garden garden, Func<bool> done)
    {
        for (int i = 0; i < 2000 && !done(); i++)
        {
            foreach (var n in garden.Nodes) if (!n.Spec.IsFruit) n.Power = n.Store;
            garden.Grow(Tuning.EconomyStepSeconds);
        }
        Assert.True(done());
    }

    [Fact]
    public void ANewGardenWalksTheScriptInOrder()
    {
        var (garden, stem) = Opening();
        Assert.Equal(TutorialStep.Welcome, garden.Tutorial);
        Assert.False(garden.AdvanceTutorial()); // the welcome waits for Continue
        Assert.True(garden.ActTutorial(TutorialStep.Welcome));
        Assert.Equal(TutorialStep.Grove, garden.Tutorial);

        Assert.True(garden.SproutFrom(stem, NodeKind.Grove));
        garden.AdvanceTutorial();
        Assert.Equal(TutorialStep.GroveWait, garden.Tutorial);
        GrowUntil(garden, () => !stem.FormingBud);
        garden.AdvanceTutorial();
        Assert.Equal(TutorialStep.Stem, garden.Tutorial);

        Assert.True(garden.SproutFrom(stem, NodeKind.Stem));
        garden.AdvanceTutorial();
        Assert.Equal(TutorialStep.StemWait, garden.Tutorial);
        GrowUntil(garden, () => !stem.FormingBud);
        garden.AdvanceTutorial();
        Assert.Equal(TutorialStep.Lab, garden.Tutorial);

        var second = garden.Nodes.Last(n => n.Spec.Kind == NodeKind.Stem || Garden.IsBudOf(n.Spec.Kind, NodeKind.Stem));
        Assert.Same(second, Tutorial.Spot(garden, NodeKind.Lab)); // the lab goes on the new stem
        Assert.True(garden.SproutFrom(second, NodeKind.Lab));
        garden.AdvanceTutorial();
        Assert.Equal(TutorialStep.Pot, garden.Tutorial); // (no waiting for the lab)

        var pot = garden.Place(ShapingKind.Pot, new Vector2(0, 40))!;
        garden.Tie(stem, pot);
        Assert.False(garden.AdvanceTutorial()); // the core it asks for
        garden.Tie(garden.Core, pot);
        garden.AdvanceTutorial();
        Assert.Equal(TutorialStep.Tools, garden.Tutorial);
        Assert.False(garden.AdvanceTutorial()); // waits for build mode to close
        Assert.True(garden.ActTutorial(TutorialStep.Tools));
        Assert.Equal(TutorialStep.Genes, garden.Tutorial);

        Assert.False(garden.ActTutorial(TutorialStep.Welcome)); // only the step's own act
        Assert.True(garden.ActTutorial(TutorialStep.Genes)); // Research opened
        Assert.Equal(TutorialStep.Wait, garden.Tutorial);

        garden.GeneBalance = garden.Next(Track.Stems)!.Cost;
        garden.AdvanceTutorial();
        Assert.Equal(TutorialStep.Research, garden.Tutorial);
        Assert.True(garden.Buy(Track.Stems));
        garden.AdvanceTutorial();
        Assert.Equal(TutorialStep.Done, garden.Tutorial);
        Assert.True(garden.ActTutorial(TutorialStep.Done));
        Assert.Null(garden.Tutorial);
    }

    [Fact]
    public void ItKeepsThePlayerOnTheRails()
    {
        Assert.False(Tutorial.Allows(TutorialStep.Welcome, TutorialAction.Bud, NodeKind.Grove));
        Assert.True(Tutorial.Allows(TutorialStep.Grove, TutorialAction.Bud, NodeKind.Grove));
        Assert.False(Tutorial.Allows(TutorialStep.Grove, TutorialAction.Bud, NodeKind.Lab));
        Assert.False(Tutorial.Allows(TutorialStep.GroveWait, TutorialAction.Bud, NodeKind.Grove));
        Assert.False(Tutorial.Allows(TutorialStep.GroveWait, TutorialAction.CancelBud));
        foreach (var step in new[] { TutorialStep.Welcome, TutorialStep.Grove, TutorialStep.StemWait, TutorialStep.Lab })
        {
            Assert.False(Tutorial.Allows(step, TutorialAction.Prune));
            Assert.False(Tutorial.Allows(step, TutorialAction.Build));
            Assert.False(Tutorial.Allows(step, TutorialAction.Research));
        }
        // Tending Mode: build mode for the pot alone; buds and Research open again; still no pruning.
        Assert.True(Tutorial.Allows(TutorialStep.Pot, TutorialAction.Build));
        Assert.True(Tutorial.Allows(TutorialStep.Pot, TutorialAction.Shaping, shaping: ShapingKind.Pot));
        Assert.False(Tutorial.Allows(TutorialStep.Pot, TutorialAction.Shaping, shaping: ShapingKind.Trellis));
        Assert.False(Tutorial.Allows(TutorialStep.Pot, TutorialAction.MoveGarden));
        Assert.False(Tutorial.Allows(TutorialStep.Tools, TutorialAction.Shaping, shaping: ShapingKind.Well));
        Assert.False(Tutorial.Allows(TutorialStep.Tools, TutorialAction.Prune));
        Assert.False(Tutorial.Allows(TutorialStep.Pot, TutorialAction.Prune));
        Assert.True(Tutorial.Allows(TutorialStep.Pot, TutorialAction.Tie, NodeKind.Core));
        Assert.False(Tutorial.Allows(TutorialStep.Pot, TutorialAction.Tie, NodeKind.Stem));
        Assert.True(Tutorial.Allows(TutorialStep.Pot, TutorialAction.Bud, NodeKind.Generator));
        // From then on, and without a tutorial, nothing is held back.
        Assert.True(Tutorial.Allows(TutorialStep.Genes, TutorialAction.Prune));
        Assert.True(Tutorial.Allows(null, TutorialAction.Prune));
    }

    [Fact]
    public void NoPassiveBudsUntilTheLabIsPlanted()
    {
        var (garden, stem) = Opening();
        garden.ActTutorial(TutorialStep.Welcome);
        garden.GrowBranch(stem, NodeKind.Grove);
        garden.AdvanceTutorial();
        for (int i = 0; i < 2000; i++)
        {
            foreach (var n in garden.Nodes) if (!n.Spec.IsFruit) n.Power = n.Store;
            garden.Grow(Tuning.EconomyStepSeconds);
        }
        Assert.False(stem.FormingBud);
        Assert.Equal(2, stem.Links); // its parent and the grove: the free link waits for the player's stem
    }

    [Fact]
    public void ItsStepIsSavedAndAnOldSaveHasNone()
    {
        var (garden, _) = Opening();
        garden.ActTutorial(TutorialStep.Welcome);
        Assert.Equal(TutorialStep.Grove, Garden.FromSave(garden.ToSave()).Tutorial);
        Assert.Null(Garden.FromSave(garden.ToSave() with { Tutorial = null }).Tutorial);
        Assert.Null(Garden.New(Layout.Radial).Tutorial); // (tests and sims start without one)
    }

    [Fact]
    public void BudsGrowTwiceAsFastDuringIt()
    {
        double Seconds(bool tutorial, bool lab)
        {
            var (garden, stem) = Opening();
            garden.ActTutorial(TutorialStep.Welcome);
            if (!tutorial) garden = Garden.FromSave(garden.ToSave() with { Tutorial = null });
            stem = garden.Nodes[1];
            garden.GrowBranch(stem, NodeKind.Grove); // (not the groveless first grove's speed)
            if (lab)
            {
                garden.Capacity = garden.Capacity with { CoreLinks = 2 };
                Assert.NotNull(garden.GrowBranch(garden.Core, NodeKind.Lab));
            }
            garden.SproutFrom(stem, NodeKind.Stem);
            double t = 0;
            while (stem.FormingBud && t < 600)
            {
                foreach (var n in garden.Nodes) if (!n.Spec.IsFruit) n.Power = n.Store;
                garden.Grow(Tuning.EconomyStepSeconds);
                t += Tuning.EconomyStepSeconds;
            }
            return t;
        }
        double with = Seconds(true, lab: false), without = Seconds(false, lab: false);
        Assert.InRange(with, without / Tuning.TutorialGrowthSpeed * 0.8, without / Tuning.TutorialGrowthSpeed * 1.2);
        Assert.InRange(Seconds(true, lab: true), without * 0.9, without * 1.1); // the lab out: back to the usual speed
    }

    [Fact]
    public void AStepAndItsWaitShareAPip()
    {
        Assert.Equal(-1, Tutorial.Pip(TutorialStep.Welcome));
        Assert.Equal(Tutorial.Pip(TutorialStep.Grove), Tutorial.Pip(TutorialStep.GroveWait));
        Assert.Equal(Tutorial.Pip(TutorialStep.Pot), Tutorial.Pip(TutorialStep.Tools));
        Assert.Equal(Tutorial.Pips - 1, Tutorial.Pip(TutorialStep.Research));
        Assert.Equal(Tutorial.Pips, Tutorial.Pip(TutorialStep.Done));
        for (var step = TutorialStep.Welcome; step < TutorialStep.Done; step++) // in order, none skipped
            Assert.InRange(Tutorial.Pip(step + 1) - Tutorial.Pip(step), 0, 1);
    }
}
