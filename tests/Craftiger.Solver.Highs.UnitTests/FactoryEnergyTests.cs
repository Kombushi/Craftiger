using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;
using static Craftiger.Solver.Highs.UnitTests.FactoryHarness;

namespace Craftiger.Solver.Highs.UnitTests;

/// <summary>Energy targets on single-block generators: fuel chains, timed fuels, and quality bands.</summary>
public class FactoryEnergyTests
{
    [Fact]
    public void EnergyTargetBurnsFuelThroughItsChain()
    {
        // Example 2 in miniature: logs pyrolyse into benzene, a gas turbine burns it, and the pyrolyse oven's own draw is netted out of the export.
        var graph = SolverGraph.Build(
            [Leaf("log", weight: 1)],
            [Recipe("pyro", inputs: [("log", 1)], outputs: ("benzene", 100, 1.0))]);
        var machines = Generators(
            "Gas Turbine Fuel",
            new FactoryMachineBlock("turbine-mv", 2, false, false, 2, 1, [], GeneratorEfficiency: 95, GeneratorEuT: 128, GeneratorAmps: 1),
            new FactoryFuel("Gas Turbine Fuel", "benzene", 1, 360, null, null));

        var plan = Solve(
            graph,
            Energy(128, generatorTier: 2),
            new Dictionary<string, (long, long, long)> { ["pyro"] = (100, 20, 1) },
            machines,
            garageTier: 2);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        var generator = Assert.Single(plan.Lines, l => l.MachineItemId == "turbine-mv");
        // 126 net EU/t per machine after the 2 EU/amp loss; the plan overshoots to cover the pyrolyse ovens' own draw.
        Assert.Equal(128, plan.ExportEuT - plan.DrawEuT, 1e-2);
        Assert.True(generator.RunsPerSecond > 128.0 / 126);
        Assert.True(Assert.Single(plan.Inflows, i => i.ItemId == "log").Rate > 0);
    }

    [Fact]
    public void TimedFuelBurnsAtItsLifetime()
    {
        var graph = SolverGraph.Build([Leaf("pellet", weight: 5)], []);
        var machines = Generators(
            "Rtg",
            new FactoryMachineBlock("rtg-hv", 3, false, false, 3, 1, [], GeneratorEfficiency: null, GeneratorEuT: 512, GeneratorAmps: 1),
            new FactoryFuel("Rtg", "pellet", 1, null, 480, 2000));

        var plan = Solve(graph, Energy(476), machines: machines, garageTier: 3);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        // 480 EU/t minus the 4 EU/amp HV loss nets 476: one machine, consuming its pellet over the 2,000-tick lifetime.
        var line = Assert.Single(plan.Lines);
        Assert.Equal(1, line.RunsPerSecond, 1e-3);
        Assert.Equal(0.01, Assert.Single(plan.Inflows).Rate, 1e-5);
    }

    [Fact]
    public void EnergyTierBandRejectsLowGenerators()
    {
        var graph = SolverGraph.Build([Leaf("fuel", weight: 1)], []);
        var machines = Generators(
            "Gen",
            new FactoryMachineBlock("gen-lv", 1, false, false, 1, 1, [], GeneratorEfficiency: 100, GeneratorEuT: 32, GeneratorAmps: 1),
            new FactoryFuel("Gen", "fuel", 1, 100, null, null));

        var plan = Solve(graph, Energy(512, generatorTier: 3), machines: machines, garageTier: 3);

        Assert.Equal(FactoryPlanStatus.Infeasible, plan.Status);
        Assert.Contains(FactoryWarning.NoGenerator(), plan.Warnings);
    }
}
