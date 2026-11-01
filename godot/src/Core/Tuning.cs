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

    // ==== WILD FLOWERS ==== \
    // Placeholders, as every wild flower number here (DESIGN.md, Wild flowers). The balance target: a wild flower slot
    // is worth about 0.65 labs, so each kind should be worth its base plus about that; upkeep and transfer are the
    // fine-tuning dials. The chance a new bud grows wild, per free wild flower slot, by how it started: a passive
    // bud's and a planted one's.
    public const float PassiveWildChance = 0.05f;
    public const float PlantedWildChance = 0.04f;

    // Each kind's own numbers (the kinds in WildFlowers.cs). Amplifier: each one a gene or growth blob passes doubles
    // it AmplifyChance of the time, once a blob at most (50% larger on average, the blobs still whole).
    public const float AmplifyChance = 0.5f;
    // Resistor: blobs never step onto it, from either side; it sends power on at ResistorTransfer a second (a stem 30).
    public const float ResistorTransfer = 12;
    // Tendril: its link TendrilLink times as long and TendrilSpring as springy; holds TendrilStore (a stem 150) and
    // sends on at TendrilTransfer.
    public const float TendrilLink = 2.5f, TendrilSpring = 0.3f, TendrilStore = 600, TendrilTransfer = 40;
    // Big Stem: 4 links, sending power on at BigStemTransfer (a stem 30).
    public const float BigStemTransfer = 15;
    // Pulsar: makes PulsarGen a second (1.5 generators) while below full and holds PulsarStore; it sends nothing until
    // it's full, then PulsarBurst of its power at once (Jeremy, Nov 1; it was built up unseen and released every 20 s),
    // which its card shows. PulsarTransfer is unused for now (the Hub's and Twin Stars' transfer boosts touch it).
    public const float PulsarGen = 18, PulsarStore = 400, PulsarTransfer = 24, PulsarBurst = 0.5f;
    // Twin Star: a generator with 2 links, making TwinStarGen (80% of a generator's 12).
    public const float TwinStarGen = 9.6f;
    // Ember: makes EmberGen (twice a generator) for its fill, but the power it can hold falls in a straight line from
    // its store to nothing over EmberLifeSeconds (Jeremy, Nov 1), so it dims and makes less as it goes (level with a
    // generator about halfway, at a day and a half), then burns out.
    public const float EmberGen = 24, EmberLifeSeconds = 3 * 86400;
    // Lumber Grove: LumberBlobs growth blobs an hour (2 groves) at LumberUpkeep a second (1.5 groves).
    public const float LumberBlobs = 2400, LumberUpkeep = 8;
    // Seed Pod: SeedPodBlobs an hour (1.5 groves), but they leave SeedPodBurst at once (every 2 minutes at full fill).
    public const float SeedPodBlobs = 1800;
    public const int SeedPodBurst = 60;
    // Wild Lab: a lab's genes, but each of its WildLabFruits research counts WildLabFruitScale times (+20%): twice a
    // bare lab with all five, against a full lab's 1.3.
    public const float WildLabFruitScale = 2;
    // Lichen: LichenBlobs gene blobs an hour (0.8 of a lab) at LichenUpkeep a second (a lab 6); its 4 leaves, a quarter
    // off each, bring its upkeep to 0.
    public const float LichenBlobs = 576, LichenUpkeep = 3;

    // The passive-only kinds, white. Buffer: BufferBlobs buff blobs an hour (a glow fruit GlowBlobs, a quarter of
    // that); each wanders BufferReach links (BufferAmplifiedReach more once it passes an Amplifier), and every part it
    // passes makes BuffBonus more (power and blobs) for the next BuffSeconds. A second buff only starts the time again.
    public const float BufferBlobs = 240, GlowBlobs = BufferBlobs / 4, BuffBonus = 0.5f, BuffSeconds = 60;
    public const int BufferReach = 5, BufferAmplifiedReach = 3;
    // Comet Bloom: as it appears, a burst of gene and growth blobs worth CometHours of a lab and a grove (their blobs
    // mixed in that share); withers after CometLifeSeconds.
    public const int CometHours = 2;
    public const float CometLifeSeconds = 86400;
    // Moonflower: each makes buds within MoonflowerReach links MoonflowerBoost more likely to grow wild (three times
    // as likely, with one), and wild flowers within that reach make MoonlitBonus more (power and blobs; not stacking).
    public const int MoonflowerReach = 5;
    public const float MoonflowerBoost = 2, MoonlitBonus = 0.1f;
    // Nebula Bloom: power reaching a seedling within NebulaReach links counts NebulaFill times (not stacking).
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
    // HubTripleChance of the time), the Buffer's (every buff HubBuffStrength times as strong), the Moonflower's (its
    // reach HubMoonflowerReach links) and the Nebula Bloom's (power to its seedlings counts HubNebulaFill times).
    public const double HubFirstPoint = 3_000, HubPointStep = 400;
    public const float HubUpkeep = 3, HubUpkeepPerLevel = 0.5f, HubUpkeepCut = 0.8f;
    public const int HubStemCap = 2, HubMoonflowerReach = 8;
    public const float HubTripleChance = 0.2f, HubBuffStrength = 1.1f, HubNebulaFill = 3;

    // ==== RESEARCH ==== \\
    // The three upgrade tracks (DESIGN.md, Upgrade tracks): genes from the balance buy each track's next upgrade, in
    // order. Placeholders for playtesting (Jeremy's balance pass, Nov 1: dearer overall than 5b's, the early steps
    // most). Stems and generators each raise their cap by 1 (to 47 stems and 44 generators); the core's track is its
    // own list, the Hub ninth. Past these each track ends, as long-term pacing isn't designed yet.
    public static readonly double[] StemCosts =
    [
        100, 150, 250, 450, 750, 1_150, 1_650, 2_300, 3_150, 4_250, 5_650, 7_400, 9_550, 12_150, 15_250, 18_900, 23_150,
        28_050, 33_650, 40_000, 47_150, 55_150, 64_050, 73_900, 84_750, 96_650, 109_650, 123_800, 139_150, 155_750,
        173_650, 192_900, 213_550, 235_650, 259_250, 284_400, 311_150, 339_550, 369_650, 401_500, 435_150, 470_650,
        508_050,
    ];

    public static readonly double[] GeneratorCosts =
    [
        1_000, 2_000, 3_000, 5_000, 9_000, 15_000, 25_000, 40_000, 60_000, 85_000, 115_000, 150_000, 190_000, 235_000,
        285_000, 340_000, 400_000, 465_000, 535_000, 606_000, 678_000, 751_000, 825_000, 900_000, 976_000, 1_053_000,
        1_131_000, 1_210_000, 1_290_000, 1_371_000, 1_453_000, 1_536_000, 1_620_000, 1_705_000, 1_791_000, 1_878_000,
        1_966_000, 2_055_000, 2_145_000, 2_236_000, 2_328_000, 2_421_000, 2_515_000,
    ];

    public static readonly (UpgradeKind Kind, double Cost)[] CoreCosts =
    [
        (UpgradeKind.WildFlower, 1_500),
        (UpgradeKind.CoreLink, 2_500),
        (UpgradeKind.Seedling, 3_500),
        (UpgradeKind.WildFlower, 6_000),
        (UpgradeKind.Seedling, 12_000),
        (UpgradeKind.WildFlower, 21_000),
        (UpgradeKind.Seedling, 32_000),
        (UpgradeKind.WildFlower, 43_000),
        (UpgradeKind.Hub, 54_000),
        (UpgradeKind.Seedling, 165_000),
        (UpgradeKind.WildFlower, 286_000),
        (UpgradeKind.Seedling, 417_000),
        (UpgradeKind.WildFlower, 558_000),
        (UpgradeKind.Seedling, 709_000),
        (UpgradeKind.WildFlower, 870_000),
        (UpgradeKind.Seedling, 1_041_000),
        (UpgradeKind.WildFlower, 1_222_000),
        (UpgradeKind.Seedling, 1_413_000),
        (UpgradeKind.WildFlower, 1_614_000),
        (UpgradeKind.Seedling, 1_825_000),
        (UpgradeKind.WildFlower, 2_046_000),
        (UpgradeKind.Seedling, 2_277_000),
        (UpgradeKind.WildFlower, 2_518_000),
        (UpgradeKind.Seedling, 2_769_000),
        (UpgradeKind.WildFlower, 3_030_000),
        (UpgradeKind.Seedling, 3_301_000),
        (UpgradeKind.WildFlower, 3_582_000),
        (UpgradeKind.Seedling, 3_873_000),
        (UpgradeKind.WildFlower, 4_174_000),
        (UpgradeKind.Seedling, 4_485_000),
        (UpgradeKind.WildFlower, 4_806_000),
        (UpgradeKind.Seedling, 5_137_000),
        (UpgradeKind.WildFlower, 5_478_000),
        (UpgradeKind.Seedling, 5_829_000),
        (UpgradeKind.WildFlower, 6_190_000),
        (UpgradeKind.Seedling, 6_561_000),
        (UpgradeKind.WildFlower, 6_942_000),
        (UpgradeKind.Seedling, 7_333_000),
        (UpgradeKind.WildFlower, 7_734_000),
        (UpgradeKind.Seedling, 8_145_000),
        (UpgradeKind.WildFlower, 8_566_000),
        (UpgradeKind.Seedling, 8_997_000),
        (UpgradeKind.WildFlower, 9_438_000),
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
