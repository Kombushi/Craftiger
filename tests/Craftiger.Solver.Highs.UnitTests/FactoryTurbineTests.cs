using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;
using static Craftiger.Solver.Highs.UnitTests.FactoryHarness;

namespace Craftiger.Solver.Highs.UnitTests;

/// <summary>Large turbines: optimal flow under the dynamo cap, the fit as a solver choice, cap-aware frontiers and XL parallels.</summary>
public class FactoryTurbineTests
{
    [Fact]
    public void TurbineRunsAtOptimalFlowCappedByDynamo()
    {
        // Raw 2700 EU/t caps at the HV hatch's 4 x 512, then loses 4 EU per emitted amp; the fuel still burns at full optimal flow — capped excess is voided, not saved. The unpriced rotor never spins.
        var graph = SolverGraph.Build(
            [Leaf("benzene", weight: 1), Leaf("rotor-a", weight: 5)],
            []);
        var machines = Turbines(
            new FactoryMachineBlock("lgt", null, true, false, 0, 1, [], RotorFuel: "GAS"),
            new FactoryDynamo("dyn-hv", 0, 512, 4),
            Rotor("rotor-a", 0.75, 3600, looseEfficiency: 0.70, looseFlow: 4000),
            Rotor("rotor-x", 0.99, 9000));

        var plan = Solve(graph, Energy(2032), machines: machines);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        var line = Dominant(plan);
        Assert.EndsWith("|rotor-a|tight", line.RecipeId);
        Assert.DoesNotContain(plan.Lines, l => l.RecipeId.Contains("rotor-x"));
        Assert.Equal(2032, plan.ExportEuT, 1e-4);
        Assert.Equal(200, Assert.Single(plan.Inflows, i => i.ItemId == "benzene").Rate, 0.5);
    }

    [Fact]
    public void TurbineFitIsASolverChoice()
    {
        // Uncapped, the tight fit burns less fuel per EU and the loose fit makes more EU per machine — the priority order decides which one spins.
        var graph = SolverGraph.Build(
            [Leaf("benzene", weight: 1), Leaf("rotor-a", weight: 5)],
            []);
        var machines = Turbines(
            new FactoryMachineBlock("lgt", null, true, false, 0, 1, [], RotorFuel: "GAS"),
            new FactoryDynamo("dyn-luv", 0, 32768, 4),
            Rotor("rotor-a", 0.80, 1000, looseEfficiency: 0.40, looseFlow: 2500));

        var thrifty = Solve(graph, Energy(500), machines: machines);
        var compact = Solve(
            graph,
            Energy(500, priority: [FactoryObjective.Machines, FactoryObjective.Resource, FactoryObjective.Energy]),
            machines: machines);

        Assert.EndsWith("|tight", Dominant(thrifty).RecipeId);
        Assert.EndsWith("|loose", Dominant(compact).RecipeId);
    }

    [Fact]
    public void CapAwareFrontierKeepsTheModestRotor()
    {
        // Raw stats let the monster rotor dominate everything, then the cap makes it burn 50x the fuel for the same emitted EU — the frontier must judge under the cap, or no turbine survives pruning at all.
        var graph = SolverGraph.Build(
            [Leaf("benzene", weight: 1), Leaf("modest", weight: 5), Leaf("monster", weight: 5)],
            []);
        var machines = Turbines(
            new FactoryMachineBlock("lgt", null, true, false, 0, 1, [], RotorFuel: "GAS"),
            new FactoryDynamo("dyn-hv", 0, 512, 4),
            Rotor("modest", 0.90, 2000),
            Rotor("monster", 2.0, 100000));

        var plan = Solve(graph, Energy(1500), machines: machines);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        Assert.Contains("|modest|", Dominant(plan).RecipeId);
    }

    [Fact]
    public void XlTurbineFoldsItsThroughputIntoOneController()
    {
        // Parallels 16 multiply flow and output per controller, and the XL takes the multi-amp hatch a large turbine must refuse.
        var graph = SolverGraph.Build(
            [Leaf("benzene", weight: 1), Leaf("rotor-a", weight: 5)],
            []);
        var machines = Turbines(
            new FactoryMachineBlock("xlgt", null, true, false, 0, 16, [], RotorFuel: "GAS"),
            new FactoryDynamo("dyn-16a", 0, 512, 16),
            Rotor("rotor-a", 0.75, 133.33333333333334));

        var plan = Solve(graph, Energy(1587.5), machines: machines);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        var line = Assert.Single(plan.Lines);
        Assert.Equal(1, line.RunsPerSecond, 1e-4);
        Assert.Equal(1, line.BusyMachines, 1e-4);
        Assert.Equal(1587.5, plan.ExportEuT, 1e-4);
    }
}
