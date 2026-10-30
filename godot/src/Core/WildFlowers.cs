namespace SpaceGarden.Core;

// The wild flowers (DESIGN.md, Wild flowers): a new bud, planted or passive, may grow into one of these instead of
// its basic kind. Each is its base kind's spec with its own name, look, fruit and quirk; the passive-only kinds have no
// base and are white (so are the Amplifier and the Resistor). A stem or generator kind (but the Amplifier and the Resistor) takes its base kind's slot as
// well as a wild flower slot. Description is the table's Effect, the Codex's line; WildSpec.Quirk is the card's. Their
// numbers are first passes, in Tuning (Wild flowers). Their fruit: every fruit adds a tenth to its flower's main
// effect (wood, from the Lumber Grove, too: crawlers will use it later), but leaves (a quarter off its upkeep) and
// glow, a white fruit that sends buff blobs as a small Buffer.
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
            PowerStore = NodeBase.PowerStore, PowerPerSecond = NodeBase.StemDrain,
            PowerTransferPerSecond = NodeBase.PowerTransferSub,
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

        // The stem kinds. The Amplifier and the Resistor take only a wild flower slot, and are white, the specialists'
        // hue (Jeremy, Oct 30).
        var amplifier = Wild(stem, NodeKind.Amplifier, "Amplifier", Shape.Plus, 0xF2F2F2, WildUnlock.Start,
            "Gene and growth blobs passing through grow 50% larger.", "2 links: it can't fork.");
        yield return amplifier with
        {
            Radius = 11, MaxLinks = 2, Fruit = NodeKind.Glow, MaxFruits = 1, DoublesBlobs = true,
            Wild = amplifier.Wild! with { BaseSlot = false },
        };
        var resistor = Wild(stem, NodeKind.Resistor, "Resistor", Shape.Minus, 0xF2F2F2, WildUnlock.Start,
            "Blobs bounce off it from both sides.", "Low transfer: slows power beyond it.");
        yield return resistor with
        {
            Radius = 11, MaxLinks = 2, Fruit = NodeKind.Glow, MaxFruits = 1, BlocksBlobs = true,
            PowerTransferPerSecond = Tuning.ResistorTransfer, Wild = resistor.Wild! with { BaseSlot = false },
        };
        yield return Wild(stem, NodeKind.Tendril, "Tendril", Shape.Trefoil, 0x78E010, WildUnlock.Start,
            "A long, springy link and a large power store.", "Takes a stem slot too.") with
        {
            Radius = 11, LinkScale = Tuning.TendrilLink, Spring = Tuning.TendrilSpring, MaxFruits = 3,
            PowerStore = Tuning.TendrilStore, PowerTransferPerSecond = Tuning.TendrilTransfer,
        };
        yield return Wild(stem, NodeKind.BigStem, "Big Stem", Shape.Quatrefoil, 0x10C838, WildUnlock.Evolution,
            "4 links.", "Slow transfer; takes a stem slot too.") with
        {
            Radius = 13, Mass = 1.5f, MaxLinks = 4, PowerTransferPerSecond = Tuning.BigStemTransfer,
        };

        // The generator kinds, each taking a generator slot too.
        yield return Wild(generator, NodeKind.Pulsar, "Pulsar", Shape.Star5, 0xF2D84A, WildUnlock.Evolution,
            "1.5 times a generator's power, released in bursts.", "Uneven supply; takes a generator slot too.") with
        {
            PowerPerSecond = Tuning.PulsarGen, PulseSeconds = Tuning.PulsarSeconds, PowerStore = Tuning.PulsarStore,
            PowerTransferPerSecond = Tuning.PulsarTransfer,
        };
        yield return Wild(generator, NodeKind.TwinStar, "Twin Star", Shape.Star10, 0xE8EE8C, WildUnlock.Evolution,
            "A generator that continues the branch: 2 links.", "80% of a generator's power; takes a generator slot too.") with
        {
            MaxLinks = 2, PowerPerSecond = Tuning.TwinStarGen, Fruit = NodeKind.Berry,
        };
        yield return Wild(generator, NodeKind.Ember, "Ember", Shape.Flame, 0xF0BE50, WildUnlock.Start,
            "Starts at twice a generator's power, level with one at a day and a half.",
            "Keeps fading, and burns out at 3 days unless pruned first; takes a generator slot too.") with
        {
            PowerPerSecond = Tuning.EmberGen, LifeSeconds = Tuning.EmberLifeSeconds, Dims = true, Fruit = null, MaxFruits = 0,
        };

        // The grove kinds.
        yield return Wild(grove, NodeKind.LumberGrove, "Lumber Grove", Shape.Star8, 0xC8661E, WildUnlock.Start,
            "Twice a grove's growth blobs.", "Higher upkeep.") with
        {
            BlobsPerHour = Tuning.LumberBlobs, PowerPerSecond = -Tuning.LumberUpkeep, Fruit = NodeKind.Wood, MaxFruits = 5,
        };
        yield return Wild(grove, NodeKind.SeedPod, "Seed Pod", Shape.Oval, 0xEA8A34, WildUnlock.Start,
            "1.5 times a grove's growth blobs, in big bursts.", "Silent in between.") with
        {
            BlobsPerHour = Tuning.SeedPodBlobs, BlobBurst = Tuning.SeedPodBurst,
        };

        // The lab kinds.
        yield return Wild(lab, NodeKind.WildLab, "Wild Lab", Shape.Star6, 0xA478F0, WildUnlock.Start,
            "Its research counts double: twice a bare lab with all five.", "Slow to pay off.") with
        {
            MaxFruits = 5, FruitBonusScale = Tuning.WildLabFruitScale,
        };
        yield return Wild(lab, NodeKind.Lichen, "Lichen", Shape.Lobes6, 0x8C84D4, WildUnlock.Start,
            "Low upkeep; its 4 leaves bring it to 0.", "Fewer genes.") with
        {
            Radius = stem.Radius, PowerPerSecond = -Tuning.LichenUpkeep, BlobsPerHour = Tuning.LichenBlobs,
            Fruit = NodeKind.Leaf, MaxFruits = 4,
        };

        // The passive-only kinds.
        yield return Wild(passive, NodeKind.Buffer, "Buffer", Shape.Burst8, 0xF2F2F2, WildUnlock.Start,
            "Buff blobs give the parts they pass 50% more for a minute.", "Local only: 5 links (8 past an Amplifier).") with
        {
            Sends = BlobKind.Buff, BlobsPerHour = Tuning.BufferBlobs, MaxFruits = 2,
        };
        yield return Wild(passive, NodeKind.CometBloom, "Comet Bloom", Shape.Gear, 0xFFFFFF, WildUnlock.Start,
            "Bright: a burst of genes and growth worth 2 hours of a lab and a grove.",
            "Withers after a day.") with
        {
            Sends = BlobKind.All, MaxFruits = 2, LifeSeconds = Tuning.CometLifeSeconds,
            EmergeBurst = (int)(Tuning.CometHours * (lab.BlobsPerHour + grove.BlobsPerHour)),
        };
        yield return Wild(passive, NodeKind.Moonflower, "Moonflower", Shape.Rose, 0xE6ECFA, WildUnlock.Start,
            "Buds nearby grow wild far more often; wild flowers near it make 10% more.", "Takes a slot.") with
        {
            WildBoost = Tuning.MoonflowerBoost, MaxFruits = 3,
        };
        yield return Wild(passive, NodeKind.NebulaBloom, "Nebula Bloom", Shape.Swirl, 0xF4EEFA, WildUnlock.Start,
            "Power reaching a seedling within 5 links counts double.", "Useless without a seedling.") with
        {
            MaxFruits = 3,
        };
    }
}
