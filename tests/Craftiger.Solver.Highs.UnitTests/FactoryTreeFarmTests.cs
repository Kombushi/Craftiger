using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;
using static Craftiger.Solver.Highs.UnitTests.FactoryHarness;

namespace Craftiger.Solver.Highs.UnitTests;

/// <summary>The Tree Growth Simulator: a fixed five-second run whose outputs, not its speed, grow with the hatch tier.</summary>
public class FactoryTreeFarmTests
{
    private const string Map = "Tree Growth Simulator";

    /// <summary>Forty logs per run at LV, the artifact's tool-multiplied amount; 30 EU/t is LV's practical voltage.</summary>
    private static readonly Dictionary<string, (long, long, long)> Data = new() { ["grow"] = (100, 30, 1) };

    private static SolverGraph Graph() =>
        SolverGraph.Build(
            [Leaf("log", weight: 1)],
            [Recipe("grow", tier: 1, machine: Map, outputs: ("log", 40, 1.0))]);

    private static FactoryMachineData Machines() =>
        new(
            new Dictionary<string, IReadOnlyList<FactoryMachineBlock>>
            {
                [Map] = [new FactoryMachineBlock("tgs", null, true, false, 0, 1, [])],
            },
            [], [], [], []);

    [Fact]
    public void TreeFarmOverclockMultipliesOutputsAtFixedDuration()
    {
        // Eight logs a second: at LV that is one busy machine at 30 EU/t; at HV each run yields 17/5 as much for 16x the power, so a third of a machine draws 141 EU/t.
        var thrifty = Solve(
            Graph(),
            Produce([("log", 8)], priority: [FactoryObjective.Resource, FactoryObjective.Energy, FactoryObjective.Machines]),
            Data, Machines(), garageTier: 3, treeFarms: ["grow"]);
        var compact = Solve(
            Graph(),
            Produce([("log", 8)], priority: [FactoryObjective.Resource, FactoryObjective.Machines, FactoryObjective.Energy]),
            Data, Machines(), garageTier: 3, treeFarms: ["grow"]);

        Assert.Equal(FactoryPlanStatus.Solved, thrifty.Status);
        var lv = Dominant(thrifty);
        Assert.Equal(0, lv.OcSteps);
        // The layer corridor leaves a sub-percent sliver on the other tiers.
        Assert.Equal(0.2, lv.RunsPerSecond, 1e-3);
        Assert.Equal(1, lv.BusyMachines, 5e-3);
        Assert.Equal(30, thrifty.DrawEuT, 0.2);

        Assert.Equal(FactoryPlanStatus.Solved, compact.Status);
        var hv = Dominant(compact);
        Assert.Equal(2, hv.OcSteps);
        Assert.Equal(8.0 / 136, hv.RunsPerSecond, 1e-4);
        Assert.Equal(8.0 / 136 * 5, hv.BusyMachines, 5e-4);
        Assert.Equal(8.0 / 136 * 100 * 480 / 20, compact.DrawEuT, 0.5);
        // The multiplied yield is what the plan reports, and it covers the target without surplus.
        var logs = Assert.Single(compact.Flows, f => f.ItemId == "log");
        Assert.Equal(8, logs.Produced, 1e-3);
        Assert.Equal(0, logs.Surplus, 1e-3);
        Assert.Equal(0, compact.PricedInflowCost, 1e-3);
        Assert.True(logs.AutoInfinite);
    }

    [Fact]
    public void TheAnonymousBlockKeepsTheTreeFarmLadder()
    {
        // Without a buildable block the map runs on the flagged fallback, still multiplying outputs rather than speed.
        var plan = Solve(
            Graph(),
            Produce([("log", 8)], priority: [FactoryObjective.Resource, FactoryObjective.Machines, FactoryObjective.Energy]),
            Data, garageTier: 3, treeFarms: ["grow"]);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        var line = Dominant(plan);
        Assert.Null(line.MachineItemId);
        Assert.True(line.Estimated);
        Assert.Equal(2, line.OcSteps);
        Assert.Equal(8.0 / 136 * 5, line.BusyMachines, 5e-4);
    }
}
