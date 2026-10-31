namespace SpaceGarden.Core;

// The evolutions (DESIGN.md, Seedlings and evolutions), in the table's order.
public enum EvolutionKind
{
    BuildYourCore, SecondPath, LongStemmed, SmartParticles, RapidEvolution, MorePower, PulsarPower, TwinStars, BigStem,
    WildBloom,
}

// One level of an evolution's effect, each field a change to the garden (1 or 0: none). Multipliers apply to the
// basic kind only (a stem, not a Tendril or Big Stem; a generator, not a Pulsar): the wild flowers keep their quirks.
// Caps (CoreLinks, Generators, WildFlowers) are raised once, as it blooms; the rest hold while it's bloomed.
public sealed record EvolutionLevel
{
    public float CoreGen { get; init; } = 1;
    public float CoreTransfer { get; init; } = 1;
    public int CoreLinks { get; init; }
    public float StemTransfer { get; init; } = 1;
    public int StemFruits { get; init; }
    public bool SmartBlobs { get; init; }
    public float LabGenes { get; init; } = 1;
    public float LabUpkeep { get; init; } = 1;
    public int Generators { get; init; }
    public float GeneratorGen { get; init; } = 1;
    public float GeneratorTransfer { get; init; } = 1;
    public int WildFlowers { get; init; }

    // What it does, for the choice menu: "Core power ×1.5", "Core link +1, core transfer ×1.5".
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
            if (SmartBlobs) parts.Add("Blobs never wander into dead ends");
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
// name in the Almanac), and its effect at each level it can be taken to (Levels.Length is its repeat limit: 1, one-off).
public sealed record EvolutionSpec(EvolutionKind Kind, string Name, NodeKind Becomes, uint Rgb,
    EvolutionLevel[] Levels)
{
    public int MaxLevel => Levels.Length;
    // An unlock evolution's flower joins the wild flower pool as it blooms (Pulsar, Twin Star, Big Stem).
    public bool Unlocks => NodeSpecs.All[Becomes].Wild?.Unlock == WildUnlock.Evolution;
}

// The evolution table (DESIGN.md), every number a placeholder. Each level's values stand alone and multiply with the
// levels before (Build your Core II: the core's power ×1.5 then ×1.25). The unlock evolutions' first level is the small
// boost to their base type; repeating them adds a smaller one.
public static class Evolutions
{
    // The role hues: power, structure, genes, and the specialists' white.
    const uint Power = 0xE5E570, Structure = 0x07E000, Genes = 0x9370E0, Specialist = 0xF2F2F2;

    public static readonly IReadOnlyDictionary<EvolutionKind, EvolutionSpec> All = new Dictionary<EvolutionKind, EvolutionSpec>
    {
        [EvolutionKind.BuildYourCore] = new(EvolutionKind.BuildYourCore, "Build your Core", NodeKind.Ember,
            Power, [new() { CoreGen = 1.5f }, new() { CoreGen = 1.25f }, new() { CoreGen = 1.2f }]),
        [EvolutionKind.SecondPath] = new(EvolutionKind.SecondPath, "Second Path", NodeKind.Tendril,
            Structure, [new() { CoreLinks = 1, CoreTransfer = 1.5f }, new() { CoreLinks = 1, CoreTransfer = 1.25f }]),
        [EvolutionKind.LongStemmed] = new(EvolutionKind.LongStemmed, "Long Stemmed", NodeKind.Amplifier,
            Structure, [new() { StemTransfer = 3 }, new() { StemTransfer = 1.5f }]),
        [EvolutionKind.SmartParticles] = new(EvolutionKind.SmartParticles, "Smart Particles", NodeKind.Resistor,
            Specialist, [new() { SmartBlobs = true }]),
        [EvolutionKind.RapidEvolution] = new(EvolutionKind.RapidEvolution, "Rapid Evolution", NodeKind.WildLab,
            Genes,
            [new() { LabGenes = 2, LabUpkeep = 1.25f }, new() { LabGenes = 1.5f, LabUpkeep = 1.25f }]),
        [EvolutionKind.MorePower] = new(EvolutionKind.MorePower, "More Power", NodeKind.Lichen, Power,
            [new() { Generators = 2 }, new() { Generators = 2 }, new() { Generators = 2 }]),
        [EvolutionKind.PulsarPower] = new(EvolutionKind.PulsarPower, "Pulsar Power", NodeKind.Pulsar,
            Power, [new() { GeneratorGen = 1.2f }, new() { GeneratorGen = 1.1f }]),
        [EvolutionKind.TwinStars] = new(EvolutionKind.TwinStars, "Twin Stars", NodeKind.TwinStar, Power,
            [new() { GeneratorTransfer = 1.5f }, new() { GeneratorTransfer = 1.25f }]),
        [EvolutionKind.BigStem] = new(EvolutionKind.BigStem, "Big Stem", NodeKind.BigStem, Structure,
            [new() { StemFruits = 1 }, new() { StemFruits = 1 }]),
        [EvolutionKind.WildBloom] = new(EvolutionKind.WildBloom, "Wild Bloom", NodeKind.Moonflower,
            Specialist, [new() { WildFlowers = 2 }, new() { WildFlowers = 2 }, new() { WildFlowers = 2 }]),
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
    public EvolutionLevel Effect => Spec.Levels[Level - 1];
}

// A garden's evolutions' effect on one basic kind: its power made, its upkeep, how fast it sends power on, its blobs,
// and fruit it can hold beyond its spec's (see Garden.Boosts).
public sealed record Boost(float Gen = 1, float Upkeep = 1, float Transfer = 1, float Blobs = 1, int Fruits = 0)
{
    public static readonly Boost None = new();
}

public sealed record EvolutionSave(EvolutionKind Kind, int Level, double Target, bool Bloomed, double BloomedAt,
    double ChosenAt = 0);
