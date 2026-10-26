namespace SpaceGarden.Core;

// Every number that shapes the feel, in one place. Physics values are per 60 Hz tick, as in the TS game's
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

    // Power shows as link flashes, as in the original game: a flash fades in about 0.3 s (its 300 ms tween). Placeholder
    // rate: a link carrying 1 power an hour flashes about every 8 s, a trunk carrying 8 about once a second, never more
    // than 3 a second (the original's fastest, one transfer every 20 ticks).
    public const float LinkFlashFadePerSecond = 3.3f;
    public const float FlashesPerPowerHour = 0.125f;
    public const float MaxFlashesPerSecond = 3;
    // Placeholder. A flash's brightness is this times the fill of the part the power reached (1: a full part's flash
    // is as bright as the original's).
    public const float FlashPeak = 0.6f;

    // A part's brightness and size follow its fill, as in the original (PlantNodeView): the shown fill moves a share
    // of the way to the real one each tick (INTENSITY_V_INC / _DEC), and never shows below FillLookFloor.
    public const float FillLookRise = 0.1f;
    public const float FillLookFall = 0.05f;
    public const float FillLookFloor = 0.2f;

    // Placeholder. Each flash of power arriving kicks a part's bounce, a damped spring (about 3 swings a second,
    // lightly damped): it swells past its level by about BounceKick / 25, dips a little below, and settles.
    public const float BounceKick = 3f;
    public const float BounceStiffness = 360;
    public const float BounceDamping = 13;
    public const float BounceMax = 0.3f;

    // Growth is in real seconds at speed 1; the prototype runs at 60x by default so the growth can be watched. A free
    // Sprout() (the first stem, stress tests) grows fruit FruitChance of the time.
    public const double FruitChance = 2.0 / 3; // about two thirds of a grown plant is fruit (65% at 500 parts)
    public static int MaxNodes = 500; // not const: the --parts dev option raises it for stress tests

    // Placeholder. How long the last prune can be undone, in real seconds (counted in physics ticks, so the same at
    // any growth speed). Growth blobs start nothing new meanwhile, so the room the prune freed is still there.
    public const double UndoPruneSeconds = 30;

    // Time away is caught up to at most this much real time.
    public const double CatchUpCapSeconds = 72 * 3600;
    public const int SettleTicks = 900;

    // ==== ECONOMY ==== \\
    // Power rates are per real hour (DESIGN.md, Economy: draft values, to be tuned through play). The economy runs
    // in fixed steps of EconomyStepSeconds of growth time, so a live session, a catch-up and a test all take the same
    // steps whatever chunks the time arrives in.
    public const double EconomyStepSeconds = 10;

    public const float CorePowerPerHour = 8;
    public const float GeneratorPowerPerHour = 4;  // half the core's
    public const float StemUpkeepPerHour = 0.5f;   // halved by each leaf: 0.25 with one, 0.125 with two
    public const float LeafUpkeepFactor = 0.5f;
    public const float GroveUpkeepPerHour = 1.5f;
    public const float LabUpkeepPerHour = 2 * GroveUpkeepPerHour;

    // Placeholder: each flower fruit (a generator's burr, a lab's research, a grove's berry) adds this much to its
    // flower's output, up to the doc's +20% per flower.
    public const float FruitBonusEach = 0.2f / 3;
    public const float FruitBonusMax = 0.2f;

    // Placeholder. Generators, labs and groves work at this share of their rate when empty, rising to full when
    // full (none while wilting).
    public const float OutputFloor = 0.5f;

    // Placeholders. How much power a part holds, and how freely it flows: along a link, power runs downhill from the
    // fuller part to the emptier (by fill, as in the original game), PowerFlowPerHour per hour at a full-to-empty
    // difference. A long branch carrying a lot needs a steep slope, so far tips run low first: power is local.
    public const float NodePowerStore = 1;
    public const float CorePowerStore = 4;
    public const float PowerFlowPerHour = 40;

    // Placeholder. The power a new part starts with (free: buds and grown parts don't cost their parent anything).
    public const float SproutPower = 0.5f;

    // Placeholder. A part that can't pay its upkeep wilts: it stops draining (so the power left reaches the rest) and
    // recovers once it has filled back up to RecoverFill. Nothing is ever destroyed.
    public const float RecoverFill = 0.5f;
    public const float WiltLookPerSecond = 1.5f; // how fast a part's wilted look comes and goes (presentation)

    // Capacity at the start (DESIGN.md, Linear unlock track: "Start"). The unlock track raises Garden.Capacity.
    public const int StartStemCap = 4;
    public const int StartGeneratorCap = 1;
    public const int StartCoreLinks = 1;

    // ==== BLOBS ==== \\
    // Draft rates (DESIGN.md, Economy, Blobs), to tune in play. Flowers make blobs at these rates when full of power,
    // in proportion to their fill when lower, and none while wilting (their own fruit add up to FruitBonusMax).
    // Both kinds wander the stems at random, never turning straight back unless at a dead end, and fade after
    // BlobMaxHops links (the original's 30) if nothing absorbed them.
    public const int BlobMaxHops = 30;
    public const int MaxMotes = 60; // blobs in flight at once; past it a blob still does its work, unseen

    // Growth blobs, from groves: one every 3 minutes (every 3 s at 60x), each carrying BlobGrowth. A part with
    // nothing forming absorbs one AbsorbChance of the time (the original's 30%), a part with something forming
    // always does. A fresh one starts a bud BudChance of the time where a branch could grow, else a fruit; the fruit
    // completes at FruitGrowth, the bud at BudGrowth.
    public const float GroveBlobsPerHour = 20;
    public const float BlobGrowth = 0.1f;
    public const float AbsorbChance = 0.3f;
    public const float BudChance = 0.5f;
    public const float FruitGrowth = 1;
    public const float BudGrowth = 3;
    // Placeholders. A completed bud starts this far out (a share of its link's length) and pops out at this speed.
    public const float EmergeStart = 0.3f;
    public const float EmergeSpeed = 1.2f;

    // Gene blobs, from labs: one every 5 minutes (every 5 s at 60x), each carrying BlobGenes; only the core absorbs
    // them. Labs are the only source: the first unlock waits for the first lab's first blob to find the core.
    public const float LabBlobsPerHour = 12;
    public const float BlobGenes = 0.5f;

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
        new(18, Stems: 3),                                 // ~4 h
        new(37, Generators: 1),                            // ~6 h
        new(74, Stems: 3),                                 // day 1 evening (~10 h)
        new(200, Stems: 4, Generators: 1),                 // day 2
        new(410, Stems: 5, Generators: 1, CoreLinks: 1),   // day 3
        new(850, Stems: 6),                                // day 4
        new(1250, Generators: 2),                          // day 5
        new(1740, Stems: 6),                               // day 6
        new(2190, Generators: 2),                          // day 7: 31 stems, 8 generators, 2 core links
        new(2660, Stems: 4),                               // day 8
        new(3110, Generators: 1),                          // day 9
        new(3550, Stems: 4),                               // day 10
        new(4000, Generators: 1),                          // day 11
        new(4450, Stems: 4),                               // day 12
        new(4900, Generators: 1),                          // day 13
        new(5350, Stems: 4, CoreLinks: 1),                 // day 14: 47 stems, 11 generators, 3 core links
        new(6510, Stems: 5, Generators: 1),                // day 16
        new(7670, Stems: 5),                               // day 18
        new(8840, Generators: 2),                          // day 20
        new(10030, Stems: 5),                              // day 22
        new(11210, Generators: 1),                         // day 24
        new(12400, Stems: 5),                              // day 26
        new(13590, Generators: 2),                         // day 28
        new(14790, Stems: 5),                              // day 30: 72 stems, 17 generators, 3 core links
    ];
}
