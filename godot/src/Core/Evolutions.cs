namespace SpaceGarden.Core;

// The evolutions (DESIGN.md, Seedlings and evolutions), in the table's order.
public enum EvolutionKind
{
    BuildYourCore, SecondPath, LongStemmed, SmartParticles, RapidEvolution, MorePower, PulsarPower, TwinStars, BigStem,
    WildBloom,
}

// An evolution's effect at level I, each field a change to the garden (1 or 0: none). Every later level gives the same
// again, never less, adding up: a multiplier's share of the base (Build your Core: core power ×1.5, ×2, ×2.5; see At),
// a cap's +2 each time, Smart Particles' chance 50% then always. Multipliers apply to the basic kind only (a stem, not a
// Tendril or Big Stem; a generator, not a Pulsar): the wild flowers keep their quirks. Caps (CoreLinks, Generators,
// WildFlowers) are raised as each level blooms; the rest hold while bloomed. SmartChance: how often a blob avoids a dead
// end (see Garden.Wanders).
public sealed record EvolutionLevel
{
    public float CoreGen { get; init; } = 1;
    public float CoreTransfer { get; init; } = 1;
    public int CoreLinks { get; init; }
    public float StemTransfer { get; init; } = 1;
    public int StemFruits { get; init; }
    public float SmartChance { get; init; }
    public float LabGenes { get; init; } = 1;
    public float LabUpkeep { get; init; } = 1;
    public int Generators { get; init; }
    public float GeneratorGen { get; init; } = 1;
    public float GeneratorTransfer { get; init; } = 1;
    public int WildFlowers { get; init; }

    // The effect in all at a level (Jeremy, Oct 31): each level adds level I's again.
    public EvolutionLevel At(int level)
    {
        float Times(float by) => 1 + level * (by - 1);
        return new EvolutionLevel
        {
            CoreGen = Times(CoreGen), CoreTransfer = Times(CoreTransfer), CoreLinks = CoreLinks * level,
            StemTransfer = Times(StemTransfer), StemFruits = StemFruits * level,
            SmartChance = Math.Min(1, SmartChance * level),
            LabGenes = Times(LabGenes), LabUpkeep = Times(LabUpkeep), Generators = Generators * level,
            GeneratorGen = Times(GeneratorGen), GeneratorTransfer = Times(GeneratorTransfer),
            WildFlowers = WildFlowers * level,
        };
    }

    // What it does, for the choice menu and the Almanac: "Core power ×1.5", "Core link +1, core transfer ×1.5".
    public string Text
    {
        get
        {
            var parts = new List<string>();
            void Times(string what, float by)
            {
                if (by != 1) parts.Add($"{what} ×{by:0.##}");
            }
            void Plus(string what, int by)
            {
                if (by != 0) parts.Add($"{what} +{by}");
            }
            Times("Core power", CoreGen);
            Plus("Core link", CoreLinks);
            Times("core transfer", CoreTransfer);
            Times("Stem transfer", StemTransfer);
            if (StemFruits != 0) parts.Add($"Stems hold +{StemFruits} leaf");
            if (SmartChance >= 1) parts.Add("Blobs always avoid dead ends");
            else if (SmartChance > 0) parts.Add($"Blobs avoid dead ends {SmartChance * 100:0}% of the time");
            Times("Labs' genes", LabGenes);
            Times("their upkeep", LabUpkeep);
            Plus("Generator cap", Generators);
            Times("Generators' power", GeneratorGen);
            Times("Generator transfer", GeneratorTransfer);
            Plus("Wild flower limit", WildFlowers);
            if (parts.Count == 0) return "";
            parts[0] = char.ToUpperInvariant(parts[0][0]) + parts[0][1..];
            return string.Join(", ", parts);
        }
    }
}

// An evolution: its name, the wild flower its seedling becomes, its theme's role hue (its slot in the choice menu, its
// name in the Almanac), its effect at level I, its repeat limit (1: one-off), and whether it's offered only once
// repeats are (Late: Long Stemmed).
public sealed record EvolutionSpec(EvolutionKind Kind, string Name, NodeKind Becomes, uint Rgb, EvolutionLevel Effect,
    int MaxLevel, bool Late = false)
{
    // An unlock evolution's flower joins the wild flower pool as it blooms (Pulsar, Twin Star, Big Stem).
    public bool Unlocks => NodeSpecs.All[Becomes].Wild?.Unlock == WildUnlock.Evolution;
}

// The evolution table (DESIGN.md, Jeremy's balance update of Oct 31), every number a placeholder; the repeat limits
// are this build's. The unlock evolutions' effect is the small boost to their base type. Watch More Power and Pulsar
// Power in play as likely default picks.
public static class Evolutions
{
    // The role hues: power, structure, genes, and the specialists' white.
    const uint Power = 0xE5E570, Structure = 0x07E000, Genes = 0x9370E0, Specialist = 0xF2F2F2;

    public static readonly IReadOnlyDictionary<EvolutionKind, EvolutionSpec> All = new Dictionary<EvolutionKind, EvolutionSpec>
    {
        [EvolutionKind.BuildYourCore] = new(EvolutionKind.BuildYourCore, "Build your Core", NodeKind.Ember, Power,
            new() { CoreGen = 1.5f }, MaxLevel: 3),
        [EvolutionKind.SecondPath] = new(EvolutionKind.SecondPath, "Second Path", NodeKind.Amplifier, Structure,
            new() { CoreLinks = 1, CoreTransfer = 1.5f }, MaxLevel: 2),
        [EvolutionKind.LongStemmed] = new(EvolutionKind.LongStemmed, "Long Stemmed", NodeKind.Tendril, Structure,
            new() { StemTransfer = 3 }, MaxLevel: 2, Late: true),
        [EvolutionKind.SmartParticles] = new(EvolutionKind.SmartParticles, "Smart Particles", NodeKind.Resistor, Specialist,
            new() { SmartChance = 0.5f }, MaxLevel: 2),
        [EvolutionKind.RapidEvolution] = new(EvolutionKind.RapidEvolution, "Rapid Evolution", NodeKind.WildLab, Genes,
            new() { LabGenes = 2, LabUpkeep = 1.8f }, MaxLevel: 3),
        [EvolutionKind.MorePower] = new(EvolutionKind.MorePower, "More Power", NodeKind.Lichen, Power,
            new() { Generators = 2 }, MaxLevel: 3),
        [EvolutionKind.PulsarPower] = new(EvolutionKind.PulsarPower, "Pulsar Power", NodeKind.Pulsar, Power,
            new() { GeneratorGen = 1.1f }, MaxLevel: 3),
        [EvolutionKind.TwinStars] = new(EvolutionKind.TwinStars, "Twin Stars", NodeKind.TwinStar, Power,
            new() { GeneratorTransfer = 1.5f }, MaxLevel: 2),
        [EvolutionKind.BigStem] = new(EvolutionKind.BigStem, "Big Stem", NodeKind.BigStem, Structure,
            new() { StemFruits = 1 }, MaxLevel: 2),
        [EvolutionKind.WildBloom] = new(EvolutionKind.WildBloom, "Wild Bloom", NodeKind.Moonflower, Specialist,
            new() { WildFlowers = 2 }, MaxLevel: 3),
    };

    // An evolution's name at a level: "Build your Core", then "Build your Core II".
    public static string Name(EvolutionKind kind, int level) =>
        level <= 1 ? All[kind].Name : $"{All[kind].Name} {Roman(level)}";

    static string Roman(int n) => n switch { 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => n.ToString() };

    // Parse a name as the dev flags give it: "SecondPath", "second path" or "second-path".
    public static EvolutionKind? Parse(string name)
    {
        string plain = new(name.Where(char.IsLetter).ToArray());
        return Enum.GetValues<EvolutionKind>().Cast<EvolutionKind?>()
            .FirstOrDefault(k => k.ToString()!.Equals(plain, StringComparison.OrdinalIgnoreCase));
    }
}

// An evolution chosen for a seedling (DESIGN.md, Seedlings and evolutions): which, at what level, its target (the
// seedling's store from then on), when it was chosen, and whether it has bloomed and when (in the garden's age). A
// seedling pruned before it blooms leaves its evolution waiting (and its slot): the next seedling to emerge takes it
// up, starting empty as every new part does (see Garden, Seedlings).
public sealed class Evolution
{
    public EvolutionKind Kind;
    public int Level = 1;
    public double Target;
    public double ChosenAt;
    public bool Bloomed;
    public double BloomedAt;

    public EvolutionSpec Spec => Evolutions.All[Kind];
    public string Name => Evolutions.Name(Kind, Level);
    // What this level adds: level I's effect again, every time.
    public EvolutionLevel Effect => Spec.Effect;
}

// A garden's evolutions' effect on one basic kind: its power made, its upkeep, how fast it sends power on, its blobs,
// and fruit it can hold beyond its spec's (see Garden.Boosts).
public sealed record Boost(float Gen = 1, float Upkeep = 1, float Transfer = 1, float Blobs = 1, int Fruits = 0)
{
    public static readonly Boost None = new();
}

public sealed record EvolutionSave(EvolutionKind Kind, int Level, double Target, bool Bloomed, double BloomedAt,
    double ChosenAt = 0);
