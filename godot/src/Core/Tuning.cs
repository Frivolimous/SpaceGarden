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
    public const float PinnedFollow = 0.25f;

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
    // of the way to the real one each tick (INTENSITY_V_INC / _DEC). Its level (intensityInterpolation) is the shown
    // fill, never below FillLookFloor; past full it rises a tenth as fast (OverFillLook), up to FillLookMax. Its size
    // is its radius times its level.
    public const float FillLookRise = 0.1f;
    public const float FillLookFall = 0.05f;
    public const float FillLookFloor = 0.2f;
    public const float OverFillLook = 0.1f;
    public const float FillLookMax = 2;

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
    // set the garden's pace (the unlock track does). The economy runs in fixed steps of EconomyStepSeconds of growth
    // time, so a live session, a catch-up and a test all take the same steps whatever chunks the time arrives in. A
    // step is one power transfer: the original's powerDelay, 20 ticks give or take 10% (18.75 here, exact in binary,
    // so the steps don't drift with the chunks).
    public const double EconomyStepSeconds = 0.3125;

    // Each kind's power (store, rate, weight, transfer rate) is in its NodeSpec (NodeSpecs.All), as the original's Nodes
    // config. Making and upkeep both follow the part's fill: a source makes its rate times its fill (none past full),
    // a drain costs its rate times its fill, so nothing ever quite empties. A new part starts empty. Each leaf on a
    // part multiplies its upkeep by LeafUpkeepFactor.
    public const float LeafUpkeepFactor = 0.5f;

    // Placeholder: each flower fruit (a generator's burr, a lab's research, a grove's berry) adds this much to its
    // flower's output, up to the doc's +20% per flower.
    public const float FruitBonusEach = 0.2f / 3;
    public const float FruitBonusMax = 0.2f;

    // Power transfer (the original's attemptTransferPower and POWER_THRESHOLD): each step, a part fuller than SendFill
    // sends its clump of power to its emptiest neighbour that's less full than itself, emptiest by fill over weight
    // (stems, then the core, count as emptier than they are, so power heads out along the stems).
    public const float SendFill = 0.25f;

    // Capacity at the start (DESIGN.md, Linear unlock track: "Start"). The unlock track raises Garden.Capacity.
    public const int StartStemCap = 4;
    public const int StartGeneratorCap = 1;
    public const int StartCoreLinks = 1;

    // ==== BLOBS ==== \\
    // Flowers make blobs at their kind's rate (NodeSpec.BlobsPerHour: a grove one every 3 s, a lab one every 5 s) when
    // full of power, in proportion to their fill when lower (their own fruit add up to FruitBonusMax).
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
    // Placeholders. A completed bud starts this far out (a share of its link's length) and pops out at this speed.
    public const float EmergeStart = 0.3f;
    public const float EmergeSpeed = 1.2f;

    // Gene blobs, from labs, each carry BlobGenes (a whole gene, so they can be counted); only the core absorbs them.
    // Labs are the only source: the first unlock waits for the first lab's genes to find the core.
    public const float BlobGenes = 1;

    // The linear unlock track (DESIGN.md, Progression, "Linear unlock track (step 2)"): genes reaching the core over
    // the garden's life raise the caps at each threshold, in order. Placeholder thresholds, tuned with the headless
    // pacing run (UnlockTests.MonthPacing, retuned by UnlockTests.RetuneThresholds) to land near these times in a
    // garden a smart player tends (DESIGN.md: simulations assume one; see the tests' SmartPlayer). "Day N" is the
    // start of day N. Week one is the doc's draft (31 stems, 8 generators, 2 core links). Weeks two to four are
    // invented, a baseline to pace against until long-term pacing is designed: a step a day in week two, then one
    // every two days, reaching 72 stems, 17 generators and 3 core links at day 30. The player plants the first grove
    // and lab at once, so the first step comes from the first few hours' genes, not the first lab's first blob.
    public static readonly UnlockStep[] UnlockTrack =
    [
        new(5_110, Stems: 3),                              // ~4 h
        new(8_930, Generators: 1),                         // ~6 h
        new(16_600, Stems: 3),                             // day 1 evening (~10 h)
        new(43_600, Stems: 4, Generators: 1),              // day 2
        new(90_500, Stems: 5, Generators: 1, CoreLinks: 1), // day 3
        new(124_000, Stems: 6),                            // day 4
        new(154_000, Generators: 2),                       // day 5
        new(189_000, Stems: 6),                            // day 6
        new(221_000, Generators: 2),                       // day 7: 31 stems, 8 generators, 2 core links
        new(255_000, Stems: 4),                            // day 8
        new(292_000, Generators: 1),                       // day 9
        new(334_000, Stems: 4),                            // day 10
        new(375_000, Generators: 1),                       // day 11
        new(419_000, Stems: 4),                            // day 12
        new(459_000, Generators: 1),                       // day 13
        new(504_000, Stems: 4, CoreLinks: 1),              // day 14: 47 stems, 11 generators, 3 core links
        new(578_000, Stems: 5, Generators: 1),             // day 16
        new(650_000, Stems: 5),                            // day 18
        new(724_000, Generators: 2),                       // day 20
        new(808_000, Stems: 5),                            // day 22
        new(889_000, Generators: 1),                       // day 24
        new(965_000, Stems: 5),                            // day 26
        new(1_040_000, Generators: 2),                     // day 28
        new(1_110_000, Stems: 5),                          // day 30: 72 stems, 17 generators, 3 core links
    ];
}
