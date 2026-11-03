namespace SpaceGarden.Core;

// The evolutions (DESIGN.md, Seedlings and evolutions), in the table's order.
public enum EvolutionKind
{
    BuildYourCore, SecondPath, LongStemmed, SmartParticles, RapidEvolution, MorePower, PulsarPower, TwinStars, BigStem,
    WildBloom, ResonantFrequency,
}

// An evolution's effect at level I, each field a change to the garden (1 or 0: none). Every later level gives the same
// again, never less, adding up: a multiplier's share of the base (Build your Core: core power x2, x3, x4; see At), a
// cap's +2 each time, Smart Particles' chance 50% then always. Caps (CoreLinks, Generators, WildFlowers) are raised as
// each level blooms; the rest hold while bloomed. Jeremy's table of Nov 3:
// - Core: power made, transfer and store (Build your Core), links (Second Path).
// - Stems (the stem, the Big Stem and the Tendril): transfer and store (Long Stemmed). StemFruits: Big Stem's, retired.
// - SmartChance: how often a blob avoids a dead end (see Garden.Wanders).
// - Labs (the lab, the Wild Lab and the Lichen): the genes each blob carries, and their upkeep (Rapid Evolution).
// - Generators: the cap; power made by every generator kind (Pulsar Power); transfer by the generator, the Twin Star
//   and the Ember (More Power; a Pulsar bursts instead). GeneratorTransfer for the Pulsar too: Twin Stars', retired.
// - Wild flowers: the limit, and WildEffect on the Pulsar's, Twin Star's and Ember's power and the Lumber Grove's,
//   Seed Pod's, Wild Lab's, Lichen's and Buffer's blobs (Wild Bloom).
// - BuffBonus: added to every buff's bonus (Resonant Frequency: 200%, then 250%, then 300% of the output).
public sealed record EvolutionLevel
{
    public float CoreGen { get; init; } = 1;
    public float CoreTransfer { get; init; } = 1;
    public float CoreStore { get; init; } = 1;
    public int CoreLinks { get; init; }
    public float StemTransfer { get; init; } = 1;
    public float StemStore { get; init; } = 1;
    public int StemFruits { get; init; }
    public float SmartChance { get; init; }
    public float LabBlobSize { get; init; } = 1;
    public float LabUpkeep { get; init; } = 1;
    public int Generators { get; init; }
    public float GeneratorGen { get; init; } = 1;
    public float GeneratorTransfer { get; init; } = 1;
    public float AllGeneratorTransfer { get; init; } = 1;
    public int WildFlowers { get; init; }
    public float WildEffect { get; init; } = 1;
    public float BuffBonus { get; init; }

    // The effect in all at a level (Jeremy, Oct 31): each level adds level I's again.
    public EvolutionLevel At(int level)
    {
        float Times(float by) => 1 + level * (by - 1);
        return new EvolutionLevel
        {
            CoreGen = Times(CoreGen), CoreTransfer = Times(CoreTransfer), CoreStore = Times(CoreStore),
            CoreLinks = CoreLinks * level,
            StemTransfer = Times(StemTransfer), StemStore = Times(StemStore), StemFruits = StemFruits * level,
            SmartChance = Math.Min(1, SmartChance * level),
            LabBlobSize = Times(LabBlobSize), LabUpkeep = Times(LabUpkeep), Generators = Generators * level,
            GeneratorGen = Times(GeneratorGen), GeneratorTransfer = Times(GeneratorTransfer),
            AllGeneratorTransfer = Times(AllGeneratorTransfer),
            WildFlowers = WildFlowers * level, WildEffect = Times(WildEffect), BuffBonus = BuffBonus * level,
        };
    }

    // What it does, for the choice menu and the Almanac: "Core power x2, core transfer x2, core store x1.5".
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
            Times("core store", CoreStore);
            Times("Stem, Big Stem and Tendril transfer", StemTransfer);
            Times("their store", StemStore);
            if (StemFruits != 0) parts.Add($"Stems hold +{StemFruits} leaf");
            if (SmartChance >= 1) parts.Add("Blobs always avoid dead ends");
            else if (SmartChance > 0) parts.Add($"Blobs avoid dead ends {SmartChance * 100:0}% of the time");
            Times("Lab, Wild Lab and Lichen gene blobs' size", LabBlobSize);
            Times("their upkeep", LabUpkeep);
            Plus("Generator cap", Generators);
            Times("Every generator kind's power", GeneratorGen);
            Times("Generator, Twin Star and Ember transfer", GeneratorTransfer);
            Times("Generator transfer", AllGeneratorTransfer);
            Plus("Wild flower limit", WildFlowers);
            if (WildEffect != 1) parts.Add($"Pulsar, Twin Star, Ember, Lumber Grove, Seed Pod, Wild Lab, Lichen and Buffer +{(WildEffect - 1) * 100:0}%");
            if (BuffBonus > 0) parts.Add($"Buffs {(1 + Tuning.BuffBonus + BuffBonus) * 100:0}% of the output");
            if (parts.Count == 0) return "";
            parts[0] = char.ToUpperInvariant(parts[0][0]) + parts[0][1..];
            return string.Join(", ", parts);
        }
    }
}

// An evolution: its name, the wild flower its seedling becomes, its theme's role hue (its slot in the choice menu, its
// name in the Almanac), its effect at level I, its repeat limit (1: one-off), whether it's offered only once repeats
// are (Late: Long Stemmed), and whether it's retired (never offered again; one already taken keeps its effect).
public sealed record EvolutionSpec(EvolutionKind Kind, string Name, NodeKind Becomes, uint Rgb, EvolutionLevel Effect,
    int MaxLevel, bool Late = false, bool Retired = false)
{
    // An unlock evolution's flower joins the wild flower pool as it blooms (none since Nov 3: every kind is Start).
    public bool Unlocks => NodeSpecs.All[Becomes].Wild?.Unlock == WildUnlock.Evolution;
}

// The evolution table (DESIGN.md), Jeremy's of Nov 3: each level the same again (x2, x3, x4). Twin Stars and Big Stem
// are retired. Watch More Power and Pulsar Power in play as likely default picks.
public static class Evolutions
{
    // The role hues: power, structure, genes, and the specialists' white.
    const uint Power = 0xE5E570, Structure = 0x07E000, Genes = 0x9370E0, Specialist = 0xF2F2F2;

    public static readonly IReadOnlyDictionary<EvolutionKind, EvolutionSpec> All = new Dictionary<EvolutionKind, EvolutionSpec>
    {
        [EvolutionKind.BuildYourCore] = new(EvolutionKind.BuildYourCore, "Build your Core", NodeKind.Ember, Power,
            new() { CoreGen = 2, CoreTransfer = 2, CoreStore = 1.5f }, MaxLevel: 3),
        [EvolutionKind.SecondPath] = new(EvolutionKind.SecondPath, "Second Path", NodeKind.Amplifier, Structure,
            new() { CoreLinks = 1, CoreTransfer = 2 }, MaxLevel: 3),
        [EvolutionKind.LongStemmed] = new(EvolutionKind.LongStemmed, "Long Stemmed", NodeKind.Tendril, Structure,
            new() { StemTransfer = 2, StemStore = 2 }, MaxLevel: 2, Late: true),
        [EvolutionKind.SmartParticles] = new(EvolutionKind.SmartParticles, "Smart Particles", NodeKind.Resistor, Specialist,
            new() { SmartChance = 0.5f }, MaxLevel: 2),
        [EvolutionKind.RapidEvolution] = new(EvolutionKind.RapidEvolution, "Rapid Evolution", NodeKind.WildLab, Genes,
            new() { LabBlobSize = 2, LabUpkeep = 2 }, MaxLevel: 3),
        [EvolutionKind.MorePower] = new(EvolutionKind.MorePower, "More Power", NodeKind.Lichen, Power,
            new() { Generators = 2, GeneratorTransfer = 2 }, MaxLevel: 3),
        [EvolutionKind.PulsarPower] = new(EvolutionKind.PulsarPower, "Pulsar Power", NodeKind.Pulsar, Power,
            new() { GeneratorGen = 2 }, MaxLevel: 3),
        [EvolutionKind.TwinStars] = new(EvolutionKind.TwinStars, "Twin Stars", NodeKind.TwinStar, Power,
            new() { AllGeneratorTransfer = 1.5f }, MaxLevel: 2, Retired: true),
        [EvolutionKind.BigStem] = new(EvolutionKind.BigStem, "Big Stem", NodeKind.BigStem, Structure,
            new() { StemFruits = 1 }, MaxLevel: 2, Retired: true),
        [EvolutionKind.WildBloom] = new(EvolutionKind.WildBloom, "Wild Bloom", NodeKind.Moonflower, Specialist,
            new() { WildFlowers = 2, WildEffect = 1.25f }, MaxLevel: 3),
        [EvolutionKind.ResonantFrequency] = new(EvolutionKind.ResonantFrequency, "Resonant Frequency", NodeKind.Buffer, Specialist,
            new() { BuffBonus = 0.5f }, MaxLevel: 3),
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

// A garden's evolutions' and Hub's effect on one kind: its power made, its upkeep, how fast it sends power on, its
// blobs, fruit it can hold beyond its spec's (see Garden.UpdateBoosts). The Hub's paths add: links beyond its spec's,
// its store and life multiplied, the share of its output that holds whatever its fill (OutputFloor), how much further
// and faster its gene blobs go (Reach), a share of output each leaf adds (LeafBonus), and how much faster its buds grow
// on their own (BudGrow) (see HubMilestone).
public sealed record Boost(float Gen = 1, float Upkeep = 1, float Transfer = 1, float Blobs = 1, int Fruits = 0)
{
    public int Links { get; init; }
    public float Store { get; init; } = 1;
    public float Life { get; init; } = 1;
    public float OutputFloor { get; init; }
    public float Reach { get; init; } = 1;
    public float LeafBonus { get; init; }
    public float BudGrow { get; init; } = 1;
    // What each blob it sends carries, times its spec's BlobValue (Rapid Evolution: a lab's gene blobs' size).
    public float Value { get; init; } = 1;

    public static readonly Boost None = new();
}

public sealed record EvolutionSave(EvolutionKind Kind, int Level, double Target, bool Bloomed, double BloomedAt,
    double ChosenAt = 0);
