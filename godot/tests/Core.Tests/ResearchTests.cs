using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceGarden.Core.Tests;

// Step 5b's upgrade tracks (DESIGN.md, Upgrade tracks): genes from the balance buy each track's next upgrade, in
// order, and the caps rise at once; the levels bought go through the save.
public class ResearchTests
{
    static Garden NewGarden() => Garden.New(Layout.Radial, seed: 7);

    // The widget's own save options (SaveFile.Options).
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // Buys every upgrade of a track, delivering its cost first, and returns what each was.
    static List<Upgrade> BuyAll(Garden garden, Track track)
    {
        var bought = new List<Upgrade>();
        while (garden.Next(track) is { } next)
        {
            garden.Deliver(next.Cost);
            Assert.True(garden.Buy(track));
            bought.Add(next);
        }
        return bought;
    }

    // ==== COSTS ==== \\

    [Fact]
    public void ANewGardenStartsAsTheDocsSay()
    {
        // DESIGN.md: the core at 1 link, 4 stems, 1 generator, no seedling slots or wild flowers, 1 Gravity Pot.
        var caps = NewGarden().Capacity;
        Assert.Equal(new Capacity(4, 1, 1, Pots: 1, Trellises: 0, Wells: 0, WildFlowers: 0, Seedlings: 0, Hubs: 0), caps);
    }

    [Fact]
    public void TheStemAndGeneratorTracksCostWhatTheDocsSay()
    {
        var garden = NewGarden();
        var stems = BuyAll(garden, Track.Stems);
        Assert.Equal(43, stems.Count);
        Assert.Equal([100, 150, 200, 300, 450], stems.Take(5).Select(u => u.Cost));
        Assert.Equal(611_100, stems[^1].Cost);
        Assert.Equal(5_563_850, stems.Sum(u => u.Cost));
        Assert.All(stems, u => Assert.Equal(UpgradeKind.Stem, u.Kind));
        Assert.Equal(47, garden.Capacity.Stems);

        var generators = BuyAll(garden, Track.Generators);
        Assert.Equal(43, generators.Count);
        Assert.Equal([1_000, 2_000, 3_000, 5_000, 10_000, 15_000, 20_000, 25_000, 30_000, 42_000],
            generators.Take(10).Select(u => u.Cost));
        Assert.Equal(846_000, generators[^1].Cost);
        Assert.Equal(13_847_000, generators.Sum(u => u.Cost));
        Assert.Equal(44, garden.Capacity.Generators);
    }

    [Fact]
    public void TheCoreTrackGoesInTheDocsOrder()
    {
        var garden = NewGarden();
        var core = BuyAll(garden, Track.Core);
        Assert.Equal([(UpgradeKind.WildFlower, 1_500.0), (UpgradeKind.CoreLink, 2_500), (UpgradeKind.Seedling, 3_500),
                (UpgradeKind.WildFlower, 6_000), (UpgradeKind.Seedling, 12_000), (UpgradeKind.WildFlower, 21_000),
                (UpgradeKind.Seedling, 32_000), (UpgradeKind.WildFlower, 43_000), (UpgradeKind.Hub, 54_000)],
            core.Take(9).Select(u => (u.Kind, u.Cost)));
        Assert.Equal(175_500, core.Take(9).Sum(u => u.Cost)); // the core to the Hub
        // After the core link, seedling slots and the wild flower limit by turns (the Hub in among them), each dearer
        // than the last.
        var rest = core.Skip(2).Where(u => u.Kind != UpgradeKind.Hub).ToList();
        Assert.Equal(40, rest.Count);
        for (int i = 0; i < rest.Count; i++)
            Assert.Equal(i % 2 == 0 ? UpgradeKind.Seedling : UpgradeKind.WildFlower, rest[i].Kind);
        for (int i = 1; i < core.Count; i++) Assert.True(core[i].Cost > core[i - 1].Cost);

        var caps = garden.Capacity;
        Assert.Equal(2, caps.CoreLinks);
        Assert.Equal(2, garden.Core.MaxLinks);
        Assert.Equal(1, caps.Hubs);
        Assert.Equal(core.Count(u => u.Kind == UpgradeKind.WildFlower), caps.WildFlowers);
        Assert.Equal(core.Count(u => u.Kind == UpgradeKind.Seedling), caps.Seedlings);
    }

    [Fact]
    public void EveryTracksCostsOnlyRise()
    {
        foreach (var costs in new[] { Tuning.StemCosts, Tuning.GeneratorCosts, Tuning.CoreCosts.Select(c => c.Cost).ToArray() })
            for (int i = 1; i < costs.Length; i++) Assert.True(costs[i] > costs[i - 1]);
    }

    // ==== BUYING ==== \\

    [Fact]
    public void BuyingSpendsTheBalanceAndRaisesTheCap()
    {
        var garden = NewGarden();
        garden.Deliver(260);
        Assert.True(garden.Buy(Track.Stems));
        Assert.Equal(160, garden.GeneBalance);
        Assert.Equal(260, garden.Genes); // genes earned never drop
        Assert.Equal(5, garden.Capacity.Stems);
        Assert.Equal(1, garden.StemsBought);
        Assert.Equal(150, garden.Next(Track.Stems)!.Cost);
        Assert.True(garden.Buy(Track.Stems));
        Assert.Equal(10, garden.GeneBalance);
        Assert.Equal(6, garden.Capacity.Stems);
        Assert.Equal(2, garden.Bought);
    }

    [Fact]
    public void ABalanceThatFallsShortBuysNothing()
    {
        var garden = NewGarden();
        garden.Deliver(99);
        Assert.False(garden.Buy(Track.Stems));
        Assert.False(garden.Buy(Track.Generators));
        Assert.False(garden.Buy(Track.Core));
        Assert.Equal(99, garden.GeneBalance);
        Assert.Equal(Capacity.Start, garden.Capacity);
        Assert.Equal(0, garden.Bought);

        garden.Deliver(1); // exactly the cost
        Assert.True(garden.Buy(Track.Stems));
        Assert.Equal(0, garden.GeneBalance);
    }

    [Fact]
    public void AFinishedTrackBuysNothingMore()
    {
        var garden = NewGarden();
        BuyAll(garden, Track.Generators);
        garden.Deliver(1e6);
        Assert.Null(garden.Next(Track.Generators));
        Assert.False(garden.Buy(Track.Generators));
        Assert.Equal(1e6, garden.GeneBalance);
    }

    [Fact]
    public void TheCheapestNextUpgradeIsAcrossTheTracks()
    {
        var garden = NewGarden();
        Assert.Equal(new Upgrade(Track.Stems, UpgradeKind.Stem, 100), garden.Cheapest);
        BuyAll(garden, Track.Stems);
        Assert.Equal(new Upgrade(Track.Generators, UpgradeKind.Generator, 1_000), garden.Cheapest);
        BuyAll(garden, Track.Generators);
        Assert.Equal(new Upgrade(Track.Core, UpgradeKind.WildFlower, 1_500), garden.Cheapest);
        BuyAll(garden, Track.Core);
        Assert.Null(garden.Cheapest);
    }

    [Fact]
    public void ACoreLinkGivesTheCoreRoomForASecondBranch()
    {
        var garden = NewGarden();
        garden.PassiveGrowth = false;
        Assert.NotNull(garden.GrowBranch(garden.Core, NodeKind.Stem));
        Assert.False(garden.CanSproutFrom(garden.Core, NodeKind.Stem));
        garden.Deliver(5_500);
        Assert.True(garden.Buy(Track.Core)); // a wild flower slot
        Assert.True(garden.Buy(Track.Core)); // the core link
        Assert.True(garden.CanSproutFrom(garden.Core, NodeKind.Stem));
    }

    [Fact]
    public void CoreLinksAndTheHubAreMomentsButStemsAreNot()
    {
        var garden = NewGarden();
        garden.Grow(60);
        BuyAll(garden, Track.Stems);
        for (int i = 0; i < 9; i++)
        {
            garden.Deliver(garden.Next(Track.Core)!.Cost);
            garden.Buy(Track.Core);
        }
        var bought = garden.Tracking.Journal.Where(e => e.Kind == JournalKind.Purchase).ToList();
        Assert.Equal(["Second core link bought", "Hub bought"], bought.Select(e => e.Text));
        Assert.All(bought, e => Assert.Equal(60, e.Age, 6));
    }

    [Fact]
    public void TheTotalsExportTheUpgradesBought()
    {
        var garden = NewGarden();
        garden.Deliver(2_000);
        garden.Buy(Track.Stems);
        garden.Buy(Track.Core);
        var csv = garden.Tracking.TotalsCsv(garden);
        Assert.Contains("upgrades_bought,2\n", csv);
        Assert.Contains("stems_bought,1\n", csv);
        Assert.Contains("generators_bought,0\n", csv);
        Assert.Contains("core_bought,1\n", csv);
        Assert.DoesNotContain("unlock", csv);
    }

    // ==== SAVES ==== \\

    // Through the save's JSON and back: the levels bought and the caps they raised, and the next upgrades carry on
    // from there.
    [Fact]
    public void WhatWasBoughtSurvivesTheSave()
    {
        var garden = NewGarden();
        garden.Deliver(10_000);
        for (int i = 0; i < 3; i++) garden.Buy(Track.Stems);
        garden.Buy(Track.Generators);
        garden.Buy(Track.Core);
        garden.Buy(Track.Core);

        var json = JsonSerializer.Serialize(garden.ToSave(), Json);
        var loaded = Garden.FromSave(JsonSerializer.Deserialize<GardenSave>(json, Json)!, seed: 7);
        Assert.Equal((3, 1, 2), (loaded.StemsBought, loaded.GeneratorsBought, loaded.CoreBought));
        Assert.Equal(garden.Capacity, loaded.Capacity);
        Assert.Equal(garden.GeneBalance, loaded.GeneBalance);
        Assert.Equal(2, loaded.Core.MaxLinks);
        Assert.Equal(garden.Next(Track.Stems), loaded.Next(Track.Stems));
        Assert.Equal(new Upgrade(Track.Core, UpgradeKind.Seedling, 3_500), loaded.Next(Track.Core));
    }

    // A version 9 save from step 5a has no levels: its caps (raised by the old unlock track) are kept and read as the
    // upgrades bought, so the tracks carry on from them.
    [Fact]
    public void AStep5aSaveKeepsItsCaps()
    {
        var garden = NewGarden();
        var save = garden.ToSave() with { Capacity = new Capacity(16, 5, 2, Pots: 2, Trellises: 1, Wells: 1), Research = null };
        var json = JsonSerializer.Serialize(save, Json).Replace(",\"Research\":null", "");
        Assert.DoesNotContain("Research", json);
        var loaded = Garden.FromSave(JsonSerializer.Deserialize<GardenSave>(json, Json)!, seed: 7);
        Assert.Equal((12, 4, 2), (loaded.StemsBought, loaded.GeneratorsBought, loaded.CoreBought));
        Assert.Equal(new Capacity(16, 5, 2, Pots: 2, Trellises: 1, Wells: 1, WildFlowers: 1, Seedlings: 0, Hubs: 0),
            loaded.Capacity);
        Assert.Equal(Tuning.StemCosts[12], loaded.Next(Track.Stems)!.Cost);
        Assert.Equal(UpgradeKind.Seedling, loaded.Next(Track.Core)!.Kind); // the wild flower slot and core link read as bought
    }
}
