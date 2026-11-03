namespace SpaceGarden.Core;

// The wild flowers (DESIGN.md, Wild flowers): a new bud, planted or passive, may grow into one of these instead of
// its basic kind. Each is its base kind's spec with its own name, look, fruit and quirk; the passive-only kinds have no
// base and are white (so are the Amplifier and the Resistor). A stem or generator kind (but the Amplifier and the Resistor) takes its base kind's slot as
// well as a wild flower slot. Description is the table's Effect, the Codex's line; WildSpec.Quirk is the card's. Their
// numbers are Jeremy's table of Nov 3, here; their quirks' are in Tuning (Wild flowers). Their fruit: every fruit adds a
// tenth to its flower's main effect (wood, from the Lumber Grove, too: crawlers will use it later), but leaves (a
// quarter off its upkeep) and glow, a white fruit that sends buff blobs, one a minute.
static class WildFlowers
{
    public static IEnumerable<NodeSpec> All(IReadOnlyDictionary<NodeKind, NodeSpec> basic)
    {
        var stem = basic[NodeKind.Stem];
        var generator = basic[NodeKind.Generator];
        var grove = basic[NodeKind.Grove];
        var lab = basic[NodeKind.Lab];

        yield return new NodeSpec(NodeKind.Wood)
        {
            Name = "Wood", Description = "Raises its Lumber Grove's growth output a little. Crawlers will use it later.",
            Radius = 5, Mass = 0.5f, Force = 0.2f, MaxLinks = 0, Shape = Shape.Square, Rgb = 0x9A6232,
            PowerStore = NodeBase.FruitStore, FlowerBonus = 0.1f,
        };
        yield return new NodeSpec(NodeKind.Glow)
        {
            Name = "Glow", Description = "Sends buff blobs, as a small Buffer.",
            Radius = 5, Mass = 0.5f, Force = 0.2f, MaxLinks = 0, Shape = Shape.Burst8, Rgb = 0xF6F6F0,
            PowerStore = NodeBase.FruitStore, Sends = BlobKind.Buff, BlobsPerHour = Tuning.GlowBlobs,
        };

        // The passive-only kinds are small flowers of their own, at the end of a branch, with glow fruit.
        var passive = new NodeSpec(NodeKind.Buffer)
        {
            Name = "", Description = "",
            Radius = 13, Mass = 2, Force = 1.5f, MaxLinks = 1, Shape = Shape.Circle, Rgb = 0xF0F0F0,
            Fruit = NodeKind.Glow, PowerToFruitPerSecond = NodeBase.PowerToFruit,
            PowerStore = 150, PowerPerSecond = -6, PowerTransferPerSecond = 15,
        };

        NodeSpec Wild(NodeSpec basis, NodeKind kind, string name, Shape shape, uint rgb, WildUnlock unlock, string effect,
            string quirk)
        {
            bool passiveOnly = ReferenceEquals(basis, passive);
            bool capped = basis.Kind is NodeKind.Stem or NodeKind.Generator;
            return basis with
            {
                Kind = kind, Name = name, Description = effect, Shape = shape, Rgb = rgb,
                Wild = new WildSpec(passiveOnly ? null : basis.Kind, unlock, quirk, BaseSlot: capped && !passiveOnly),
            };
        }

        // Every kind can grow wild from the first wild flower slot (Jeremy, Nov 3: unlocking the Big Stem, Pulsar and
        // Twin Star by evolution was anticlimactic; the core track only adds slots). Each kind's store, power made (+)
        // or upkeep (-), power weight, transfer, links and fruit are Jeremy's table of Nov 3.

        // The stem kinds. The Amplifier and the Resistor take only a wild flower slot, and are white, the specialists'
        // hue (Jeremy, Oct 30).
        var amplifier = Wild(stem, NodeKind.Amplifier, "Amplifier", Shape.Plus, 0xF2F2F2, WildUnlock.Start,
            "Gene and growth blobs passing through have a 50% chance to double in value.", "2 links: it can't fork. High upkeep.");
        yield return amplifier with
        {
            Radius = 11, MaxLinks = 2, Fruit = NodeKind.Glow, MaxFruits = 1, DoublesBlobs = true,
            PowerStore = 150, PowerPerSecond = -12, PowerWeight = 1.1f, PowerTransferPerSecond = 20,
            Wild = amplifier.Wild! with { BaseSlot = false },
        };
        var resistor = Wild(stem, NodeKind.Resistor, "Resistor", Shape.Minus, 0xF2F2F2, WildUnlock.Start,
            "Blobs bounce off it from both sides.", "Low transfer: slows power beyond it.");
        yield return resistor with
        {
            Radius = 7, MaxLinks = 2, Fruit = NodeKind.Glow, MaxFruits = 1, BlocksBlobs = true,
            PowerStore = 150, PowerPerSecond = -6, PowerWeight = 1.1f, PowerTransferPerSecond = 10,
            Wild = resistor.Wild! with { BaseSlot = false },
        };
        yield return Wild(stem, NodeKind.Tendril, "Tendril", Shape.Trefoil, 0x78E010, WildUnlock.Start,
            "A long, springy link, a large store and a fast, steady transfer.", "Takes a stem slot too.") with
        {
            Radius = 8, LinkScale = Tuning.TendrilLink, Spring = Tuning.TendrilSpring, MaxFruits = 3,
            PowerStore = 300, PowerPerSecond = -6, PowerWeight = 1.1f, PowerTransferPerSecond = 60,
            TransferSplits = Tuning.TendrilSplits,
        };
        yield return Wild(stem, NodeKind.BigStem, "Big Stem", Shape.Quatrefoil, 0x10C838, WildUnlock.Start,
            "4 links.", "Slower transfer; takes a stem slot too.") with
        {
            Radius = 13, Mass = 1.5f, MaxLinks = 4,
            PowerStore = 250, PowerPerSecond = -6, PowerWeight = 1.1f, PowerTransferPerSecond = 25,
        };

        // The generator kinds, each taking a generator slot too.
        yield return Wild(generator, NodeKind.Pulsar, "Pulsar", Shape.Star5, 0xF2D84A, WildUnlock.Start,
            "Twice a generator's power, released in bursts.", "Uneven supply; takes a generator slot too.") with
        {
            PowerStore = 200, PowerPerSecond = 24, PowerWeight = 0.9f,
            BurstShare = Tuning.PulsarBurst, BurstFill = Tuning.PulsarBurstFill,
        };
        yield return Wild(generator, NodeKind.TwinStar, "Twin Star", Shape.Star10, 0xE8EE8C, WildUnlock.Start,
            "A generator that continues the branch: 2 links.", "Takes a generator slot too.") with
        {
            MaxLinks = 2, Fruit = NodeKind.Berry,
            PowerStore = 200, PowerPerSecond = 12, PowerWeight = 1.1f, PowerTransferPerSecond = 42,
        };
        yield return Wild(generator, NodeKind.Ember, "Ember", Shape.Flame, 0xF0BE50, WildUnlock.Start,
            "Starts at 2.5 times a generator's power, fading as it goes.",
            "Fades over a day, then burns out unless pruned first; takes a generator slot too.") with
        {
            LifeSeconds = Tuning.EmberLifeSeconds, Dims = true, Fruit = null, MaxFruits = 0,
            PowerStore = 200, PowerPerSecond = 30, PowerWeight = 1, PowerTransferPerSecond = 54,
        };

        // The grove kinds. A growth blob of BlobValue 2 carries 0.2 growth, twice a grove's.
        yield return Wild(grove, NodeKind.LumberGrove, "Lumber Grove", Shape.Star8, 0xC8661E, WildUnlock.Start,
            "Growth blobs twice a grove's size, a little less often.", "Higher upkeep.") with
        {
            Fruit = NodeKind.Wood, MaxFruits = 5, BlobsPerHour = 3600 / 3.5f, BlobValue = 2,
            PowerStore = 300, PowerPerSecond = -7, PowerTransferPerSecond = 15,
        };
        yield return Wild(grove, NodeKind.SeedPod, "Seed Pod", Shape.Oval, 0xEA8A34, WildUnlock.Start,
            "20 growth blobs, each twice a grove's size, every minute.", "Silent in between.") with
        {
            BlobsPerHour = 1200, BlobBurst = 20, BlobValue = 2,
            PowerStore = 400, PowerPerSecond = -5, PowerTransferPerSecond = 15,
        };

        // The lab kinds. A gene blob of BlobValue 2 carries 2 genes.
        yield return Wild(lab, NodeKind.WildLab, "Wild Lab", Shape.Star6, 0xA478F0, WildUnlock.Start,
            "2 genes a blob every 6 s, and room for 6 research fruit.", "High upkeep.") with
        {
            MaxFruits = 6, BlobsPerHour = 600, BlobValue = 2,
            PowerStore = 300, PowerPerSecond = -12, PowerTransferPerSecond = 15,
        };
        yield return Wild(lab, NodeKind.Lichen, "Lichen", Shape.Lobes6, 0x8C84D4, WildUnlock.Start,
            "2 genes a blob every 6 s; its 4 leaves bring its upkeep to 0.", "Sends no power on.") with
        {
            Radius = stem.Radius, Fruit = NodeKind.Leaf, MaxFruits = 4, BlobsPerHour = 600, BlobValue = 2,
            PowerStore = 150, PowerPerSecond = -6, PowerTransferPerSecond = 0,
        };

        // The passive-only kinds.
        yield return Wild(passive, NodeKind.Buffer, "Buffer", Shape.Burst8, 0xF2F2F2, WildUnlock.Start,
            "Buff blobs give the parts they pass 50% more for 30 s.", "Local only: 5 links (8 past an Amplifier).") with
        {
            Sends = BlobKind.Buff, BlobsPerHour = Tuning.BufferBlobs, MaxFruits = 2, PowerPerSecond = -10,
        };
        yield return Wild(passive, NodeKind.CometBloom, "Comet Bloom", Shape.Gear, 0xFFFFFF, WildUnlock.Start,
            "10 minutes of double-value gene and growth blobs, and buffs twice as long.",
            "Then dormant, fading over 6 hours, and withers.") with
        {
            MaxFruits = 2, PowerStore = 200, Dims = true,
            LifeSeconds = Tuning.CometActiveSeconds + Tuning.CometFadeSeconds, ActiveSeconds = Tuning.CometActiveSeconds,
        };
        yield return Wild(passive, NodeKind.Moonflower, "Moonflower", Shape.Rose, 0xE6ECFA, WildUnlock.Start,
            "Buds nearby grow wild twice as often; wild flowers near it make 25% more.", "Takes a slot.") with
        {
            WildBoost = Tuning.MoonflowerBoost, MaxFruits = 3, PowerStore = 400, PowerPerSecond = -10,
        };
        // Retired (Jeremy, Nov 3): it no longer grows; one in an older garden keeps working.
        yield return Wild(passive, NodeKind.NebulaBloom, "Nebula Bloom", Shape.Swirl, 0xF4EEFA, WildUnlock.Start,
            "Power reaching a seedling within 5 links counts double.", "Retired: no longer grows.") with
        {
            MaxFruits = 3, SeedlingBoost = Tuning.NebulaFill, Retired = true,
        };
    }
}
