using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;
using static Craftiger.Solver.Highs.UnitTests.FactoryHarness;

namespace Craftiger.Solver.Highs.UnitTests;

/// <summary>Infeasibility is never bare: the pin or the missing item is named.</summary>
public class FactoryDiagnosisTests
{
    [Fact]
    public void ElasticDiagnosisNamesTheMissingItem()
    {
        // The pinned route needs an unmakeable input; the elastic re-solve keeps the cheaper slack — the missing input, not the target — and names it.
        var graph = SolverGraph.Build(
            [],
            [Recipe("make", inputs: [("x", 1)], outputs: ("t", 2, 1.0))]);

        var plan = Solve(graph, Produce([("t", 4)], pins: new Dictionary<string, string> { ["t"] = "make" }));

        Assert.Equal(FactoryPlanStatus.Infeasible, plan.Status);
        Assert.Contains(FactoryWarning.InfeasibleItem("x"), plan.Warnings);
        Assert.DoesNotContain(plan.Warnings, w => w.Kind == FactoryWarningKind.Infeasible);
        Assert.DoesNotContain(FactoryWarning.InfeasibleItem("t"), plan.Warnings);
    }

    [Fact]
    public void PinConflictIsDiagnosed()
    {
        // The pinned recipe is garage-illegal and the pin removes the only legal route: lifting the pins restores feasibility, so the pin is named instead of the item.
        var graph = SolverGraph.Build(
            [Leaf("l", weight: 1), Leaf("m", weight: 1)],
            [
                Recipe("alpha", inputs: [("l", 1)], tier: 1, outputs: ("t", 1, 1.0)),
                Recipe("beta", inputs: [("m", 1)], outputs: ("t", 1, 1.0)),
            ]);

        var plan = Solve(graph, Produce([("t", 1)], pins: new Dictionary<string, string> { ["t"] = "alpha" }));

        Assert.Equal(FactoryPlanStatus.Infeasible, plan.Status);
        Assert.Contains(FactoryWarning.PinIllegal("t"), plan.Warnings);
        Assert.Contains(FactoryWarning.PinConflict("t"), plan.Warnings);
        Assert.DoesNotContain(plan.Warnings, w => w.Kind is FactoryWarningKind.Infeasible
            or FactoryWarningKind.InfeasibleItem or FactoryWarningKind.InfeasibleEnergy);
    }
}
