using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;
using static Craftiger.Solver.Highs.UnitTests.FactoryHarness;

namespace Craftiger.Solver.Highs.UnitTests;

/// <summary>Combustion engines and mode-driven reactors: boosted variants, consumable flows, and hatch gates.</summary>
public class FactoryBoostedGeneratorTests
{
    private static readonly GeneratorMode Oxygen = new(GeneratorModeKind.Booster, "oxygen", 40, 3);
    private static readonly GeneratorMode Lubricant = new(GeneratorModeKind.Lubricant, "lubricant", 20.0 / 72, 1);

    private static FactoryMachineBlock Engine => new(
        "engine", null, Multiblock: true, Steam: false, Era: 0, MaxParallel: 1, [], GeneratorEuT: 2048);

    [Fact]
    public void EngineBoostTriplesOutputForOxygenAndLubricant()
    {
        var graph = SolverGraph.Build(
            [Leaf("diesel", weight: 1), Leaf("oxygen", weight: 0.01), Leaf("lubricant", weight: 0.01)], []);
        var machines = Generators(
            "Combustion Generator Fuels", Engine,
            new FactoryFuel("Combustion Generator Fuels", "diesel", 1, 512, null, null),
            dynamos: [new FactoryDynamo("dyn-iv", 0, 8192, 1)],
            modes: [Oxygen, Lubricant]);

        var plan = Solve(graph, Energy(6000), machines: machines, garageTier: 5);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        // Boosting nets 6,132 EU/t for 160 L/s of diesel; three base engines would burn 240 L/s.
        var line = Dominant(plan);
        Assert.EndsWith("|boost", line.RecipeId);
        var runs = line.RunsPerSecond;
        Assert.Equal(160 * runs, Assert.Single(plan.Inflows, i => i.ItemId == "diesel").Rate, 1e-3);
        Assert.Equal(40 * runs, Assert.Single(plan.Inflows, i => i.ItemId == "oxygen").Rate, 1e-3);
        Assert.Equal(2 * 20.0 / 72 * runs, Assert.Single(plan.Inflows, i => i.ItemId == "lubricant").Rate, 1e-4);
    }

    [Fact]
    public void EngineRefusesFuelRicherThanItsNominalUnboosted()
    {
        var graph = SolverGraph.Build(
            [Leaf("hog", weight: 1), Leaf("oxygen", weight: 0.01), Leaf("lubricant", weight: 0.01)], []);
        var machines = Generators(
            "Combustion Generator Fuels", Engine,
            new FactoryFuel("Combustion Generator Fuels", "hog", 1, 2500, null, null),
            dynamos: [new FactoryDynamo("dyn-iv", 0, 8192, 1)],
            modes: [Oxygen, Lubricant]);

        var plan = Solve(graph, Energy(6000), machines: machines, garageTier: 5);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        var line = Assert.Single(plan.Lines);
        Assert.EndsWith("|boost", line.RecipeId);
        // Integer division reads one liter per tick; the weighted top-up adds the fraction of 6144 over 3750.
        var expected = (1 + 6144.0 / 3750 - 1) * 20 * line.RunsPerSecond;
        Assert.Equal(expected, Assert.Single(plan.Inflows, i => i.ItemId == "hog").Rate, 1e-3);
    }

    [Fact]
    public void ReactorPicksItsCheapestModeAndReturnsSpentFuel()
    {
        var graph = SolverGraph.Build(
            [
                Leaf("naqfuel", weight: 1000), Leaf("air", weight: 0.0001),
                Leaf("coolant", weight: 0.0001), Leaf("excite", weight: 50), Leaf("depleted"),
            ],
            []);
        var machines = Generators(
            "Large Naquadah Reactor",
            new FactoryMachineBlock("reactor", null, Multiblock: true, Steam: false, Era: 0, MaxParallel: 1, []),
            new FactoryFuel("Large Naquadah Reactor", "naqfuel", 1, null, 100000, 200, "depleted", 1),
            dynamos: [new FactoryDynamo("dyn-16a", 0, 65536, 16)],
            modes:
            [
                new GeneratorMode(GeneratorModeKind.Upkeep, "air", 2400, 1),
                new GeneratorMode(GeneratorModeKind.Coolant, "coolant", 1000, 1.5),
                new GeneratorMode(GeneratorModeKind.Excited, "excite", 20, 4),
            ]);

        var plan = Solve(graph, Energy(550000), machines: machines, garageTier: 6);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        // The coolant multiplies output alone, so it wins the resource layer; the excited liquid scales fuel with output and only saves machines.
        var line = Dominant(plan);
        Assert.Contains("c~coolant", line.RecipeId);
        Assert.DoesNotContain("x~", line.RecipeId);
        // The corridor's losing-route sliver shifts flows below a tenth of a percent.
        var runs = line.RunsPerSecond;
        Assert.Equal(2400 * runs, Assert.Single(plan.Inflows, i => i.ItemId == "air").Rate, 2400 * runs * 1e-3);
        var fuelRate = Assert.Single(plan.Inflows, i => i.ItemId == "naqfuel").Rate;
        Assert.Equal(0.1 * runs, fuelRate, 0.1 * runs * 1e-2);
        Assert.Equal(fuelRate, Assert.Single(plan.Flows, f => f.ItemId == "depleted").Produced, fuelRate * 1e-2);
    }

    [Fact]
    public void ReactorSkipsModesItsHatchCannotCover()
    {
        var graph = SolverGraph.Build(
            [Leaf("naqfuel", weight: 1), Leaf("air", weight: 0.0001), Leaf("coolant", weight: 0.0001)], []);
        var machines = Generators(
            "Large Naquadah Reactor",
            new FactoryMachineBlock("reactor", null, Multiblock: true, Steam: false, Era: 0, MaxParallel: 1, []),
            new FactoryFuel("Large Naquadah Reactor", "naqfuel", 1, null, 100000, 200),
            dynamos: [new FactoryDynamo("dyn-2a", 0, 65536, 2)],
            modes:
            [
                new GeneratorMode(GeneratorModeKind.Upkeep, "air", 2400, 1),
                new GeneratorMode(GeneratorModeKind.Coolant, "coolant", 1000, 1.5),
            ]);

        var plan = Solve(graph, Energy(100000), machines: machines, garageTier: 6);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        // Coolant would beat the bare run outright, but its 150,000 EU/t stops the reactor on a 131,072-capacity hatch.
        var line = Assert.Single(plan.Lines);
        Assert.DoesNotContain("c~", line.RecipeId);
    }
}
