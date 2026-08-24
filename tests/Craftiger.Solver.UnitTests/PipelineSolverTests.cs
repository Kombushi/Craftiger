using Craftiger.Solver.Models;

namespace Craftiger.Solver.UnitTests;

/// <summary>LP model construction against the recording fake; the solved-plan behavior runs
/// end-to-end in the HiGHS test project.</summary>
public class PipelineSolverTests
{
    [Fact]
    public void BuildsBalanceRowsWithTargetBound()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore")],
            Fx.Recipe("smelt", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)));
        var lp = new RecordingLpSolver();

        Fx.Pipeline(lp).Solve(graph, Fx.Data(graph), Fx.Garage(), Fx.Weights(), Fx.Request([("ingot", 2)]));

        var program = lp.Program!;
        Assert.Equal(2, program.Rows.Count);
        Assert.Equal(2, program.Rows[0].Lower);
        Assert.Equal(double.PositiveInfinity, program.Rows[0].Upper);
        Assert.Equal(0, program.Rows[1].Lower);
        Assert.Equal(2, program.Columns.Count);
        Assert.Equal([new LpEntry(0, 1), new LpEntry(1, -1)], program.Columns[0].Entries);
        Assert.Equal([new LpEntry(1, 1)], program.Columns[1].Entries);
    }

    [Fact]
    public void SplitsChoiceSlotsThroughLinkRows()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("x"), Fx.Leaf("y")],
            Fx.Recipe("mix", slots: [[("x", 2), ("y", 3)]], outputs: ("out", 1, 1.0)));
        var lp = new RecordingLpSolver();

        Fx.Pipeline(lp).Solve(graph, Fx.Data(graph), Fx.Garage(), Fx.Weights(), Fx.Request([("out", 1)]));

        var program = lp.Program!;
        // Rows: target, link; then x and y through the split columns. Columns: run, two
        // splits, two buys.
        var link = program.Rows[1];
        Assert.Equal(0, link.Lower);
        Assert.Equal(0, link.Upper);
        Assert.Equal(5, program.Columns.Count);
        Assert.Equal([new LpEntry(0, 1), new LpEntry(1, -1)], program.Columns[0].Entries);
        Assert.Equal([new LpEntry(1, 1), new LpEntry(2, -2)], program.Columns[1].Entries);
        Assert.Equal([new LpEntry(1, 1), new LpEntry(3, -3)], program.Columns[2].Entries);
    }

    [Fact]
    public void PinZerosOtherDeterministicProducers()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("a"), Fx.Leaf("b"), Fx.Leaf("c")],
            Fx.Recipe("route1", inputs: [("a", 1)], outputs: ("plate", 1, 1.0)),
            Fx.Recipe("route2", inputs: [("b", 1)], outputs: ("plate", 1, 1.0)),
            Fx.Recipe("chanced", inputs: [("c", 1)], outputs: ("plate", 1, 0.5)));
        var lp = new RecordingLpSolver();

        Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph), Fx.Garage(), Fx.Weights(),
            Fx.Request([("plate", 1)], pins: new() { ["plate"] = "route2" }));

        var program = lp.Program!;
        Assert.Equal(0, program.Columns[0].Upper);
        Assert.Equal(double.PositiveInfinity, program.Columns[1].Upper);
        Assert.Equal(double.PositiveInfinity, program.Columns[2].Upper);
    }

    [Fact]
    public void UnknownTargetFailsWithoutSolving()
    {
        var graph = Fx.Graph([Fx.Leaf("ore")]);
        var lp = new RecordingLpSolver();

        var plan = Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph), Fx.Garage(), Fx.Weights(), Fx.Request([("nope", 1)]));

        Assert.Equal(FactoryPlanStatus.Failed, plan.Status);
        Assert.Contains(new FactoryWarning("target_unknown", "nope"), plan.Warnings);
        Assert.Null(lp.Program);
    }

    [Fact]
    public void UnreachableTargetReportsBeforeSolving()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore")],
            Fx.Recipe("smelt", machine: "Electric Blast Furnace", tier: 5, inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)));
        var lp = new RecordingLpSolver();

        var plan = Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph), Fx.Garage(defaultTier: 1), Fx.Weights(), Fx.Request([("ingot", 1)]));

        Assert.Equal(FactoryPlanStatus.Infeasible, plan.Status);
        Assert.Contains(new FactoryWarning("unreachable_target", "ingot"), plan.Warnings);
        Assert.Null(lp.Program);
    }

    [Fact]
    public void ObjectivesFollowPriorityWithCanonicalizationLast()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore", weight: 7)],
            Fx.Recipe("smelt", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)));
        var lp = new RecordingLpSolver();

        Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph, new() { ["smelt"] = (100, 30, 2) }), Fx.Garage(), Fx.Weights(),
            Fx.Request([("ingot", 1)], priority: [FactoryObjective.Machines, FactoryObjective.Resource]));

        var program = lp.Program!;
        Assert.Equal(3, program.Objectives.Count);
        Assert.Equal([new LpEntry(0, 5)], program.Objectives[0].Coefficients);
        Assert.Equal([new LpEntry(1, 7)], program.Objectives[1].Coefficients);
        Assert.Equal([new LpEntry(0, 1), new LpEntry(1, 1)], program.Objectives[2].Coefficients);
    }

    [Fact]
    public void EnergyLayerPricesDutyCycledDraw()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore")],
            Fx.Recipe("smelt", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)));
        var lp = new RecordingLpSolver();

        Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph, new() { ["smelt"] = (100, 30, 2) }), Fx.Garage(), Fx.Weights(),
            Fx.Request([("ingot", 1)]));

        // kEU per run: 100 ticks × 30 EU/t × 2 A ÷ 1000.
        Assert.Equal([new LpEntry(0, 6)], lp.Program!.Objectives[1].Coefficients);
    }

    [Fact]
    public void InterpretsSolutionIntoPlan()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore", weight: 3)],
            Fx.Recipe("smelt", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)));
        var lp = new RecordingLpSolver
        {
            Result = new LinearProgramResult(LpSolveStatus.Optimal, [2.0, 2.0]),
        };

        var plan = Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph, new() { ["smelt"] = (40, 8, 1) }), Fx.Garage(), Fx.Weights(),
            Fx.Request([("ingot", 2)]));

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        var line = Assert.Single(plan.Lines);
        Assert.Equal("smelt", line.RecipeId);
        Assert.Equal(2, line.RunsPerSecond);
        Assert.Equal(4, line.BusyMachines);
        Assert.False(line.Durationless);
        Assert.Contains(plan.Flows, f => f is { ItemId: "ingot", Produced: 2, Surplus: 0 });
        Assert.Contains(plan.Flows, f => f is { ItemId: "ore", Consumed: 2 });
        var inflow = Assert.Single(plan.Inflows);
        Assert.Equal(("ore", 2.0, 3.0), (inflow.ItemId, inflow.Rate, inflow.Weight));
        Assert.Equal(6, plan.PricedInflowCost);
        Assert.Equal(32, plan.DrawEuT);
        Assert.Equal(4, plan.BusyMachines);
    }

    [Fact]
    public void SolverFailureSurfacesAsWarning()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore")],
            Fx.Recipe("smelt", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)));
        var lp = new RecordingLpSolver
        {
            Result = new LinearProgramResult(LpSolveStatus.Infeasible, []),
        };

        var plan = Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph), Fx.Garage(), Fx.Weights(), Fx.Request([("ingot", 1)]));

        Assert.Equal(FactoryPlanStatus.Infeasible, plan.Status);
        Assert.Contains(new FactoryWarning("infeasible", ""), plan.Warnings);
    }
}
