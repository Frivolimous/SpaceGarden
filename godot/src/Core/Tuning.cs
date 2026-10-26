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

    // Motes: seconds per link, how often the core and each generator release one, and the most links one travels.
    public const float MoteLinkSeconds = 0.3f;
    public const float CoreMoteEvery = 0.9f;
    public const float GeneratorMoteEvery = 4f;
    public const int MoteMaxHops = 14;
    public const float GlowFadePerSecond = 1.5f;
    public const float LinkFlashFadePerSecond = 3.3f;

    // Growth in real seconds at speed 1: a new part about every 20 minutes, so a full prototype garden takes
    // a few days. The prototype runs at 60x by default (one every 20 s) so the growth can be watched.
    public const double SproutEverySeconds = 20 * 60;
    public const double FruitChance = 0.35;
    public static int MaxNodes = 220; // not const: the --parts dev option raises it for stress tests

    // Time away is caught up to at most this much real time.
    public const double CatchUpCapSeconds = 72 * 3600;
    public const int SettleTicks = 900;
}
