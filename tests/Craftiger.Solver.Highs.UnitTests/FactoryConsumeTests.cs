using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;
using static Craftiger.Solver.Highs.UnitTests.FactoryHarness;

namespace Craftiger.Solver.Highs.UnitTests;

/// <summary>Consume targets: the supply is absorbed in full or the shortfall is named.</summary>
public class FactoryConsumeTests
{
    [Fact]
    public void ConsumeTargetProcessesTheSupply()
    {
        // Example 3 in miniature: supplied raw ore macerates into dusts with a chanced byproduct; everything supplied is absorbed and the outputs land as surplus.
        var graph = SolverGraph.Build(
            [Leaf("dust", weight: 2), Leaf("bonus", weight: 3)],
            [Recipe("macerate", inputs: [("rawore", 1)], outputs: [("dust", 2, 1.0), ("bonus", 1, 0.5)])]);

        var plan = Solve(graph, Consume("rawore", 2));

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        Assert.DoesNotContain(plan.Warnings, w => w.Kind == FactoryWarningKind.ConsumeShortfall);
        Assert.Equal(2, Assert.Single(plan.Lines).RunsPerSecond, Tolerance);
        var ore = Assert.Single(plan.Flows, f => f.ItemId == "rawore");
        Assert.Equal(2, ore.Supplied, Tolerance);
        Assert.Equal(2, ore.Consumed, Tolerance);
        Assert.Equal(4, Assert.Single(plan.Flows, f => f.ItemId == "dust").Surplus, Tolerance);
        Assert.Equal(1, Assert.Single(plan.Flows, f => f.ItemId == "bonus").Surplus, Tolerance);
    }

    [Fact]
    public void ConsumeShortfallNamesTheItem()
    {
        // The only consumer needs an unmakeable co-input, so nothing can be absorbed — the plan still solves and reports the shortfall instead of a bare infeasibility.
        var graph = SolverGraph.Build(
            [Leaf("dust", weight: 1)],
            [Recipe("wash", inputs: [("rawore", 1), ("mystery", 1)], outputs: ("dust", 1, 1.0))]);

        var plan = Solve(graph, Consume("rawore", 2));

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        Assert.Contains(FactoryWarning.ConsumeShortfall("rawore"), plan.Warnings);
        Assert.Empty(plan.Lines);
    }
}
