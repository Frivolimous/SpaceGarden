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

    // Motes (travelling blobs: genes, and fruit): seconds per link, and the most links one travels.
    public const float MoteLinkSeconds = 0.3f;
    public const int MoteMaxHops = 14;
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

    // Growth in real seconds at speed 1. Wild growth adds a stem or flower about every SproutEverySeconds (it used to
    // sprout every 20 minutes, two thirds of it fruit; fruit now comes from groves' blobs, so the branches keep their
    // old pace). The prototype runs at 60x by default (one a minute) so the growth can be watched. A free Sprout()
    // (stress tests) still grows fruit itself, FruitChance of the time.
    public const double SproutEverySeconds = 60 * 60;
    public const double FruitChance = 2.0 / 3; // about two thirds of a grown plant is fruit (65% at 500 parts)
    public static int MaxNodes = 500; // not const: the --parts dev option raises it for stress tests

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

    // Placeholders. How much power a part holds, and how freely it flows: along a link, power runs downhill from the
    // fuller part to the emptier (by fill, as in the original game), PowerFlowPerHour per hour at a full-to-empty
    // difference. A long branch carrying a lot needs a steep slope, so far tips run low first: power is local.
    public const float NodePowerStore = 1;
    public const float CorePowerStore = 4;
    public const float PowerFlowPerHour = 40;

    // Placeholders. Wild growth needs excess power: a part sprouts only when at least GrowFill full, and the sprout
    // costs its parent SproutCost. Manual buds are free. (Fruit is grown by fruit blobs, below.)
    public const float GrowFill = 0.6f;
    public const float SproutCost = 0.5f;

    // Placeholder. A part that can't pay its upkeep wilts: it stops draining (so the power left reaches the rest) and
    // recovers once it has filled back up to RecoverFill. Nothing is ever destroyed.
    public const float RecoverFill = 0.5f;
    public const float WiltLookPerSecond = 1.5f; // how fast a part's wilted look comes and goes (presentation)

    // Capacity at the start (DESIGN.md, Linear unlock track: "Start"). The unlock track raises Garden.Capacity.
    public const int StartStemCap = 4;
    public const int StartGeneratorCap = 1;
    public const int StartCoreLinks = 1;

    // Placeholders. Wild growth adds a part only if the whole garden could still pay for it with this much power an
    // hour to spare (production less every part's upkeep, wilting ones included, as they'll want paying again), so
    // it can't grow into a deficit. A lab must leave LabSpareShare of the production unspent, so labs never take
    // the power that stems need to reach new room for generators. Generators always pass.
    public const float WildSpareReserve = 1;
    public const float LabSpareShare = 0.3f;

    // ==== GROVES AND FRUIT ==== \\
    // Placeholders. A grove makes fruit blobs while it isn't wilting (its berries add up to FruitBonusMax). Each blob
    // wanders the stems, a random walk that never turns straight back unless at a dead end, and at each part with
    // room for fruit grows one there FruitLandChance of the time (the original's 30%); after FruitBlobMaxHops links
    // (the original's 30) it fades unused, even when no part anywhere has room (the wandering is pretty to see).
    // Tuned for DESIGN.md's target (one or two groves just keep up with new stems' leaves): week one's ~54 leaves
    // plus the flowers' fruit, over about two groves, is roughly a blob every two hours each.
    public const float GroveFruitPerHour = 0.5f;
    public const float FruitLandChance = 0.3f;
    public const int FruitBlobMaxHops = 30;
    // Placeholder. Wild growth adds a grove only while the garden has more than this much free fruit room per grove
    // it already has, so groves stay few and useful (DESIGN.md: "a small number of groves always stays useful").
    public const int GroveFruitRoomEach = 20;

    // ==== GENES AND UNLOCKS ==== \\
    // Placeholders. A lab makes genes while it isn't wilting (its research fruit add up to FruitBonusMax, as a
    // generator's burrs do) and sends them to the core as a purple blob each time it has GeneBlobSize. Labs are the
    // only source: the first unlock waits for the first lab's first blob.
    public const float LabGenesPerHour = 6;  // a blob every 10 minutes (every 10 s at 60x)
    public const float GeneBlobSize = 1;
    public const int MaxMotes = 60; // blobs in flight at once; past it a blob's genes still arrive, unseen

    // The linear unlock track (DESIGN.md, Progression, "Linear unlock track (step 2)"): genes reaching the core over
    // the garden's life raise the caps at each threshold, in order. Placeholder thresholds, tuned with the headless
    // pacing run (UnlockTests.MonthPacing) to land near these times in a typical wild garden; they rise faster than
    // the labs do. "Day N" is the start of day N. Week one is the doc's draft (31 stems, 8 generators, 2 core links),
    // except that its first two steps wait for the first lab (no lab grows before ~2-5 h), not ~10 min and ~1 h.
    // Weeks two to four are invented, a baseline to pace against until long-term pacing is designed: a step a day
    // in week two, then one every two days, reaching 72 stems, 17 generators and 3 core links at day 30. Lowered
    // when groves came: fruit now costs a grove's upkeep rather than 0.2 power a sprout, so gardens run leaner.
    public static readonly UnlockStep[] UnlockTrack =
    [
        new(1, Stems: 3),                                  // the first lab's first blob (~4 h)
        new(12, Generators: 1),                            // ~6 h
        new(45, Stems: 3),                                 // day 1 evening (~10 h)
        new(130, Stems: 4, Generators: 1),                 // day 2
        new(440, Stems: 5, Generators: 1, CoreLinks: 1),   // day 3
        new(970, Stems: 6),                                // day 4
        new(1500, Generators: 2),                          // day 5
        new(2210, Stems: 6),                               // day 6
        new(2990, Generators: 2),                          // day 7: 31 stems, 8 generators, 2 core links
        new(4050, Stems: 4),                               // day 8
        new(5150, Generators: 1),                          // day 9
        new(6440, Stems: 4),                               // day 10
        new(7830, Generators: 1),                          // day 11
        new(9240, Stems: 4),                               // day 12
        new(10750, Generators: 1),                         // day 13
        new(12330, Stems: 4, CoreLinks: 1),                // day 14: 47 stems, 11 generators, 3 core links
        new(15410, Stems: 5, Generators: 1),               // day 16
        new(18830, Stems: 5),                              // day 18
        new(22510, Generators: 2),                         // day 20
        new(26400, Stems: 5),                              // day 22
        new(30470, Generators: 1),                         // day 24
        new(34880, Stems: 5),                              // day 26
        new(39470, Generators: 2),                         // day 28
        new(44000, Stems: 5),                              // day 30: 72 stems, 17 generators, 3 core links
    ];
}
