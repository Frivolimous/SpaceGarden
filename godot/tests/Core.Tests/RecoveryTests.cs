namespace SpaceGarden.Core.Tests;

// An experiment, run by hand (RECOVERY=1), not a check: the same gardens with transfer following the fill (Nov 3) and
// without (the full rate whatever the fill), to see which recovers better from running low. Its report is the skip
// message. It flips Tuning.TransferFollowsFill, so run it on its own (--filter-method "*TransferRuleRecovery*").
public class RecoveryTests
{
    static PlantNode Add(Garden garden, PlantNode parent, NodeKind kind) =>
        garden.GrowBranch(parent, kind) ?? throw new InvalidOperationException($"no room for a {kind} on {parent.Spec.Kind}");

    // A trunk of stems from the core, each with a side stem holding a generator every genEvery and a lab every labEvery,
    // and a far tip of two labs: a fixed shape, no growth.
    static (Garden Garden, List<PlantNode> Labs, List<PlantNode> FarLabs) Build(int trunk, int genEvery, int labEvery, int seed = 11)
    {
        var garden = Garden.New(Layout.Radial, seed);
        garden.PassiveGrowth = false;
        garden.Capacity = Capacity.Unlimited;
        var labs = new List<PlantNode>();
        var at = garden.Core;
        var trunkStems = new List<PlantNode>();
        for (int i = 0; i < trunk; i++)
        {
            at = Add(garden, at, NodeKind.Stem);
            trunkStems.Add(at);
            var side = Add(garden, at, NodeKind.Stem);
            if (i % genEvery == 0) Add(garden, side, NodeKind.Generator);
            if (i % labEvery == 0) labs.Add(Add(garden, side, NodeKind.Lab));
        }
        var tip = Add(garden, at, NodeKind.Stem);
        var far = new List<PlantNode> { Add(garden, tip, NodeKind.Lab), Add(garden, tip, NodeKind.Lab) };
        labs.AddRange(far);
        return (garden, labs, far);
    }

    static float Fill(IEnumerable<PlantNode> parts)
    {
        var list = parts.Where(n => n.HoldsGardenPower).ToList();
        return list.Sum(n => n.Power) / list.Sum(n => n.Store);
    }

    static float MeanFill(IEnumerable<PlantNode> parts) => parts.Average(n => Math.Clamp(n.Fill, 0, 2));

    // Grows in steps, returning the growth seconds until the garden's fill first reaches target (or null).
    static double? Until(Garden garden, double seconds, Func<bool> done)
    {
        for (double t = 0; t < seconds; t += 10)
        {
            if (done()) return t;
            garden.Grow(10);
        }
        return done() ? seconds : null;
    }

    static string Minutes(double? s) => s is { } v ? $"{v / 60:0.0} min" : "never";

    [Fact]
    public void TransferRuleRecovery()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("RECOVERY") == "1", "an experiment, run by hand (RECOVERY=1)");
        var report = new System.Text.StringBuilder();
        bool before = Tuning.TransferFollowsFill;
        try
        {
            foreach (bool follows in new[] { true, false })
            {
                Tuning.TransferFollowsFill = follows;
                report.AppendLine(follows ? "TRANSFER FOLLOWS FILL (now)" : "FULL RATE AT ANY FILL (before)");

                // A healthy garden (makes more than it costs at full), drained to 5%: how fast it refills.
                {
                    var (garden, labs, far) = Build(trunk: 10, genEvery: 1, labEvery: 2);
                    var (made, cost) = garden.FullPowerBalance();
                    foreach (var n in garden.Nodes) n.Power = n.Store * 0.05f;
                    double genes = garden.Genes;
                    var to50 = Until(garden, 3 * 3600, () => Fill(garden.Nodes) >= 0.5f);
                    var to80 = Until(garden, 3 * 3600, () => Fill(garden.Nodes) >= 0.8f);
                    report.AppendLine($"  drained healthy garden (makes {made:0}/s, costs {cost:0}/s at full): " +
                        $"50% in {Minutes(to50)}, then 80% in {Minutes(to80)} more; far labs at {MeanFill(far):0%}, " +
                        $"labs at {MeanFill(labs):0%}, {garden.Genes - genes:0} genes on the way");
                }

                // An overloaded garden (extra labs: costs more than it makes at full), full at the start: where it settles.
                {
                    var (garden, labs, far) = Build(trunk: 10, genEvery: 2, labEvery: 1);
                    var (made, cost) = garden.FullPowerBalance();
                    foreach (var n in garden.Nodes) n.Power = n.Store;
                    double genes0 = garden.Genes;
                    garden.Grow(3600);
                    double genesHour = garden.Genes - genes0;
                    report.AppendLine($"  overloaded garden (makes {made:0}/s, costs {cost:0}/s at full), after an hour: " +
                        $"garden {Fill(garden.Nodes):0%}, core {garden.Core.Fill:0%}, labs {MeanFill(labs):0%} " +
                        $"(far {MeanFill(far):0%}), {genesHour:0} genes that hour");

                    // Relief: every lab but the far ones pruned, so it makes more than it costs: how fast it climbs back.
                    foreach (var lab in labs.Where(l => !far.Contains(l)).ToList()) garden.Prune(lab);
                    var (made2, cost2) = garden.FullPowerBalance();
                    var back50 = Until(garden, 3 * 3600, () => Fill(garden.Nodes) >= 0.5f);
                    var back = Until(garden, 3 * 3600, () => Fill(garden.Nodes) >= 0.7f);
                    report.AppendLine($"    relieved (labs but the far ones pruned: makes {made2:0}/s, costs {cost2:0}/s): 50% in {Minutes(back50)}, 70% in {Minutes(back)} more; " +
                        $"labs then {MeanFill(garden.Nodes.Where(n => n.Spec.Kind == NodeKind.Lab)):0%}, far {MeanFill(far.Where(garden.Nodes.Contains)):0%}");
                }

                // Steady state of the healthy garden from full, over 2 hours: the genes, and how the far labs fare.
                {
                    var (garden, labs, far) = Build(trunk: 10, genEvery: 1, labEvery: 2);
                    foreach (var n in garden.Nodes) n.Power = n.Store;
                    garden.Grow(3600);
                    double g0 = garden.Genes;
                    garden.Grow(3600);
                    report.AppendLine($"  healthy garden, steady (second hour): garden {Fill(garden.Nodes):0%}, labs {MeanFill(labs):0%}, " +
                        $"far labs {MeanFill(far):0%}, {garden.Genes - g0:0} genes an hour");
                }
                report.AppendLine();
            }
        }
        finally { Tuning.TransferFollowsFill = before; }
        Assert.Skip(report.ToString());
    }
}
