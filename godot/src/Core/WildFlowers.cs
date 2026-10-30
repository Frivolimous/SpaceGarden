namespace SpaceGarden.Core;

// The wild flowers (DESIGN.md, Wild flowers): a new bud, planted or passive, may grow into one of these instead of
// its basic kind. Each is its base kind's spec with its own name, look and quirk; the passive-only kinds have no base
// and are white. Description is the table's Effect, the Codex's line; WildSpec.Quirk is the card's. Their numbers are
// first passes, in Tuning (Wild flowers).
static class WildFlowers
{
    public static IEnumerable<NodeSpec> All(IReadOnlyDictionary<NodeKind, NodeSpec> basic)
    {
        var stem = basic[NodeKind.Stem];
        var generator = basic[NodeKind.Generator];
        var grove = basic[NodeKind.Grove];
        var lab = basic[NodeKind.Lab];

        // The passive-only kinds are small flowers of their own, at the end of a branch.
        var passive = new NodeSpec(NodeKind.Buffer)
        {
            Name = "", Description = "",
            Radius = 13, Mass = 2, Force = 1.5f, MaxLinks = 1, Shape = Shape.Circle, Rgb = 0xF0F0F0,
            PowerStore = NodeBase.PowerStore, PowerPerSecond = NodeBase.StemDrain,
            PowerTransferPerSecond = NodeBase.PowerTransferSub,
        };

        NodeSpec Wild(NodeSpec basis, NodeKind kind, string name, Shape shape, uint rgb, WildUnlock unlock, string effect,
            string quirk) => basis with
        {
            Kind = kind, Name = name, Description = effect, Shape = shape, Rgb = rgb,
            Wild = new WildSpec(ReferenceEquals(basis, passive) ? null : basis.Kind, unlock, quirk),
        };

        yield return Wild(stem, NodeKind.Amplifier, "Amplifier", Shape.Bullseye, 0x40E858, WildUnlock.Start,
            "Doubles blobs passing through, once each.", "2 links, can't fork.") with
        {
            Radius = 11, MaxLinks = 2, Fruit = null, MaxFruits = 0, DoublesBlobs = true,
        };
        yield return Wild(stem, NodeKind.Resistor, "Resistor", Shape.Gear, 0x30B838, WildUnlock.Start,
            "Blocks all blobs.", "Low transfer: slows power beyond it.") with
        {
            Radius = 11, MaxLinks = 2, Fruit = null, MaxFruits = 0, BlocksBlobs = true,
            PowerTransferPerSecond = Tuning.ResistorTransfer,
        };
        yield return Wild(stem, NodeKind.Tendril, "Tendril", Shape.Swirl, 0x78E010, WildUnlock.Start,
            "Very long and springy; stores a large power reserve.", "Slow to fill.") with
        {
            Radius = 11, LinkScale = Tuning.TendrilLink, Spring = Tuning.TendrilSpring,
            PowerStore = Tuning.TendrilStore, PowerWeight = Tuning.TendrilWeight,
        };
        yield return Wild(stem, NodeKind.BigStem, "Big Stem", Shape.Quatrefoil, 0x10C838, WildUnlock.Evolution,
            "4 links.", "Slower power transfer.") with
        {
            Radius = 13, Mass = 1.5f, MaxLinks = 4, PowerTransferPerSecond = Tuning.BigStemTransfer,
        };
        yield return Wild(generator, NodeKind.Pulsar, "Pulsar", Shape.Star5, 0xF2D84A, WildUnlock.Evolution,
            "More power, released in bursts.", "Uneven supply.") with
        {
            PowerPerSecond = Tuning.PulsarGen, PulseSeconds = Tuning.PulsarSeconds, PowerStore = Tuning.PulsarStore,
            PowerTransferPerSecond = Tuning.PulsarTransfer,
        };
        yield return Wild(generator, NodeKind.TwinStar, "Twin Star", Shape.Star10, 0xE8EE8C, WildUnlock.Evolution,
            "A generator that continues the branch.", "Capped.") with
        {
            MaxLinks = 2,
        };
        yield return Wild(generator, NodeKind.Ember, "Ember", Shape.Flame, 0xF0BE50, WildUnlock.Start,
            "No upkeep, steady output.", "Dims over days, and burns out when dim enough.") with
        {
            SteadyOutput = true, LifeSeconds = Tuning.EmberLifeSeconds, Dims = true,
        };
        yield return Wild(grove, NodeKind.LumberGrove, "Lumber Grove", Shape.Star8, 0xC8661E, WildUnlock.Start,
            "Many more growth blobs.", "Higher upkeep.") with
        {
            BlobsPerHour = Tuning.LumberBlobs, PowerPerSecond = -Tuning.LumberUpkeep,
        };
        yield return Wild(grove, NodeKind.SeedPod, "Seed Pod", Shape.Pod, 0xEA8A34, WildUnlock.Start,
            "Now and then, a big burst of growth blobs.", "Silent in between.") with
        {
            BlobsPerHour = Tuning.SeedPodBlobs, BlobBurst = Tuning.SeedPodBurst,
        };
        yield return Wild(lab, NodeKind.WildLab, "Wild Lab", Shape.Star6, 0xA478F0, WildUnlock.Start,
            "Fewer genes, but holds more fruit; with full fruit it beats a basic lab.", "Slow to pay off.") with
        {
            BlobsPerHour = Tuning.WildLabBlobs, MaxFruits = Tuning.WildLabFruits, FruitBonusScale = Tuning.WildLabFruitScale,
        };
        yield return Wild(lab, NodeKind.Lichen, "Lichen", Shape.Lobes6, 0x8C84D4, WildUnlock.Start,
            "Low upkeep; holds 4 leaves, which bring its upkeep to 0.", "Slow genes.") with
        {
            PowerPerSecond = -Tuning.LichenUpkeep, BlobsPerHour = Tuning.LichenBlobs,
            Fruit = NodeKind.Leaf, MaxFruits = Tuning.LichenLeaves,
        };
        yield return Wild(passive, NodeKind.Buffer, "Buffer", Shape.Sun, 0xF2F2F2, WildUnlock.Start,
            "Buff blobs raise nearby production, up to 5 links away (8 if amplified).", "Local only.") with
        {
            Sends = BlobKind.Buff, BlobsPerHour = Tuning.BufferBlobs,
        };
        yield return Wild(passive, NodeKind.CometBloom, "Comet Bloom", Shape.Sparkle, 0xFFFFFF, WildUnlock.Start,
            "Bright; bursts all kinds of blob as it appears.", "Withers after about a day.") with
        {
            Sends = BlobKind.All, EmergeBurst = Tuning.CometBurst, LifeSeconds = Tuning.CometLifeSeconds,
        };
        yield return Wild(passive, NodeKind.Moonflower, "Moonflower", Shape.Rose, 0xE6ECFA, WildUnlock.Start,
            "Buds nearby are a little likelier to grow wild.", "Takes a slot.") with
        {
            WildBoost = Tuning.MoonflowerBoost,
        };
        yield return Wild(passive, NodeKind.NebulaBloom, "Nebula Bloom", Shape.Galaxy, 0xF4EEFA, WildUnlock.Start,
            "A seedling within a few links fills twice as fast.", "Useless without a seedling.");
    }
}
