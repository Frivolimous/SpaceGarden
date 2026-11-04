namespace SpaceGarden.Core.Tests;

// A smart, attentive player, for pacing runs (DESIGN.md: "simulations assume a smart player who is actively tending"):
// the plant is stupid, so this is the brain. Each check-in it
//   - spends its genes on the cheapest next upgrade it can afford, again and again, saving for a generator when short
//     of power or for the next core upgrade when it costs at most twice the next stem (Research);
//   - plants a seedling in every free seedling slot and picks each seedling's evolution at random (Seedlings; the best
//     and bad players by a ranking: see Style);
//   - prunes wild flowers that don't pay for their link (PruneWild; the best player keeps a Buffer, the bad one all);
//   - prunes passive groves past the ones it wants (the farthest first);
//   - prunes drains: while the garden would cost more than it makes at full power, the lab furthest from the core
//     goes (passive ones are usually far out), then any lab too far out for its genes to find the core;
//   - makes room: when the stem or generator cap has room but no free link, prunes the farthest flowers (MakeRoom);
//   - fills the stem cap with stems, nearest the core first;
//   - adds labs near the core (within MaxLabDepth) while the power to spare covers one, keeping a small reserve and
//     a free link for the plant to grow on;
//   - fills the generator cap with generators where the plant is hungriest (lowest on power);
//   - keeps a grove for every GroveEvery stems, planted mid-plant;
//   - grows the Hub once it's bought, on the free link nearest the core; once it's out, prunes about half the garden
//     and grows it back half on the Hub's side, then keeps the genes about half to the Hub and half to the core
//     (Jeremy, Nov 4): new stems and labs go on the side that's short, and when one side's genes are well ahead, one of
//     its labs moves to the other; spends the Hub's points at every check-in on whichever level adds the most per point
//     (Hub).
public static class SmartPlayer
{
    // Three players for pacing runs (Jeremy, Nov 4: fixed builds, so runs compare), from the balance tests:
    //   - Medium: the player above (evolutions at random, Buffers pruned, generators where the garden is hungriest).
    //   - Best: evolutions by the balance test's ranking (Second Path, Rapid Evolution, Smart Motes, then, with Rapid
    //     Evolution's drain, Best Generation...), Buffers kept, generators one to a stem (a branch's own).
    //   - Bad: evolutions in the reverse order (More Power first), every wildflower kept where it grew (a Resistor too),
    //     labs on the core's links and at any depth, generators side by side, the Hub's points on any path.
    public enum PlayStyle { Bad, Medium, Best }
    public static PlayStyle Style = PlayStyle.Medium;

    static readonly EvolutionKind[] BestOrder =
    [
        EvolutionKind.SecondPath, EvolutionKind.RapidEvolution, EvolutionKind.SmartParticles, EvolutionKind.WildBloom,
        EvolutionKind.PulsarPower, EvolutionKind.ResonantFrequency, EvolutionKind.BuildYourCore, EvolutionKind.LongStemmed,
        EvolutionKind.MorePower,
    ];

    // Where an evolution stands in the player's order (lower first): the best player's, with Best Generation next after
    // Rapid Evolution (its drain wants the power); the bad player's the reverse.
    static int Rank(Garden garden, EvolutionKind kind)
    {
        int at = Array.IndexOf(BestOrder, kind);
        if (at < 0) at = BestOrder.Length;
        if (Style == PlayStyle.Best && kind == EvolutionKind.PulsarPower && garden.Taken(EvolutionKind.RapidEvolution) > 0) at = -1;
        return Style == PlayStyle.Bad ? -at : at;
    }

    const int GroveEvery = 15;
    const float Reserve = 1; // power a second

    // Check-ins every minute for the first hour (a new player watches the garden start), then every two hours while
    // awake: planted at 9:00, asleep from 1:00 to 9:00.
    public const double CheckHours = 2;
    public static bool Awake(double hours) => hours % 24 < 16;
    public static bool CheckIn(double seconds) => (long)Math.Round(seconds) is var s
        && (s < 3600 ? s % 60 == 0 : s % (long)(CheckHours * 3600) == 0 && Awake(s / 3600.0));

    public static void Tend(Garden garden)
    {
        Research(garden);
        PruneDrains(garden);
        MakeRoom(garden);
        // The opening: the first grove at once, and the first lab as soon as a part has a link to spare beside it.
        if (Count(garden, NodeKind.Grove) == 0 && !Forming(garden, NodeKind.Grove)) Bud(garden, NodeKind.Grove, nearFirst: true);
        if (Count(garden, NodeKind.Lab) == 0 && !Forming(garden, NodeKind.Lab) && FreeLinks(garden) > 1)
            Bud(garden, NodeKind.Lab, nearFirst: true, maxDepth: MaxLabDepth);
        Seedlings(garden);
        Hub(garden);
        while (BudSided(garden, NodeKind.Stem)) { }
        BalanceGenes(garden);
        while (Spare(garden) >= -NodeSpecs.All[NodeKind.Lab].PowerPerSecond + Reserve && FreeLinks(garden) > 1
            && BudSided(garden, NodeKind.Lab)) { }
        while (Bud(garden, NodeKind.Generator, nearFirst: false, hungriest: true)) { }
        // (Grove buds forming count: they come out within minutes, and the player doesn't start one on every free link.)
        while (garden.Nodes.Count(n => IsGrove(n) || n.FormingBud && Garden.IsBudOf(n.Forming!.Value, NodeKind.Grove))
                < GrovesWanted(garden) && Bud(garden, NodeKind.Grove, nearFirst: false, middle: true)) { }
        // The player doesn't undo, and the 30 s undo window passes (counted in physics ticks, which a pacing run
        // doesn't run; left open, it would hold off passive buds for good).
        garden.ForgetPrune();
    }

    static int GrovesWanted(Garden garden) => 1 + Count(garden, NodeKind.Stem) / GroveEvery;

    // ==== THE HUB (Nov 4) ==== \\

    // What the player has done with the Hub, a garden's own (the runs go side by side): its recent share of the genes,
    // the rebuild once it was out, the labs it moved to keep the split, and the levels it bought, in order, with when.
    public sealed class HubCare
    {
        internal double Genes, HubGenes, ToHub, ToCore;
        public double? Share { get; internal set; }
        public bool Rebuilt { get; internal set; }
        public double RebuiltAt { get; internal set; } = double.PositiveInfinity; // hours
        public int RebuildPruned { get; internal set; } // main parts pruned to rebuild
        public int LabsMoved { get; internal set; }
        public List<(NodeKind Kind, int Level, double Hours)> Bought { get; } = new();
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Garden, HubCare> cares = new();
    public static HubCare CareOf(Garden garden) =>
        cares.GetValue(garden, g => new HubCare { Genes = g.Genes, HubGenes = g.HubGenes });

    const double SplitSmoothing = 0.3, HubShare = 0.5, ShareSlack = 0.15;
    static double HoursOf(Garden garden) => garden.Tracking.Age / 3600;

    // Grown as soon as it's bought, on the free link nearest the core (off the core itself, which grows only stems).
    // Once it's out, the player prunes about half the garden, from the core's side, and grows it back half on the Hub's
    // side (stems and labs go to the side that's short: see BudSided), so about half the genes go to the Hub (Jeremy,
    // Nov 4). Its points go on whichever next level adds the most per point (HubValue), again and again.
    static void Hub(Garden garden)
    {
        if (garden.CanPlantHub && !Forming(garden, NodeKind.Hub)) Bud(garden, NodeKind.Hub, nearFirst: true);
        if (garden.Hub is not { } hub) return;
        var care = CareOf(garden);
        if (!care.Rebuilt) Rebuild(garden, hub, care);
        while (Hubs.Paths.Where(p => garden.CanBuyHubLevel(p.Kind))
            .OrderByDescending(p => Style == PlayStyle.Bad ? new Random(garden.HubPointsEarned * 31 + (int)p.Kind).NextDouble()
                : HubValue(garden, p) / p.Cost(garden.HubLevel(p.Kind) + 1))
            .ThenBy(p => ToMilestone(garden, p)).FirstOrDefault() is { } path)
        {
            garden.BuyHubLevel(path.Kind);
            care.Bought.Add((path.Kind, garden.HubLevel(path.Kind), HoursOf(garden)));
        }
    }

    // Prunes the core's side, the farthest parts first (each with what grew beyond it), until about half the garden's
    // main parts are gone; never the Hub or the way to it.
    static void Rebuild(Garden garden, PlantNode hub, HubCare care)
    {
        var way = new HashSet<PlantNode>();
        for (var at = hub; at != null; at = at.Parent) way.Add(at);
        int main = garden.Nodes.Count(n => !n.Spec.IsFruit && n != garden.Core && !n.IsHub), pruned = 0;
        while (pruned < main / 2 && garden.Nodes.Where(n => !n.Spec.IsFruit && n.Parent != null && !way.Contains(n)
                && !HubSide(n)).MaxBy(n => n.Depth) is { } far)
            pruned += garden.Prune(far).Count(n => !n.Spec.IsFruit);
        care.Rebuilt = true;
        care.RebuiltAt = HoursOf(garden);
        care.RebuildPruned = pruned;
    }

    // What a path's next level is worth, in genes an hour across the garden: a share more of a kind's genes is that
    // share of what its parts send; of power made, or less drain, that much power, at what a lab turns its drain into
    // genes; the rest (transfer, fruit slots, links, buffs and the like) a small part of the garden's genes, while it
    // has that kind. A level with a milestone counts a tenth of the garden's genes more.
    static double HubValue(Garden garden, HubPath path)
    {
        var lab = NodeSpecs.All[NodeKind.Lab];
        double genesPerPower = lab.BlobsPerHour * Tuning.BlobGenes / -lab.PowerPerSecond;
        double all = garden.Nodes.Where(n => n.Spec.Sends == BlobKind.Genes)
            .Sum(n => Garden.FullBlobsPerHour(n) * Garden.ValueOf(n));
        double value = 0;
        foreach (var effect in path.Each)
        {
            var parts = garden.Nodes.Where(n => n.Spec.Kind == effect.Kind).ToList();
            if (parts.Count == 0) continue;
            value += effect.Axis switch
            {
                HubAxis.Gen => effect.Share * parts.Sum(Garden.FullProductionPerSecond) * genesPerPower,
                HubAxis.Upkeep => -effect.Share * parts.Sum(Garden.FullUpkeepPerSecond) * genesPerPower,
                HubAxis.Blobs => parts[0].Spec.Sends == BlobKind.Genes
                    ? effect.Share * parts.Sum(n => Garden.FullBlobsPerHour(n) * Garden.ValueOf(n))
                    : effect.Share * 0.1 * all,
                _ => Math.Min(1, Math.Abs(effect.Share)) * 0.05 * all,
            };
        }
        if (path.MilestoneAt(garden.HubLevel(path.Kind) + 1) != null) value += 0.1 * all;
        return value;
    }

    // Levels from a path's next one to its next milestone (the tie-break: the nearer first).
    static int ToMilestone(Garden garden, HubPath path) =>
        path.Milestones.Select(m => m.Level - garden.HubLevel(path.Kind)).Where(d => d > 0).DefaultIfEmpty(99).Min();

    // The Hub's recent share of the genes (each check-in's, smoothed), of all that arrived at it or the core.
    static double? UpdateShare(Garden garden)
    {
        var care = CareOf(garden);
        double toHub = garden.HubGenes - care.HubGenes, toCore = garden.Genes - care.Genes;
        (care.Genes, care.HubGenes) = (garden.Genes, garden.HubGenes);
        care.ToHub += SplitSmoothing * (toHub - care.ToHub);
        care.ToCore += SplitSmoothing * (toCore - care.ToCore);
        double sum = care.ToHub + care.ToCore;
        return care.Share = sum > 0 ? care.ToHub / sum : null;
    }

    // Whether a part is the Hub or grows beyond it: a lab there sends its genes mostly to the Hub.
    static bool HubSide(PlantNode n)
    {
        for (var at = n; at != null; at = at.Parent) if (at.IsHub) return true;
        return false;
    }

    // Whether the Hub's side is the one to grow on: for a lab, by the genes' share once they're arriving; else by the
    // count of the kind on each side.
    static bool GrowHubSide(Garden garden, NodeKind kind)
    {
        if (kind == NodeKind.Lab && CareOf(garden).Share is { } share) return share < HubShare;
        var parts = garden.Nodes.Where(n => n.Spec.Kind == kind || n.Forming == kind).ToList();
        return parts.Count(HubSide) * 2 < parts.Count;
    }

    // A stem or lab bud: once the Hub is out and the garden rebuilt, on the side that's short (a lab within MaxLabDepth
    // of the Hub, or of the core), the other if that side has no room; before, as ever.
    static bool BudSided(Garden garden, NodeKind kind)
    {
        int depth = kind == NodeKind.Lab ? MaxLabDepth : int.MaxValue;
        if (garden.Hub is not { } hub || !CareOf(garden).Rebuilt) return Bud(garden, kind, nearFirst: true, maxDepth: depth);
        bool toHub = GrowHubSide(garden, kind);
        return BudOn(garden, kind, toHub, hub, depth) || BudOn(garden, kind, !toHub, hub, depth);
    }

    static bool BudOn(Garden garden, NodeKind kind, bool hubSide, PlantNode hub, int depth)
    {
        var spot = garden.Nodes
            .Where(n => !n.Spec.IsFruit && (n.Parent != null || kind == NodeKind.Stem) && garden.CanSproutFrom(n, kind)
                && HubSide(n) == hubSide && (hubSide ? n.Depth - hub.Depth : n.Depth) < depth)
            .OrderBy(n => n.Depth).ThenBy(n => n.Id).FirstOrDefault();
        return spot != null && garden.SproutFrom(spot, kind);
    }

    // At each check-in, the Hub's recent share of the genes; when it's more than ShareSlack off a half, the farthest lab
    // on the side that's ahead goes, if the other side has a link for one (the lab loop then grows it there).
    static void BalanceGenes(Garden garden)
    {
        if (garden.Hub is null) return;
        var care = CareOf(garden);
        if (UpdateShare(garden) is not { } s || !care.Rebuilt || Math.Abs(s - HubShare) <= ShareSlack) return;
        bool hubAhead = s > HubShare;
        bool roomOtherSide = garden.Nodes.Any(n => !n.Spec.IsFruit && n.Parent != null
            && garden.CanSproutFrom(n, NodeKind.Lab) && HubSide(n) != hubAhead);
        if (!roomOtherSide) return;
        if (garden.Nodes.Where(n => IsLab(n) && HubSide(n) == hubAhead).MaxBy(n => n.Depth) is { } lab)
        {
            garden.Prune(lab);
            care.LabsMoved++;
        }
    }

    // For tests: one check-in's Hub care alone.
    internal static void TendHub(Garden garden) => Hub(garden);

    // Spends the gene balance. While the garden is short of power (it couldn't keep another lab full) and every
    // generator it may have is grown, the next generator comes first: bought if the balance covers it, saved for if
    // not (more stems would only cost more upkeep). Otherwise the cheapest next upgrade across the three tracks, while
    // the balance covers it: the cheap early stems first, then generators and the core's track by turns as their costs
    // climb past the stems'. But when the next core upgrade costs at most twice the next stem, it saves for that
    // instead (Jeremy, Nov 1), so the core track isn't left behind the stems. (Each seedling slot
    // bought is planted at the next check-in: see Seedlings.)
    static void Research(Garden garden)
    {
        while (true)
        {
            bool shortOfPower = Spare(garden) < -NodeSpecs.All[NodeKind.Lab].PowerPerSecond + Reserve
                && Count(garden, NodeKind.Generator) >= garden.Capacity.Generators;
            var core = garden.Next(Track.Core);
            var next = shortOfPower && garden.Next(Track.Generators) is { } generator ? generator
                : core != null && garden.Next(Track.Stems) is { } stem && core.Cost <= 2 * stem.Cost ? core
                : garden.Cheapest;
            if (next == null || !garden.Buy(next.Track)) return;
        }
    }

    // Every free seedling slot gets a seedling, as near the core as there's room (its neighbours there are fullest, so it
    // fills fastest), and every seedling waiting for its choice takes one of its offers at random (from the garden's
    // own dice: the seedling and the evolutions so far, so a run repeats).
    static void Seedlings(Garden garden)
    {
        // Only once the first lab is out, and never on the last free link (it would end the plant's growth there).
        while (garden.FreeSeedlingSlots > 0 && Count(garden, NodeKind.Lab) > 0 && FreeLinks(garden) > 1
            && Bud(garden, NodeKind.Seedling, nearFirst: true)) { }
        foreach (var seedling in garden.Nodes.Where(n => n.Choosing).ToList())
        {
            var offers = seedling.Offers!.ToList();
            var dice = new Random(seedling.Id * 7919 + garden.Evolved.Count);
            while (offers.Count > 0)
            {
                var pick = Style == PlayStyle.Medium ? offers[dice.Next(offers.Count)] : offers.MinBy(k => Rank(garden, k));
                if (garden.Choose(seedling, pick)) break;
                offers.Remove(pick);
            }
        }
    }

    // Labs deeper than this lose most of their gene blobs on the way (they wander), so they aren't worth their upkeep
    // (the bad player doesn't know).
    static int MaxLabDepth => Style == PlayStyle.Bad ? 99 : 4;

    static int FreeLinks(Garden garden) => garden.Nodes.Count(n => !n.Spec.IsFruit && n.CanBranch);

    // While the stem or generator cap has room but no link can take one (passive flowers took them all), the farthest
    // flower off a stem goes, a lab before a grove, always keeping one of each: its link then takes a stem or generator.
    static void MakeRoom(Garden garden)
    {
        while ((NeedsRoom(garden, NodeKind.Stem, garden.Capacity.Stems)
                || NeedsRoom(garden, NodeKind.Generator, garden.Capacity.Generators))
            && garden.Nodes
                .Where(n => n.Spec.Kind is NodeKind.Lab or NodeKind.Grove && n.Parent!.Spec.Kind == NodeKind.Stem
                    && Count(garden, n.Spec.Kind) > 1)
                .OrderBy(n => n.Spec.Kind == NodeKind.Lab ? 0 : 1).ThenByDescending(n => n.Depth)
                .FirstOrDefault() is { } flower)
            garden.Prune(flower);
    }

    static bool NeedsRoom(Garden garden, NodeKind kind, int cap) => Count(garden, kind) < cap
        && !garden.Nodes.Any(n => !n.Spec.IsFruit && n.Parent != null && garden.CanSproutFrom(n, kind));

    static int Count(Garden garden, NodeKind kind) => garden.Nodes.Count(n => n.Spec.Kind == kind);
    static bool Forming(Garden garden, NodeKind kind) => garden.Nodes.Any(n => n.Forming == kind);

    // What the garden would make at full power, less what all its parts would cost: below 0 it's overloaded, and
    // settles lower, its far labs lowest and slowest.
    // Buds forming count at their drain: they'll cost it within minutes (Nov 4: left out, the lab loop started a lab on
    // every free link at once).
    static float Spare(Garden garden) => garden.FullPowerBalance() is var (made, used)
        ? made - used + garden.Nodes.Where(n => n.FormingBud).Sum(n => Math.Min(0, NodeSpecs.All[n.Forming!.Value].PowerPerSecond))
        : 0;

    static void PruneDrains(Garden garden)
    {
        PruneWild(garden);
        // Groves past the one-per-GroveEvery-stems the garden needs only drain it and take slots: plain groves go before
        // the wild ones (a Seed Pod, then a Lumber Grove last), the farthest first.
        while (garden.Nodes.Count(IsGrove) > GrovesWanted(garden)
            && garden.Nodes.Where(IsGrove).OrderBy(n => GroveRank(n.Spec.Kind)).ThenByDescending(n => n.Depth).FirstOrDefault()
                is { } grove)
            garden.Prune(grove);
        while (Spare(garden) < 0 && FarthestLab(garden, _ => true) is { } lab) garden.Prune(lab);
        while (FarthestLab(garden, n => n.Depth > MaxLabDepth) is { } lab) garden.Prune(lab);
    }

    static PlantNode? FarthestLab(Garden garden, Func<PlantNode, bool> which) => garden.Nodes
        .Where(n => IsLab(n) && which(n)).MaxBy(n => n.Depth);

    // Wild Labs and Lichens are labs and Lumber Groves and Seed Pods groves, to the player's rules.
    static bool IsLab(PlantNode n) => n.Spec.Kind is NodeKind.Lab or NodeKind.WildLab or NodeKind.Lichen;
    static bool IsGrove(PlantNode n) => n.Spec.Kind is NodeKind.Grove or NodeKind.SeedPod or NodeKind.LumberGrove;
    static int GroveRank(NodeKind kind) => kind switch { NodeKind.Grove => 0, NodeKind.SeedPod => 1, _ => 2 };

    // Wild flowers (passive, or bloomed from a seedling: pruning one takes only the flower, never its effect) that don't
    // pay for their link go, and a new one may grow elsewhere in the freed slot (a reroll, which the player wants):
    //   - a Resistor (it bounces blobs, genes too), a Moonflower, a Nebula Bloom or a Buffer, always;
    //   - an Amplifier too far out for genes to pass it on their way to the core (past MaxLabDepth);
    //   - an Ember once it has faded below a generator (halfway through its life), so a generator takes its slot.
    // Only at a branch's end: an Amplifier or Resistor with parts grown past it would take them all with it.
    // Wild Labs and Lichens follow the labs' rules, Lumber Groves and Seed Pods the groves'; Pulsars, Twin Stars,
    // Tendrils, Big Stems and Comet Blooms (which wither on their own) stay.
    static void PruneWild(Garden garden)
    {
        if (Style == PlayStyle.Bad) return; // (keeps whatever grows)
        foreach (var flower in garden.Nodes.Where(n => n.Branches.Count == 0 && n.Spec.Kind switch
            {
                NodeKind.Resistor or NodeKind.Moonflower or NodeKind.NebulaBloom => true,
                NodeKind.Buffer => Style != PlayStyle.Best || n.Depth > MaxLabDepth, // (the best player keeps one near the labs)
                NodeKind.Amplifier => n.Depth > MaxLabDepth,
                NodeKind.Ember => n.Age > Tuning.EmberLifeSeconds / 2,
                _ => false,
            }).ToList())
            if (garden.Nodes.Contains(flower)) garden.Prune(flower);
    }

    // A bud of this kind at a free link: the nearest the core, the furthest, or the one in the middle (by depth).
    // Only stems grow on the core. Returns whether one started (it forms, then emerges).
    static bool Bud(Garden garden, NodeKind kind, bool nearFirst, bool middle = false, int maxDepth = int.MaxValue,
        bool hungriest = false)
    {
        // (The bad player puts a lab on a core link too; generators go one to a stem for the best player, side by side
        // for the bad one.)
        bool onCore = kind == NodeKind.Stem || (Style == PlayStyle.Bad && kind == NodeKind.Lab);
        bool Paired(PlantNode n) => n.Branches.Any(b => Garden.IsBudOf(b.Spec.Kind, NodeKind.Generator))
            || n.Forming is { } f && Garden.IsBudOf(f, NodeKind.Generator);
        var spots = garden.Nodes
            .Where(n => !n.Spec.IsFruit && (n.Parent != null || onCore) && n.Depth < maxDepth
                && garden.CanSproutFrom(n, kind))
            .OrderBy(n => kind != NodeKind.Generator || Style == PlayStyle.Medium ? 0 : Style == PlayStyle.Best ? (Paired(n) ? 1 : 0) : (Paired(n) ? 0 : 1))
            .ThenBy(n => hungriest ? n.Fill : 0)
            .ThenBy(n => nearFirst ? n.Depth : -n.Depth).ThenBy(n => n.Id).ToList();
        if (spots.Count == 0) return false;
        var spot = middle ? spots.OrderBy(n => n.Depth).ElementAt(spots.Count / 2) : spots[0];
        return garden.SproutFrom(spot, kind);
    }
}
