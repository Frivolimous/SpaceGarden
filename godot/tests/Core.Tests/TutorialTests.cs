using System.Numerics;

namespace SpaceGarden.Core.Tests;

// The opening's tutorial (Jeremy, Nov 3): grove, stem, lab, pot, wait for research, research, then a send-off.
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

    [Fact]
    public void ANewGardenWalksTheStepsInOrder()
    {
        var (garden, stem) = Opening();
        Assert.Equal(TutorialStep.Grove, garden.Tutorial);

        Assert.True(garden.SproutFrom(stem, NodeKind.Grove)); // a bud started counts
        Assert.True(garden.AdvanceTutorial());
        Assert.Equal(TutorialStep.Stem, garden.Tutorial);
        for (int i = 0; i < 1000 && stem.FormingBud; i++) // (a part forms one bud at a time: the grove comes out first)
        {
            foreach (var n in garden.Nodes) if (!n.Spec.IsFruit) n.Power = n.Store;
            garden.Grow(Tuning.EconomyStepSeconds);
        }

        var second = garden.GrowBranch(stem, NodeKind.Stem)!;
        garden.AdvanceTutorial();
        Assert.Equal(TutorialStep.Lab, garden.Tutorial);

        garden.GrowBranch(second, NodeKind.Lab);
        garden.AdvanceTutorial();
        Assert.Equal(TutorialStep.Pot, garden.Tutorial);

        var pot = garden.Place(ShapingKind.Pot, new Vector2(80, 0))!;
        Assert.False(garden.AdvanceTutorial()); // placed, not yet holding a part
        Assert.True(garden.Tie(second, pot));
        garden.AdvanceTutorial();
        Assert.Equal(TutorialStep.Wait, garden.Tutorial);

        garden.GeneBalance = garden.Cheapest!.Cost;
        garden.AdvanceTutorial();
        Assert.Equal(TutorialStep.Research, garden.Tutorial);

        Assert.True(garden.Buy(garden.Cheapest!.Track));
        garden.AdvanceTutorial();
        Assert.Equal(TutorialStep.Done, garden.Tutorial);
        Assert.False(garden.AdvanceTutorial()); // the send-off waits for the UI
        garden.FinishTutorial();
        Assert.Null(garden.Tutorial);
    }

    [Fact]
    public void StepsDoneEarlyArePassedAtOnce()
    {
        var (garden, stem) = Opening();
        garden.GrowBranch(stem, NodeKind.Lab);
        garden.GrowBranch(stem, NodeKind.Grove);
        garden.AdvanceTutorial();
        Assert.Equal(TutorialStep.Stem, garden.Tutorial); // the lab waits its turn, then counts
    }

    [Fact]
    public void NoPassiveBudsUntilTheLabIsAskedFor()
    {
        var (garden, stem) = Opening();
        var grove = garden.GrowBranch(stem, NodeKind.Grove)!;
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
        var (garden, stem) = Opening();
        garden.GrowBranch(stem, NodeKind.Grove);
        garden.AdvanceTutorial();
        Assert.Equal(TutorialStep.Stem, Garden.FromSave(garden.ToSave()).Tutorial);
        Assert.Null(Garden.FromSave(garden.ToSave() with { Tutorial = null }).Tutorial);
        Assert.Null(Garden.New(Layout.Radial).Tutorial); // (tests and sims start without one)
    }
}
