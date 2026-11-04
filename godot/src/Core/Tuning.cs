namespace SpaceGarden.Core;

// The numbers that shape the feel, across the garden (each kind's own are in NodeSpecs). Physics values are per 60 Hz tick, as in the TS game's
// Config.PHYSICS (same values, so the motion matches); growth values are in real seconds.
public static class Tuning
{
    public const int TicksPerSecond = 60;

    // From Config.PHYSICS.
    public const float Elasticity = 0.004f;
    public const float Damp = 0.98f;
    public const float MinVelocity = 0.0001f;
    public const float SpinSpeed = 0.005f;

    // New for the widget: a gentle pull towards the light (upright layout only), a ground the plant can't sink
    // through, and wind that sways the plant more the higher a node sits.
    public const float Upward = 0.004f;
    public const float UpwardFruit = 0.0012f;
    public const float GroundBounce = 0.1f;
    public const float WindStrength = 0.0025f;
    public const float WindHeight = 220; // px above the core at which the sway reaches full strength

    // Holding a part, from Config.PHYSICS (PULL_FRICTION, TARGET_RATIO, TARGET_MIN): it's pulled towards the
    // pointer, harder the lighter it is, and the links, soft springs with no length limit, stretch and drag the rest
    // of the plant after it.
    public const float PullFriction = 0.9f;
    public const float PullRatio = 0.008f;
    public const float PullMinDistanceSquared = 100;

    // The grid that speeds up the parts' push on each other (see Garden.Repel): the cell size, and the force from
    // which a part pushes every other part exactly (only the core's, 30).
    public const float RepelCell = 120;
    public const int FarPushEvery = 4; // ticks between working out each part's push from distant cells
    public const float FarReachForce = 10;

    // From Config.PHYSICS.EDGE_BOUNCE: how hard the screen edges push parts back.
    public const float EdgeBounce = 0.1f;

    // Motes (travelling blobs: genes, and growth): seconds per link (Jeremy, Nov 3: 25% slower, was 0.3).
    public const float MoteLinkSeconds = 0.4f;
    public const float GlowFadePerSecond = 1.5f;

    // Power shows as link flashes, as in the original game: each transfer lights its link, and the flash fades in
    // about 0.3 s (its 300 ms tween). A transfer's flash shows at a random moment within its economy step, so links
    // don't all flash in step (the original's ±10% timing). Placeholder: a flash is FlashPeak times as bright as the
    // part the power reached is full (1: a full part's flash is as bright as the original's).
    public const float LinkFlashFadePerSecond = 3.3f;
    public const float FlashPeak = 0.9f; // (Jeremy, Nov 3: brighter, was 0.6)

    // Whether a part sends its transfer rate times its fill (Jeremy, Nov 3) or its whole rate whatever its fill (the
    // rule before). Not const: the recovery experiment (RecoveryTests) runs both.
    public static bool TransferFollowsFill = true;

    // A part's brightness and size follow its fill, as in the original (PlantNodeView): the shown fill moves a share
    // of the way to the real one each tick (INTENSITY_V_INC / _DEC). Its level (PlantNode.Level) runs from
    // FillLookFloor empty to 1 full along the shown fill to the power FillLookCurve: flat near empty, so a low part
    // doesn't jump about, and steep near full, where a tended garden spends its time (about 70-105% full: the
    // stores are big against the clumps, so a clump moves a fill only a few points). The original's was the fill
    // itself (a curve of 1), which left a part at 75% and one at 95% looking much alike. Past full it rises at
    // OverFillLook a fill (the original's a tenth), up to FillLookMax, so an overfull stem still swells as clumps
    // arrive. Its size is its radius times its level.
    public const float FillLookRise = 0.1f;
    public const float FillLookFall = 0.05f;
    public const float FillLookFloor = 0.2f;
    public const float FillLookCurve = 2;
    public const float OverFillLook = 0.5f;
    public const float FillLookMax = 1.5f;

    // Growth is in real seconds at speed 1 (the prototype's default; its menu has faster speeds for testing). A free
    // Sprout() (the first stem, stress tests) grows fruit FruitChance of the time.
    public const double FruitChance = 2.0 / 3; // about two thirds of a grown plant is fruit (65% at 500 parts)
    public static int MaxNodes = 500; // not const: the --parts dev option raises it for stress tests

    // Placeholder. How long the last prune can be undone, in real seconds (counted in physics ticks, so the same at
    // any growth speed). Growth blobs start nothing new meanwhile, so the room the prune freed is still there.
    public const double UndoPruneSeconds = 30;
    // Whether the player can undo a prune at all (its pill and Ctrl+Z). Off (Jeremy, Nov 3: not needed); the rules keep
    // the window, so turning it back on is this switch.
    public const bool UndoPruneOn = false;

    // A pruned branch's parts drift loose for this long (real seconds, in physics ticks), fading out from the cut.
    // Each part fades a little quicker or slower than the next, at most this share of the time quicker.
    public const float CutFadeSeconds = 5f, CutFadeJitter = 0.25f;

    // Time away is caught up to at most this much real time.
    public const double CatchUpCapSeconds = 72 * 3600;
    public const int SettleTicks = 900;

    // ==== ECONOMY ==== \\
    // Power is the original game's (its Config NodeBase and Nodes, and PlantNodePower), its per-tick numbers times 60
    // to make them per second (DESIGN.md, Economy): it fills and drains in seconds, a lively light show, and doesn't
    // set the garden's pace (the upgrades bought with genes do). The economy runs in fixed steps of EconomyStepSeconds of growth
    // time, so a live session, a catch-up and a test all take the same steps whatever chunks the time arrives in. A
    // step is one power transfer: the original's powerDelay, 20 ticks give or take 10% (18.75 here, exact in binary,
    // so the steps don't drift with the chunks).
    public const double EconomyStepSeconds = 0.375;

    // Each kind's power (store, rate, weight, transfer rate) is in its NodeSpec (NodeSpecs.All), as the original's Nodes
    // config. Making and upkeep both follow the part's fill: a source makes its rate times its fill (none past full),
    // a drain costs its rate times its fill, so nothing ever quite empties. A new part starts empty. A fruit's effect on
    // its part (FlowerPowerFactor, FlowerBonus) is in the fruit's NodeSpec.

    // Power transfer (the original's attemptTransferPower and POWER_THRESHOLD): each step, a part fuller than SendFill
    // sends its clump of power to its emptiest neighbour that's less full than itself, emptiest by fill over weight
    // (stems, then the core, count as emptier than they are, so power heads out along the stems).
    public const float SendFill = 0.25f;

    // Fruit holds power too (the original's powerFruits, FRUIT_THRESHOLD): a part at least FeedFruitFill full feeds its
    // emptiest fruit (at its PowerToFruitPerSecond), so new fruit fill and brighten as they grow in.
    public const float FeedFruitFill = 0.6f;

    // Capacity at the start (DESIGN.md, Upgrade tracks). Buying upgrades and the garden's size raise Garden.Capacity.
    public const int StartStemCap = 4;
    public const int StartGeneratorCap = 1;
    public const int StartCoreLinks = 1;
    public const int StartPotCap = 1;
    public const int StartSeedlings = 0;
    public const int StartWildFlowers = 0;

    // ==== SHAPING ==== \\
    // Placeholders, per 60 Hz tick (DESIGN.md, Shaping the garden). A potted part moves PotFollow of the way to its pot
    // each tick, and each main part linked to it (its parent, and what grows straight out of it) is pushed PotPush,
    // over its mass, the way the pot faces (about five times the wind's strength on a stem). A Gravity Well pulls
    // every part, core and fruit too, WellPull towards it, alike whatever its weight (like gravity), easing off within
    // WellEase of its centre: a free plant drifts to the well at about 3 units a second. A part on a trellis slides
    // along it, its sliding damped by TrellisSlide each tick.
    public const float PotFollow = 0.12f;
    public const float PotPush = 0.012f;
    public const float WellPull = 0.001f;
    public const float WellEase = 40;
    public const float TrellisSlide = 0.9f;

    // ==== BLOBS ==== \\
    // Flowers make blobs at their kind's rate (NodeSpec.BlobsPerHour: a grove one every 3 s, a lab one every 5 s) when
    // full of power, in proportion to their fill when lower (each of their fruit adding its FlowerBonus).
    // Both kinds wander the stems at random, never turning straight back unless at a dead end, and fade after
    // BlobMaxHops links (the original's 30) if nothing absorbed them.
    public const int BlobMaxHops = 30;
    public const int MaxMotes = 60; // blobs in flight at once; past it a blob still does its work, unseen
    // Build mode's blob-path view counts the links blobs take over the last TrafficSeconds of growth (Jeremy, Nov 3: each
    // step expires after it; was a half-life of 2 minutes).
    public const double TrafficSeconds = 10;

    // Growth blobs, from groves, each carry BlobGrowth. A part with nothing forming absorbs one AbsorbChance of the
    // time (the original's 30%), a part with something forming always does. A fresh one starts a bud BudChance of the
    // time where a branch could grow, else a fruit; the fruit completes at FruitGrowth, the bud at BudGrowth.
    public const float BlobGrowth = 0.1f;
    public const float AbsorbChance = 0.3f;
    public const float BudChance = 0.5f;
    public const float FruitGrowth = 1;
    public const float BudGrowth = 3;
    // Every bud, passive or the player's, also grows on its own (fruit never does): a whole bud in BudSelfGrowSeconds
    // while its part is full, in proportion to the part's fill when lower. A draft (Jeremy, step 3e): fast enough that
    // a new player's first grove emerges within the opening minutes, slow enough that a grove-fed bud is plainly
    // faster (at full fill a blob's BlobGrowth is 4 s of growing alone, and a grove makes one every 3 s).
    public const float BudSelfGrowSeconds = 120;
    // While the garden has no grove (a new garden, or one that pruned its last), a grove bud grows this many times as
    // fast, so a new player's first grove is out in about half a minute instead of two (Jeremy, balance pass).
    public const float FirstGroveSpeed = 4;
    // While the opening's tutorial runs, until its lab is out, buds and fruit grow this many times as fast, on their own
    // and from growth blobs alike (Jeremy, Nov 3).
    public const float TutorialGrowthSpeed = 2;
    // Placeholders. A completed bud starts this far out (a share of its link's length) and pops out at this speed.
    public const float EmergeStart = 0.3f;
    public const float EmergeSpeed = 1.2f;

    // Gene blobs, from labs, each carry BlobGenes (a whole gene, so they can be counted); only the core absorbs them.
    // Labs are the only source: the first upgrade waits for the first lab's genes to find the core.
    public const float BlobGenes = 1;

    // ==== WILD FLOWERS ==== \\
    // DESIGN.md, Wild flowers. Every kind's numbers are Jeremy's table of Nov 3 (its store, power made or upkeep, power
    // weight, transfer, links and fruit are in WildFlowers.cs; the quirks' numbers are here). The chance a new bud grows
    // wild while a wild flower slot is free, by how it started: a passive bud's and a planted one's (Jeremy, Nov 3:
    // raised to a quarter, and no longer per free slot; were 5% and 4% a slot).
    public const float PassiveWildChance = 0.25f;
    public const float PlantedWildChance = 0.25f;

    // Amplifier: a gene or growth blob passing it has its value doubled AmplifyChance of the time, once a blob at most.
    public const float AmplifyChance = 0.5f;
    // Pulsar: makes power up to PulsarBurstFill of its store, sending nothing, then sends PulsarBurst of its store at
    // once (Jeremy, Nov 3: half its store, at 150%).
    public const float PulsarBurst = 0.5f, PulsarBurstFill = 1.5f;
    // Ember: the power it can hold falls in a straight line from its store to nothing over EmberLifeSeconds, so it dims
    // and makes less as it goes, then burns out (Jeremy, Nov 3: a day, was 3).
    public const float EmberLifeSeconds = 86400;
    // Tendril: its link TendrilLink times as long, as springy as any (Jeremy, Nov 3; was 0.3 as springy); it sends power TendrilSplits times a
    // step, a share each (Jeremy, Nov 3: "transfer delay / 2", a steadier flow).
    public const float TendrilLink = 2.5f;
    public const int TendrilSplits = 2;
    // Wood (the Lumber Grove's fruit) and Glow: glow, a white fruit, sends GlowBlobs buff blobs an hour (one a minute).
    // Buffer: BufferBlobs an hour (one every 5 s); each wanders BufferReach links (BufferAmplifiedReach more once it
    // passes an Amplifier), and every part it passes that uses a buff makes BuffBonus more (power and blobs) for the
    // next BuffSeconds (Jeremy, Nov 3: 150% of its output, +50%, for 30 s; was a minute). A second buff only starts the
    // time again.
    public const float BufferBlobs = 720, GlowBlobs = 60, BuffBonus = 0.5f, BuffSeconds = 30;
    // A buff blob is used up by the first part on its way that uses a buff and has none, or less than this many seconds
    // of one left; the rest pass it on (Jeremy, Nov 4: it was spent on every part it passed, and never used up).
    public const float BuffRefreshSeconds = 5;
    public const int BufferReach = 5, BufferAmplifiedReach = 3;
    // Comet Bloom (Jeremy, Nov 3): active for CometActiveSeconds, then dormant, fading for CometFadeSeconds, then it
    // withers. While any is active, every gene and growth blob is worth CometBlobValue times as much, and buffs last
    // CometBuffTime times as long.
    public const float CometActiveSeconds = 600, CometFadeSeconds = 6 * 3600, CometBlobValue = 2, CometBuffTime = 2;
    // Moonflower: each makes buds within MoonflowerReach links MoonflowerBoost more likely to grow wild (twice as likely,
    // with one), and wild flowers within that reach make MoonlitBonus more (power and blobs; not stacking).
    public const int MoonflowerReach = 5;
    public const float MoonflowerBoost = 1, MoonlitBonus = 0.25f;
    // (The Nebula Bloom is retired, Nov 3: it no longer grows. One in an older garden keeps its effect: power reaching a
    // seedling within NebulaReach links counts NebulaFill times.)
    public const int NebulaReach = 5;
    public const float NebulaFill = 2;
    // A passive bud that grows wild picks among its base's own kinds PassiveBaseShare of the time and among the
    // passive-only kinds the rest (alike within each), when there are both. Picked evenly (5d's first build), a grove
    // bud became a passive-only kind two times in three.
    public const double PassiveBaseShare = 2.0 / 3;

    // ==== SEEDLINGS ==== \\
    // Placeholders (DESIGN.md, Seedlings and evolutions). A seedling's fill is its power (its own numbers are in its
    // NodeSpec: its store, the choice's 200, no upkeep, a heavy power weight, and no transfer, so it only takes power
    // in). Full, it offers ChoiceCount evolutions; choosing one raises its store to its target, FirstBloom times
    // BloomGrowth for each evolution chosen before; full again, it blooms. Repeats are offered only once the garden has
    // RepeatsAfter evolutions.
    public const double FirstBloom = 10_000, BloomGrowth = 3; // Jeremy, Nov 1 (were 1,000 and 5: blooms took minutes)
    public const int ChoiceCount = 3, RepeatsAfter = 5;

    // ==== THE HUB ==== \\
    // Placeholders (DESIGN.md, The Hub; Jeremy's step 5 addendum). Gene blobs it catches fill its pool, which turns into
    // hub points, linearly: the first at HubFirstPoint genes, each after HubPointStep more than the last (3,000, 3,400,
    // 3,800 ...). The paths, their costs and effects are data in Hubs.Paths. The Hub's upkeep is HubUpkeep a second,
    // plus HubUpkeepPerLevel for every level bought (HubUpkeepCut of that once its own path reaches level 3). The
    // milestones read where they act: the stem's (HubStemCap more stems), the Amplifier's (a doubling blob tripled
    // HubTripleChance of the time), the Buffer's (buffs last HubBuffSeconds longer), the Moonflower's (its
    // reach HubMoonflowerReach links) and the Nebula Bloom's (power to its seedlings counts HubNebulaFill times).
    public const double HubFirstPoint = 3_000, HubPointStep = 400;
    public const float HubUpkeep = 3, HubUpkeepPerLevel = 0.5f, HubUpkeepCut = 0.8f;
    // How fast the Hub sends power on, a second (a stem 30; Jeremy, Nov 3: was 15).
    public const float HubTransfer = 20;
    public const int HubStemCap = 2, HubMoonflowerReach = 6; // (Nov 3: 6, was 8)
    // The Buffer's milestone (Nov 3): buffs last this many seconds longer.
    public const float HubBuffSeconds = 10;
    // The lab's milestones (Jeremy, Nov 3): a gene blob, from any flower, that fades without finding the core or the
    // Hub still gives this share of its genes to the core, at level 3 and level 5.
    public const float HubFadedGenes3 = 0.25f, HubFadedGenes5 = 0.5f;
    // The stem's level 3 milestone (Nov 3): a stem's max power this much more.
    public const float HubStemStore = 150;
    // The level 3 milestones of the Wild Lab (max power more), the Lichen (with all 4 leaves, power made a second and its
    // transfer) and the Seed Pod (buds its blobs start this many times as likely to grow wild) (Nov 3).
    public const float HubWildLabStore = 100, HubLichenGen = 3, HubSeedPodWild = 2;
    // What resetting the Hub's levels costs, in points (Jeremy, Nov 3: "reduce total levels by 1"; a level is 1-3).
    public const int HubRespecCost = 1;
    public const float HubTripleChance = 0.2f, HubNebulaFill = 3;

    // ==== RESEARCH ==== \\
    // The three upgrade tracks (DESIGN.md, Upgrade tracks): genes from the balance buy each track's next upgrade, in
    // order. Placeholders for playtesting (Jeremy's balance pass, Nov 1: dearer overall than 5b's, the early steps
    // most; all three again, Nov 3-4: dearer from the middle, to meet the rebalanced wild flowers and evolutions).
    // Stems and generators each raise their cap by 1 (to 47 stems and 44 generators); the core's track is its own
    // list, the Hub ninth. Past these each track ends, as long-term pacing isn't designed yet.
    public static readonly double[] StemCosts =
    [
        100, 150, 250, 450, 750, 1_150, 1_650, 2_300, 3_150, 4_250, 5_650, 7_400, 9_550, 12_350, 15_850, 20_100, 25_200,
        31_200, 38_200, 46_250, 55_400, 65_750, 77_350, 90_300, 104_650, 120_450, 137_800, 156_750, 177_400, 199_800,
        224_000, 250_100, 278_150, 308_250, 340_450, 374_800, 411_400, 450_300, 491_600, 535_350, 581_600, 630_450,
        681_950,
    ];

    public static readonly double[] GeneratorCosts =
    [
        1_000, 2_000, 4_000, 7_000, 12_000, 20_000, 31_000, 55_000, 90_000, 138_000, 200_000, 278_000, 375_000, 535_000,
        682_000, 737_000, 797_000, 862_000, 932_000, 1_003_000, 1_075_000, 1_148_000, 1_222_000, 1_297_000, 1_373_000,
        1_450_000, 1_528_000, 1_607_000, 1_687_000, 1_768_000, 1_850_000, 1_933_000, 2_017_000, 2_102_000, 2_188_000,
        2_275_000, 2_363_000, 2_452_000, 2_542_000, 2_633_000, 2_725_000, 2_818_000, 2_912_000,
    ];

    public static readonly (UpgradeKind Kind, double Cost)[] CoreCosts =
    [
        (UpgradeKind.WildFlower, 1_500),
        (UpgradeKind.CoreLink, 2_500),
        (UpgradeKind.Seedling, 3_500),
        (UpgradeKind.WildFlower, 6_000),
        (UpgradeKind.Seedling, 10_000),
        (UpgradeKind.WildFlower, 16_000),
        (UpgradeKind.Seedling, 31_000),
        (UpgradeKind.WildFlower, 46_000),
        (UpgradeKind.Hub, 66_000),
        (UpgradeKind.Seedling, 138_000),
        (UpgradeKind.WildFlower, 250_000),
        (UpgradeKind.Seedling, 682_000),
        (UpgradeKind.WildFlower, 823_000),
        (UpgradeKind.Seedling, 974_000),
        (UpgradeKind.WildFlower, 1_135_000),
        (UpgradeKind.Seedling, 1_306_000),
        (UpgradeKind.WildFlower, 1_487_000),
        (UpgradeKind.Seedling, 1_678_000),
        (UpgradeKind.WildFlower, 1_879_000),
        (UpgradeKind.Seedling, 2_090_000),
        (UpgradeKind.WildFlower, 2_311_000),
        (UpgradeKind.Seedling, 2_542_000),
        (UpgradeKind.WildFlower, 2_783_000),
        (UpgradeKind.Seedling, 3_034_000),
        (UpgradeKind.WildFlower, 3_295_000),
        (UpgradeKind.Seedling, 3_566_000),
        (UpgradeKind.WildFlower, 3_847_000),
        (UpgradeKind.Seedling, 4_138_000),
        (UpgradeKind.WildFlower, 4_439_000),
        (UpgradeKind.Seedling, 4_750_000),
        (UpgradeKind.WildFlower, 5_071_000),
        (UpgradeKind.Seedling, 5_402_000),
        (UpgradeKind.WildFlower, 5_743_000),
        (UpgradeKind.Seedling, 6_094_000),
        (UpgradeKind.WildFlower, 6_455_000),
        (UpgradeKind.Seedling, 6_826_000),
        (UpgradeKind.WildFlower, 7_207_000),
        (UpgradeKind.Seedling, 7_598_000),
        (UpgradeKind.WildFlower, 7_999_000),
        (UpgradeKind.Seedling, 8_410_000),
        (UpgradeKind.WildFlower, 8_831_000),
        (UpgradeKind.Seedling, 9_262_000),
        (UpgradeKind.WildFlower, 9_703_000),
    ];

    // Shaping tools come from the garden's size, the most main nodes it has had (DESIGN.md, Upgrade tracks): a Gravity
    // Pot from the start and another at each of PotSizes, then every PotEvery past the last; a Trellis at TrellisSize
    // and a Gravity Well at each of WellSizes (the second and third for later: Jeremy, Nov 3).
    public static readonly int[] PotSizes = [25, 60, 100];
    public const int PotEvery = 50;
    public const int TrellisSize = 15;
    public static readonly int[] WellSizes = [40, 100, 150];
    // A Sway Controller at SwaySize (Jeremy, Nov 3), whose dial's right end sways the plant SwayMax times as much.
    public const int SwaySize = 60;
    public const float SwayMax = 3;

    // The sway controllers a garden of this size may place.
    public static int SwaysAtSize(int size) => size >= SwaySize ? 1 : 0;

    // The shaping tools a garden of this size may place.
    public static (int Pots, int Trellises, int Wells) ToolsAtSize(int size)
    {
        int pots = StartPotCap + PotSizes.Count(s => size >= s);
        if (size >= PotSizes[^1]) pots += (size - PotSizes[^1]) / PotEvery;
        return (pots, size >= TrellisSize ? 1 : 0, WellSizes.Count(s => size >= s));
    }

    // The size at which a garden of this size gains its next one of a tool (null: never another).
    public static int? NextToolSize(ShapingKind kind, int size) => kind switch
    {
        ShapingKind.Trellis => size < TrellisSize ? TrellisSize : null,
        ShapingKind.Well => WellSizes.Where(s => s > size).Select(s => (int?)s).FirstOrDefault(),
        ShapingKind.Sway => size < SwaySize ? SwaySize : null,
        _ => size < PotSizes[^1] ? PotSizes.First(s => s > size)
            : PotSizes[^1] + ((size - PotSizes[^1]) / PotEvery + 1) * PotEvery,
    };
}
