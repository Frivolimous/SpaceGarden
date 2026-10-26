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

    // Motes (travelling blobs, for fruit and genes to come): seconds per link, and the most links one travels.
    public const float MoteLinkSeconds = 0.3f;
    public const int MoteMaxHops = 14;
    public const float GlowFadePerSecond = 1.5f;

    // Power shows as link flashes, as in the original game: a flash fades in about 0.3 s (its 300 ms tween). Placeholder
    // rate: a link carrying 1 power an hour flashes about every 8 s, a trunk carrying 8 about once a second, never more
    // than 3 a second (the original's fastest, one transfer every 20 ticks).
    public const float LinkFlashFadePerSecond = 3.3f;
    public const float FlashesPerPowerHour = 0.125f;
    public const float MaxFlashesPerSecond = 3;

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

    // Growth in real seconds at speed 1: a new part about every 20 minutes, so a full prototype garden (500 parts)
    // takes about a week. The prototype runs at 60x by default (one every 20 s) so the growth can be watched.
    public const double SproutEverySeconds = 20 * 60;
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
    public const float GroveUpkeepPerHour = 1.5f;  // no groves yet; kept so labs stay at twice a grove
    public const float LabUpkeepPerHour = 2 * GroveUpkeepPerHour;

    // Placeholder: each generator fruit (burr) adds this much output, up to the doc's +20% per flower.
    public const float FruitBonusEach = 0.2f / 3;
    public const float FruitBonusMax = 0.2f;

    // Placeholders. How much power a part holds, and how freely it flows: along a link, power runs downhill from the
    // fuller part to the emptier (by fill, as in the original game), PowerFlowPerHour per hour at a full-to-empty
    // difference. A long branch carrying a lot needs a steep slope, so far tips run low first: power is local.
    public const float NodePowerStore = 1;
    public const float CorePowerStore = 4;
    public const float PowerFlowPerHour = 40;

    // Placeholders. Wild growth needs excess power: a part sprouts only when at least GrowFill full, and the sprout
    // costs its parent SproutCost (FruitSproutCost for fruit, until fruit blobs grow it). Manual buds are free.
    public const float GrowFill = 0.8f;
    public const float SproutCost = 0.5f;
    public const float FruitSproutCost = 0.2f;

    // Placeholder. A part that can't pay its upkeep wilts: it stops draining (so the power left reaches the rest) and
    // recovers once it has filled back up to RecoverFill. Nothing is ever destroyed.
    public const float RecoverFill = 0.5f;
    public const float WiltLookPerSecond = 1.5f; // how fast a part's wilted look comes and goes (presentation)

    // Capacity at the start (DESIGN.md, Linear unlock track: "Start"). The unlock track raises Garden.Capacity.
    public const int StartStemCap = 4;
    public const int StartGeneratorCap = 1;
    public const int StartCoreLinks = 1;
}
