using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;
using static Craftiger.Solver.Highs.UnitTests.FactoryHarness;

namespace Craftiger.Solver.Highs.UnitTests;

/// <summary>Pipeline solves: the candidate set is exactly the steps, whatever no step makes is supplied at its standing price, and a step's pin narrows the run variants.</summary>
public sealed class FactoryPipelineTests
{
    private static SolverGraph TwoRoutes() => SolverGraph.Build(
        [Leaf("a", 1), Leaf("b", 2)],
        [
            Recipe("viaA", inputs: [("a", 1)], outputs: ("out", 1, 1.0)),
            Recipe("viaB", inputs: [("b", 4)], outputs: ("out", 1, 1.0)),
        ]);

    [Fact]
    public void APipelineRunsOnlyItsSteps()
    {
        // The free solve would pick viaA (1 per unit against 8); the step forces the dear route.
        var plan = Solve(TwoRoutes(), Produce([("out", 2)], steps: [new FactoryStep("viaB")]));

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        var line = Assert.Single(plan.Lines);
        Assert.Equal("viaB", line.RecipeId);
        var inflow = Assert.Single(plan.Inflows);
        Assert.Equal("b", inflow.ItemId);
        Assert.Equal(8, inflow.Rate, 5);
        Assert.Equal(16, plan.PricedInflowCost, 4);
    }

    [Fact]
    public void AMissingInputIsSuppliedAtItsStandingPrice()
    {
        var graph = SolverGraph.Build(
            [Leaf("base", 1)],
            [
                Recipe("makeMid", inputs: [("base", 3)], outputs: ("mid", 1, 1.0)),
                Recipe("makeOut", inputs: [("mid", 2)], outputs: ("out", 1, 1.0)),
            ]);

        var plan = Solve(graph, Produce([("out", 1)], steps: [new FactoryStep("makeOut")]));

        // mid is no leaf, yet the pipeline supplies it — charged what its chain would cost.
        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        Assert.Equal("makeOut", Assert.Single(plan.Lines).RecipeId);
        var inflow = Assert.Single(plan.Inflows);
        Assert.Equal("mid", inflow.ItemId);
        Assert.Equal(2, inflow.Rate, 5);
        Assert.Equal(3, inflow.Weight, 5);
        Assert.Equal(6, plan.PricedInflowCost, 4);
    }

    [Fact]
    public void AProduceTargetIsNeverSupplied()
    {
        var plan = Solve(TwoRoutes(), Produce([("b", 1)], steps: [new FactoryStep("viaA")]));

        Assert.Equal(FactoryPlanStatus.Infeasible, plan.Status);
        Assert.Contains(plan.Warnings, warning => warning.Kind == FactoryWarningKind.UnreachableTarget && warning.ItemId == "b");
    }

    [Fact]
    public void PinsAreIgnoredInAPipeline()
    {
        var plan = Solve(
            TwoRoutes(),
            Produce(
                [("out", 2)],
                pins: new Dictionary<string, string> { ["out"] = "viaA" },
                steps: [new FactoryStep("viaB")]));

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        Assert.Equal("viaB", Assert.Single(plan.Lines).RecipeId);
        Assert.DoesNotContain(plan.Warnings, warning => warning.Kind == FactoryWarningKind.PinConflict);
    }

    [Fact]
    public void AnUnknownStepWarnsAndTheRestSolves()
    {
        var plan = Solve(
            TwoRoutes(),
            Produce([("out", 1)], steps: [new FactoryStep("viaA"), new FactoryStep("nope")]));

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        Assert.Equal("viaA", Assert.Single(plan.Lines).RecipeId);
        Assert.Contains(plan.Warnings, warning => warning.Kind == FactoryWarningKind.StepUnknown && warning.ItemId == "nope");
    }

    private static (SolverGraph Graph, FactoryMachineData Machines, Dictionary<string, (long, long, long)> Data) OverclockFixture()
    {
        var graph = SolverGraph.Build(
            [Leaf("ore")],
            [Recipe("smelt", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0))]);
        var machines = new FactoryMachineData(
            new Dictionary<string, IReadOnlyList<FactoryMachineBlock>>
            {
                ["Crafting Table"] = [new FactoryMachineBlock("mac-hv", 3, false, false, 3, 1, [])],
            },
            [], [], [], []);
        var data = new Dictionary<string, (long, long, long)> { ["smelt"] = (160, 4, 1) };
        return (graph, machines, data);
    }

    private static readonly FactoryObjective[] MachinesFirst =
        [FactoryObjective.Resource, FactoryObjective.Machines, FactoryObjective.Energy];

    [Fact]
    public void AStepPinForcesTheOverclock()
    {
        var (graph, machines, data) = OverclockFixture();

        // Machines-first would overclock to 3 steps; the pin holds the line at none.
        var plan = Solve(
            graph,
            Produce([("ingot", 1)], priority: MachinesFirst, steps: [new FactoryStep("smelt", OcSteps: 0)]),
            data, machines, garageTier: 3);

        Assert.Equal(0, plan.Lines.MaxBy(line => line.RunsPerSecond)!.OcSteps);
        Assert.DoesNotContain(plan.Warnings, warning => warning.Kind == FactoryWarningKind.StepVariantUnknown);
    }

    [Fact]
    public void AnImpossibleVariantPinFallsBackVisibly()
    {
        var (graph, machines, data) = OverclockFixture();

        var plan = Solve(
            graph,
            Produce([("ingot", 1)], priority: MachinesFirst, steps: [new FactoryStep("smelt", OcSteps: 9)]),
            data, machines, garageTier: 3);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        Assert.Equal(3, plan.Lines.MaxBy(line => line.RunsPerSecond)!.OcSteps);
        Assert.Contains(plan.Warnings, warning => warning.Kind == FactoryWarningKind.StepVariantUnknown && warning.ItemId == "smelt");
    }

    [Fact]
    public void AGeneratorStepPicksItsLine()
    {
        // The dear fuel would be pruned and never chosen freely; the step selects it by line id.
        var graph = SolverGraph.Build([Leaf("cheap", 1), Leaf("dear", 100)], []);
        var machines = new FactoryMachineData(
            new Dictionary<string, IReadOnlyList<FactoryMachineBlock>>
            {
                ["Gen"] = [new FactoryMachineBlock(
                    "gen-mv", 2, false, false, 2, 1, [], GeneratorEfficiency: 100, GeneratorEuT: 128, GeneratorAmps: 1)],
            },
            [],
            [new FactoryFuel("Gen", "cheap", 1, 360, null, null), new FactoryFuel("Gen", "dear", 1, 360, null, null)],
            [], []);

        var free = Solve(graph, Energy(64), machines: machines, garageTier: 2);
        var steered = Solve(
            graph,
            Energy(64, steps: [new FactoryStep("generator|gen-mv|dear")]),
            machines: machines, garageTier: 2);

        Assert.Equal("generator|gen-mv|cheap", Dominant(free).RecipeId);
        Assert.Equal(FactoryPlanStatus.Solved, steered.Status);
        Assert.Equal("generator|gen-mv|dear", Assert.Single(steered.Lines).RecipeId);
    }
}
