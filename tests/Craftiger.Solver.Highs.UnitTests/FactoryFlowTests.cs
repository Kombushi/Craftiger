using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;
using static Craftiger.Solver.Highs.UnitTests.FactoryHarness;

namespace Craftiger.Solver.Highs.UnitTests;

/// <summary>Steady-state flow: expected values, loops, byproducts, choices, pins, priorities and canonicalization.</summary>
public class FactoryFlowTests
{
    [Fact]
    public void ChancedOutputsRunAtExpectedValue()
    {
        var graph = SolverGraph.Build(
            [Leaf("ore")],
            [Recipe("sift", inputs: [("ore", 1)], outputs: ("gem", 1, 0.25))]);

        var plan = Solve(graph, Produce([("gem", 1)]));

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        var line = Assert.Single(plan.Lines);
        Assert.Equal(4, line.RunsPerSecond, Tolerance);
        var inflow = Assert.Single(plan.Inflows);
        Assert.Equal(4, inflow.Rate, Tolerance);
    }

    [Fact]
    public void LoopBalancesAsSteadyStateFlow()
    {
        // The reactor returns 9 of the 10 solvent it takes; only the makeup is purchased.
        var graph = SolverGraph.Build(
            [Leaf("solvent"), Leaf("raw")],
            [Recipe("react", inputs: [("solvent", 10), ("raw", 1)], outputs: [("product", 1, 1.0), ("solvent", 9, 1.0)])]);

        var plan = Solve(graph, Produce([("product", 1)]));

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        Assert.Equal(1, Assert.Single(plan.Lines).RunsPerSecond, Tolerance);
        var solvent = Assert.Single(plan.Inflows, i => i.ItemId == "solvent");
        Assert.Equal(1, solvent.Rate, Tolerance);
    }

    [Fact]
    public void ByproductFeedsSecondTargetWithoutPurchase()
    {
        var graph = SolverGraph.Build(
            [Leaf("x"), Leaf("by")],
            [Recipe("main", inputs: [("x", 1)], outputs: [("target", 1, 1.0), ("by", 1, 1.0)])]);

        var plan = Solve(graph, Produce([("target", 1), ("by", 1)]));

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        Assert.DoesNotContain(plan.Inflows, i => i.ItemId == "by");
        Assert.Equal(1, Assert.Single(plan.Inflows, i => i.ItemId == "x").Rate, Tolerance);
    }

    [Fact]
    public void LeafOverproductionIsSurplusNeverCredit()
    {
        var graph = SolverGraph.Build(
            [Leaf("ore", weight: 2), Leaf("rich", weight: 100)],
            [Recipe("smelt", inputs: [("ore", 1)], outputs: [("ingot", 1, 1.0), ("rich", 5, 1.0)])]);

        var plan = Solve(graph, Produce([("ingot", 1)]));

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        Assert.Equal(2, plan.PricedInflowCost, Tolerance);
        var rich = Assert.Single(plan.Flows, f => f.ItemId == "rich");
        Assert.Equal(5, rich.Surplus, Tolerance);
    }

    [Fact]
    public void CanonicalizationRemovesZeroCostChurn()
    {
        var graph = SolverGraph.Build(
            [Leaf("ore")],
            [
                Recipe("smelt", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)),
                Recipe("pack", inputs: [("ingot", 1)], outputs: ("block", 1, 1.0)),
                Recipe("unpack", inputs: [("block", 1)], outputs: ("ingot", 1, 1.0)),
            ]);

        var plan = Solve(graph, Produce([("ingot", 1)]));

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        Assert.Equal("smelt", Assert.Single(plan.Lines).RecipeId);
    }

    [Fact]
    public void ChoiceSlotBuysTheCheaperAlternative()
    {
        var graph = SolverGraph.Build(
            [Leaf("pricey", weight: 10), Leaf("cheap", weight: 1)],
            [Recipe("mix", slots: [[("pricey", 1), ("cheap", 1)]], outputs: ("out", 1, 1.0))]);

        var plan = Solve(graph, Produce([("out", 1)]));

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        var inflow = Assert.Single(plan.Inflows);
        Assert.Equal("cheap", inflow.ItemId);
        Assert.Equal(1, plan.PricedInflowCost, Tolerance);
    }

    [Fact]
    public void LeafTargetIsProducedWhenCheaperThanBuying()
    {
        var graph = SolverGraph.Build(
            [Leaf("plastic", weight: 100), Leaf("oil", weight: 1)],
            [Recipe("polymerize", inputs: [("oil", 1)], outputs: ("plastic", 1, 1.0))]);

        var plan = Solve(graph, Produce([("plastic", 2)]));

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        Assert.Equal(2, Assert.Single(plan.Lines).RunsPerSecond, Tolerance);
        Assert.Equal("oil", Assert.Single(plan.Inflows).ItemId);
        Assert.Equal(2, plan.PricedInflowCost, Tolerance);
    }

    [Fact]
    public void LeafTargetIsBoughtWhenCheaperThanProducing()
    {
        var graph = SolverGraph.Build(
            [Leaf("plastic", weight: 1), Leaf("oil", weight: 100)],
            [Recipe("polymerize", inputs: [("oil", 1)], outputs: ("plastic", 1, 1.0))]);

        var plan = Solve(graph, Produce([("plastic", 2)]));

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        Assert.Empty(plan.Lines);
        Assert.Equal("plastic", Assert.Single(plan.Inflows).ItemId);
        Assert.Equal(2, plan.PricedInflowCost, Tolerance);
    }

    [Fact]
    public void PinForcesTheRoute()
    {
        var graph = SolverGraph.Build(
            [Leaf("cheap", weight: 1), Leaf("pricey", weight: 10)],
            [
                Recipe("viaCheap", inputs: [("cheap", 1)], outputs: ("plate", 1, 1.0)),
                Recipe("viaPricey", inputs: [("pricey", 1)], outputs: ("plate", 1, 1.0)),
            ]);

        var free = Solve(graph, Produce([("plate", 1)]));
        var pinned = Solve(graph, Produce([("plate", 1)], pins: new() { ["plate"] = "viaPricey" }));

        Assert.Equal("viaCheap", Assert.Single(free.Lines).RecipeId);
        Assert.Equal("viaPricey", Assert.Single(pinned.Lines).RecipeId);
        Assert.Equal(10, pinned.PricedInflowCost, Tolerance);
    }

    [Fact]
    public void PriorityOrderDecidesEnergyVersusMachines()
    {
        var graph = SolverGraph.Build(
            [Leaf("ore")],
            [
                Recipe("slowCool", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)),
                Recipe("fastHot", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)),
            ]);
        var data = new Dictionary<string, (long, long, long)>
        {
            ["slowCool"] = (200, 1, 1),
            ["fastHot"] = (20, 100, 1),
        };

        var energyFirst = Solve(
            graph,
            Produce([("ingot", 1)], priority: [FactoryObjective.Resource, FactoryObjective.Energy, FactoryObjective.Machines]),
            data);
        var machinesFirst = Solve(
            graph,
            Produce([("ingot", 1)], priority: [FactoryObjective.Resource, FactoryObjective.Machines, FactoryObjective.Energy]),
            data);

        Assert.Equal("slowCool", Dominant(energyFirst).RecipeId);
        Assert.Equal("fastHot", Dominant(machinesFirst).RecipeId);
    }

    [Fact]
    public void RepeatedSolvesReturnIdenticalPlans()
    {
        var graph = SolverGraph.Build(
            [Leaf("a"), Leaf("b")],
            [
                Recipe("viaA", inputs: [("a", 1)], outputs: ("out", 1, 1.0)),
                Recipe("viaB", inputs: [("b", 1)], outputs: ("out", 1, 1.0)),
            ]);

        var first = Solve(graph, Produce([("out", 3)]));
        var second = Solve(graph, Produce([("out", 3)]));

        Assert.Equal(FactoryPlanStatus.Solved, first.Status);
        // Line flows are lists, so record equality stops at them; equivalence compares the values.
        Assert.Equivalent(first.Lines, second.Lines, strict: true);
        Assert.Equal(first.Inflows, second.Inflows);
        Assert.Equal(first.Flows, second.Flows);
    }

    [Fact]
    public void PriorityPicksTheOverclockLevel()
    {
        // Fewer overclocks halve power per step; more halve machine time. The priority order decides, on the same block.
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

        var energyFirst = Solve(
            graph,
            Produce([("ingot", 1)], priority: [FactoryObjective.Resource, FactoryObjective.Energy, FactoryObjective.Machines]),
            data, machines, garageTier: 3);
        var machinesFirst = Solve(
            graph,
            Produce([("ingot", 1)], priority: [FactoryObjective.Resource, FactoryObjective.Machines, FactoryObjective.Energy]),
            data, machines, garageTier: 3);

        Assert.Equal(0, energyFirst.Lines.MaxBy(l => l.RunsPerSecond)!.OcSteps);
        Assert.Equal(3, machinesFirst.Lines.MaxBy(l => l.RunsPerSecond)!.OcSteps);
    }

    [Fact]
    public void ParallelsDivideBusyMachines()
    {
        var graph = SolverGraph.Build(
            [Leaf("ore")],
            [Recipe("smelt", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0))]);
        var machines = new FactoryMachineData(
            new Dictionary<string, IReadOnlyList<FactoryMachineBlock>>
            {
                ["Crafting Table"] = [new FactoryMachineBlock("multi", null, true, false, 0, 4, [])],
            },
            [], [], [], []);

        var plan = Solve(
            graph, Produce([("ingot", 1)]),
            new Dictionary<string, (long, long, long)> { ["smelt"] = (80, 4, 1) }, machines);

        var line = Assert.Single(plan.Lines);
        Assert.Equal("multi", line.MachineItemId);
        Assert.Equal(4, line.Parallels);
        Assert.Equal(1, line.BusyMachines, 5);
        Assert.Equal(1, plan.BusyMachines, 5);
    }
}
