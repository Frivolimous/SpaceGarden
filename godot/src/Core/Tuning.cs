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

    // Motes (travelling blobs: genes, and growth): seconds per link.
    public const float MoteLinkSeconds = 0.3f;
    public const float GlowFadePerSecond = 1.5f;

    // Power shows as link flashes, as in the original game: each transfer lights its link, and the flash fades in
    // about 0.3 s (its 300 ms tween). A transfer's flash shows at a random moment within its economy step, so links
    // don't all flash in step (the original's ±10% timing). Placeholder: a flash is FlashPeak times as bright as the
    // part the power reached is full (1: a full part's flash is as bright as the original's).
    public const float LinkFlashFadePerSecond = 3.3f;
    public const float FlashPeak = 0.6f;

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
    public const int StartSeedlings = 1;
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
    // Placeholders. A completed bud starts this far out (a share of its link's length) and pops out at this speed.
    public const float EmergeStart = 0.3f;
    public const float EmergeSpeed = 1.2f;

    // Gene blobs, from labs, each carry BlobGenes (a whole gene, so they can be counted); only the core absorbs them.
    // Labs are the only source: the first upgrade waits for the first lab's genes to find the core.
    public const float BlobGenes = 1;

    // ==== WILD FLOWERS ==== \\
    // The chance a new bud grows wild, per free wild flower slot, by how it started (DESIGN.md, Wild flowers): a
    // passive bud's and a planted one's. Placeholders, unused until step 5d.
    public const float PassiveWildChance = 0.05f;
    public const float PlantedWildChance = 0.04f;

    // ==== RESEARCH ==== \\
    // The three upgrade tracks (DESIGN.md, Upgrade tracks): genes from the balance buy each track's next upgrade, in
    // order. Placeholders for playtesting. Stems and generators each raise their cap by 1 (to 30 stems and 10
    // generators); the core's track is its own list. Past week one is long-term pacing, not designed yet, so each track
    // ends: the core's last six (alternating seedling slots and the wild flower limit) are placeholders set in 5b.
    public static readonly double[] StemCosts =
    [
        100, 150, 200, 300, 450, 650, 950, 1_350, 1_850, 2_450, 3_150, 3_950, 4_850, 5_850, 6_950, 8_150, 9_450,
        10_850, 12_350, 13_950, 15_700, 17_600, 19_650, 21_850, 24_200, 26_700,
    ];

    public static readonly double[] GeneratorCosts = [1_000, 2_000, 3_000, 5_000, 10_000, 15_000, 20_000, 25_000, 30_000];

    public static readonly (UpgradeKind Kind, double Cost)[] CoreCosts =
    [
        (UpgradeKind.WildFlower, 1_500),
        (UpgradeKind.CoreLink, 4_000),
        (UpgradeKind.WildFlower, 10_000),
        (UpgradeKind.Seedling, 20_000),
        (UpgradeKind.WildFlower, 35_000),
        (UpgradeKind.Hub, 60_000),
        (UpgradeKind.Seedling, 90_000),
        (UpgradeKind.WildFlower, 120_000),
        (UpgradeKind.Seedling, 160_000),
        (UpgradeKind.WildFlower, 210_000),
        (UpgradeKind.Seedling, 270_000),
        (UpgradeKind.WildFlower, 340_000),
    ];

    // Shaping tools come from the garden's size, the most main nodes it has had (DESIGN.md, Upgrade tracks): a Gravity
    // Pot from the start and another at each of PotSizes, then every PotEvery past the last; a Trellis at TrellisSize
    // and a Gravity Well at WellSize.
    public static readonly int[] PotSizes = [25, 60, 100];
    public const int PotEvery = 50;
    public const int TrellisSize = 15;
    public const int WellSize = 40;

    // The shaping tools a garden of this size may place.
    public static (int Pots, int Trellises, int Wells) ToolsAtSize(int size)
    {
        int pots = StartPotCap + PotSizes.Count(s => size >= s);
        if (size >= PotSizes[^1]) pots += (size - PotSizes[^1]) / PotEvery;
        return (pots, size >= TrellisSize ? 1 : 0, size >= WellSize ? 1 : 0);
    }

    // The size at which a garden of this size gains its next one of a tool (null: never another).
    public static int? NextToolSize(ShapingKind kind, int size) => kind switch
    {
        ShapingKind.Trellis => size < TrellisSize ? TrellisSize : null,
        ShapingKind.Well => size < WellSize ? WellSize : null,
        _ => size < PotSizes[^1] ? PotSizes.First(s => s > size)
            : PotSizes[^1] + ((size - PotSizes[^1]) / PotEvery + 1) * PotEvery,
    };
}
