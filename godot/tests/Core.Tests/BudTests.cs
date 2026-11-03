namespace SpaceGarden.Core.Tests;

// Step 3e (DESIGN.md, Growth and pruning): a bud the player plants starts at once and forms like a passive one; every bud
// grows on its own at its part's fill, and growth blobs hurry it; fruit grows only from blobs.
public class BudTests
{
    static PlantNode Add(Garden garden, PlantNode parent, NodeKind kind) =>
        garden.GrowBranch(parent, kind) ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

    // core - stem, no passive buds, no groves.
    static (Garden Garden, PlantNode Stem) WithAStem(int seed = 7)
    {
        var garden = Garden.New(Layout.Radial, seed);
        garden.Capacity = new Capacity(3, 1, 1);
        garden.PassiveGrowth = false;
        var stem = Add(garden, garden.Core, NodeKind.Stem);
        return (garden, stem);
    }

    // Grows a step at a time, every part held at this fill, until done or the time runs out; returns the seconds taken.
    static double GrowAt(Garden garden, float fill, Func<bool> done, double maxSeconds)
    {
        double seconds = 0;
        while (!done() && seconds < maxSeconds)
        {
            foreach (var n in garden.Nodes) if (!n.Spec.IsFruit) n.Power = n.Store * fill;
            garden.Grow(Tuning.EconomyStepSeconds);
            seconds += Tuning.EconomyStepSeconds;
        }
        return seconds;
    }

    // Nov 3: the player can cancel a forming bud (its growth lost, its link free again) or change it to another kind,
    // which starts over.
    [Fact]
    public void AFormingBudCanBeCancelledOrChangedStartingOver()
    {
        var (garden, stem) = WithAStem();
        Assert.True(garden.SproutFrom(stem, NodeKind.Lab));
        stem.Growth = 2;
        Assert.False(garden.CanChangeBud(stem, NodeKind.Lab)); // already a lab
        Assert.True(garden.CanChangeBud(stem, NodeKind.Grove));
        Assert.True(garden.ChangeBud(stem, NodeKind.Grove));
        Assert.True(Garden.IsBudOf(stem.Forming!.Value, NodeKind.Grove));
        Assert.Equal(0, stem.Growth);

        stem.Growth = 1;
        Assert.True(garden.CancelBud(stem));
        Assert.Null(stem.Forming);
        Assert.Equal(0, stem.Growth);
        Assert.False(garden.CancelBud(stem)); // nothing forming
        Assert.True(garden.CanSproutFrom(stem, NodeKind.Lab)); // its link free again
    }

    // A change needs room for the new kind, counting the bud's own room as free: no stem slot left, a lab bud can't
    // become a stem; a stem bud can become another stem kind's room (its own).
    [Fact]
    public void AChangeNeedsRoomForTheNewKind()
    {
        var (garden, stem) = WithAStem(); // 3 stems allowed, 1 grown
        Add(garden, stem, NodeKind.Stem);
        var third = Add(garden, garden.Nodes.Last(), NodeKind.Stem);
        Assert.True(garden.SproutFrom(third, NodeKind.Lab));
        Assert.False(garden.CanChangeBud(third, NodeKind.Stem)); // the stem cap is full
        Assert.True(garden.CanChangeBud(third, NodeKind.Grove));
    }

    [Fact]
    public void AManualBudFormsThenEmergesInAboutTwoMinutesAtFullFill()
    {
        var (garden, stem) = WithAStem();
        int parts = garden.Nodes.Count;
        Assert.True(garden.SproutFrom(stem, NodeKind.Lab));
        // It starts at once, as a bud: no new part yet, but its link is held.
        Assert.Equal(parts, garden.Nodes.Count);
        Assert.Equal(NodeKind.Lab, stem.Forming);
        Assert.Equal(0, stem.Growth);
        Assert.False(garden.CanSproutFrom(stem, NodeKind.Lab)); // one bud at a time in a part
        double seconds = GrowAt(garden, 1, () => stem.Branches.Count == 1, 600);
        // (a little over: a step's upkeep takes the fill just under full before the bud grows)
        Assert.InRange(seconds, Tuning.BudSelfGrowSeconds, Tuning.BudSelfGrowSeconds * 1.03);
        Assert.Equal(NodeKind.Lab, Assert.Single(stem.Branches).Spec.Kind);
        Assert.Null(stem.Forming);
    }

    [Fact]
    public void AManualBudHoldsItsCapSlotWhileItForms()
    {
        var (garden, stem) = WithAStem();
        garden.Capacity = new Capacity(2, 1, 1); // room for one more stem
        Assert.True(garden.SproutFrom(stem, NodeKind.Stem));
        Assert.False(garden.CanSproutFrom(garden.Core, NodeKind.Stem));
    }

    [Fact]
    public void ABudOnADimPartGrowsSlower()
    {
        var (garden, stem) = WithAStem();
        garden.SproutFrom(stem, NodeKind.Lab);
        GrowAt(garden, 0.25f, () => false, Tuning.BudSelfGrowSeconds);
        Assert.Null(stem.Branches.FirstOrDefault());
        Assert.Equal(Tuning.BudGrowth / 4, stem.Growth, 0.02f);
        double seconds = Tuning.BudSelfGrowSeconds + GrowAt(garden, 0.25f, () => stem.Branches.Count == 1, 1200);
        Assert.InRange(seconds, 4 * Tuning.BudSelfGrowSeconds, 4 * Tuning.BudSelfGrowSeconds * 1.03);
    }

    [Fact]
    public void AnEmptyPartsBudWaits()
    {
        var (garden, stem) = WithAStem();
        garden.SproutFrom(stem, NodeKind.Lab);
        GrowAt(garden, 0, () => false, 600);
        Assert.Equal(0, stem.Growth);
    }

    [Fact]
    public void FruitNeverGrowsOnItsOwn()
    {
        var (garden, stem) = WithAStem();
        stem.Forming = NodeKind.Leaf;
        stem.Growth = Tuning.BlobGrowth;
        GrowAt(garden, 1, () => false, 3600);
        Assert.Equal(NodeKind.Leaf, stem.Forming);
        Assert.Equal(Tuning.BlobGrowth, stem.Growth);
        Assert.Empty(stem.Fruits);
    }

    [Fact]
    public void APlantedBudReplacesAFormingFruit()
    {
        var (garden, stem) = WithAStem();
        stem.Forming = NodeKind.Leaf;
        stem.Growth = 0.5f;
        Assert.True(garden.SproutFrom(stem, NodeKind.Grove));
        Assert.Equal(NodeKind.Grove, stem.Forming);
        Assert.Equal(0, stem.Growth);
    }

    [Fact]
    public void GrowthBlobsHurryABud()
    {
        // core - stem - stem - grove, the bud on the middle stem, which every blob passes on its way out.
        var (garden, first) = WithAStem();
        var middle = Add(garden, first, NodeKind.Stem);
        Add(garden, middle, NodeKind.Grove);
        Assert.True(garden.SproutFrom(middle, NodeKind.Lab));
        double seconds = GrowAt(garden, 1, () => middle.Branches.Any(b => b.Spec.Kind == NodeKind.Lab), 600);
        Assert.True(seconds < 0.75 * Tuning.BudSelfGrowSeconds, $"took {seconds:0} s");
    }

    [Fact]
    public void APassiveBudGrowsOnItsOwnToo()
    {
        // A passive bud starts from a blob, then grows alone even once no grove is left to feed it.
        var (garden, first) = WithAStem();
        garden.PassiveGrowth = true;
        var middle = Add(garden, first, NodeKind.Stem);
        var grove = Add(garden, middle, NodeKind.Grove);
        GrowAt(garden, 1, () => garden.Nodes.Any(n => n.FormingBud), 3600);
        var budding = garden.Nodes.Single(n => n.FormingBud);
        garden.Prune(grove);
        garden.ForgetPrune();
        var kind = budding.Forming!.Value;
        int before = budding.Branches.Count(b => b.Spec.Kind == kind);
        GrowAt(garden, 1, () => !budding.FormingBud, 600);
        Assert.Equal(before + 1, budding.Branches.Count(b => b.Spec.Kind == kind));
    }

    [Fact]
    public void ANewPlayersFirstGroveEmergesInTheOpeningMinutes()
    {
        // As a new game starts (see Main.PlantAtHome): the core and its first stem, no grove, so nothing grows passively.
        // The player plants a grove on the stem straight away; it forms at the stem's own fill, which starts empty.
        var garden = Garden.New(Layout.Radial, 7);
        var stem = garden.Sprout()!;
        Assert.True(garden.SproutFrom(stem, NodeKind.Grove));
        double seconds = 0;
        while (!stem.Branches.Any(b => b.Spec.Kind == NodeKind.Grove) && seconds < 3600)
        {
            garden.Grow(1);
            seconds++;
        }
        // A garden with no grove grows one FirstGroveSpeed times as fast: about half a minute, the stem filling meanwhile.
        Assert.InRange(seconds, Tuning.BudSelfGrowSeconds / Tuning.FirstGroveSpeed, 60);
    }

    [Fact]
    public void OnlyAGrovelessGardenGrowsItsGroveFaster()
    {
        var (garden, stem) = WithAStem();
        Assert.True(garden.SproutFrom(stem, NodeKind.Grove));
        double first = GrowAt(garden, 1, () => stem.Branches.Count == 1, 600);
        Assert.InRange(first, Tuning.BudSelfGrowSeconds / Tuning.FirstGroveSpeed, Tuning.BudSelfGrowSeconds / Tuning.FirstGroveSpeed * 1.05);
        // With a grove out, the next grove bud grows at the usual rate (the first grove held empty, so it sends no blobs).
        Assert.True(garden.SproutFrom(stem, NodeKind.Grove));
        double second = 0;
        while (stem.Branches.Count < 2 && second < 600)
        {
            foreach (var n in garden.Nodes) if (!n.Spec.IsFruit) n.Power = n.Spec.Kind == NodeKind.Grove ? 0 : n.Store;
            garden.Grow(Tuning.EconomyStepSeconds);
            second += Tuning.EconomyStepSeconds;
        }
        Assert.InRange(second, Tuning.BudSelfGrowSeconds, Tuning.BudSelfGrowSeconds * 1.03);
    }
}
