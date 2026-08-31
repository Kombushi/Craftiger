using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;
using static Craftiger.Solver.Highs.UnitTests.FactoryHarness;

namespace Craftiger.Solver.Highs.UnitTests;

/// <summary>Farm rows never climb a ladder; mob lines wait for the toggle and quarter their duration to the crusher's floor.</summary>
public class FactoryFarmTests
{
    private static SolverGraph MobGraph() =>
        SolverGraph.Build(
            [Leaf("pearl", weight: 4)],
            [Recipe("eec~mob", tier: 4, machine: "Extreme Entity Crusher", scope: RecipeScope.FactoryMob, outputs: ("pearl", 1, 0.5))]);

    private static readonly Dictionary<string, (long, long, long)> MobData = new() { ["eec~mob"] = (55, 1920, 1) };

    private static readonly Dictionary<string, OverclockMode> MobModes = new() { ["eec~mob"] = OverclockMode.EntityCrusher };

    [Fact]
    public void AMobLineWaitsForTheToggle()
    {
        var off = Solve(MobGraph(), Produce([("pearl", 0.1)]), MobData, garageTier: 4, overclocks: MobModes);
        var on = Solve(MobGraph(), Produce([("pearl", 0.1)], mobFarms: true), MobData, garageTier: 4, overclocks: MobModes);

        // Off, the pearl is only bought; on, the crusher derives it free of purchases and marks it infinite.
        Assert.Equal(FactoryPlanStatus.Solved, off.Status);
        Assert.Empty(off.Lines);
        Assert.Equal(0.4, off.PricedInflowCost, 1e-6);

        Assert.Equal(FactoryPlanStatus.Solved, on.Status);
        var line = Assert.Single(on.Lines);
        Assert.Equal("eec~mob", line.RecipeId);
        Assert.Equal(0, on.PricedInflowCost, 1e-6);
        var pearl = Assert.Single(on.Flows, flow => flow.ItemId == "pearl");
        Assert.True(pearl.AutoInfinite);
    }

    [Fact]
    public void TheCrusherLadderQuartersToTheFloorThenMultiplies()
    {
        // At two steps above EV the 2.75-second kill cannot quarter under the floor, so both steps quadruple outputs at sixteen-fold power.
        var plan = Solve(
            MobGraph(),
            Produce([("pearl", 0.1)], priority: [FactoryObjective.Resource, FactoryObjective.Machines, FactoryObjective.Energy], mobFarms: true),
            MobData, garageTier: 6, overclocks: MobModes);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        var line = Dominant(plan);
        Assert.Equal(2, line.OcSteps);
        // 0.1 pearls at chance 0.5 and x16 outputs need 1/80 runs a second.
        Assert.Equal(0.1 / (0.5 * 16), line.RunsPerSecond, 1e-6);
        Assert.Equal(0.1 / (0.5 * 16) * 2.75, line.BusyMachines, 1e-6);
        // Sixteen-fold power on the flat 1920 EU/t base, scaled by machine time.
        Assert.Equal(0.1 / (0.5 * 16) * 55 * 1920 * 16 / 20, plan.DrawEuT, 0.5);
    }

    [Fact]
    public void ABredRowWaitsForItsOwnToggle()
    {
        var graph = SolverGraph.Build(
            [Leaf("water", weight: 0.001), Leaf("berry", weight: 2)],
            [
                Recipe("farm~cm1", tier: 1, machine: "Crop Manager", scope: RecipeScope.Factory, inputs: [("water", 10)], outputs: ("berry", 8, 1.0)),
                Recipe("farm~cm1~b", tier: 1, machine: "Crop Manager", scope: RecipeScope.FactoryBred, inputs: [("water", 10)], outputs: ("berry", 40, 1.0)),
            ]);
        var data = new Dictionary<string, (long, long, long)> { ["farm~cm1"] = (200, 0, 1), ["farm~cm1~b"] = (200, 0, 1) };
        var overclocks = new Dictionary<string, OverclockMode> { ["farm~cm1"] = OverclockMode.Fixed, ["farm~cm1~b"] = OverclockMode.Fixed };

        var off = Solve(graph, Produce([("berry", 1)]), data, garageTier: 5, overclocks: overclocks);
        var on = Solve(graph, Produce([("berry", 1)], bredSeeds: true), data, garageTier: 5, overclocks: overclocks);

        Assert.Equal("farm~cm1", Dominant(off).RecipeId);
        // Bred seeds cost nothing per run, so once admitted the richer row simply wins.
        Assert.Equal("farm~cm1~b", Dominant(on).RecipeId);
    }

    [Fact]
    public void AnOverclockedBuildClimbsTheStandardLadder()
    {
        // The ~oc row keeps the standard ladder: two steps above its tier it halves duration twice at sixteen-fold power.
        var graph = SolverGraph.Build(
            [Leaf("water", weight: 0.001), Leaf("berry", weight: 2)],
            [Recipe("farm~if3~oc", tier: 3, machine: "Industrial Farm", scope: RecipeScope.Factory, inputs: [("water", 10)], outputs: ("berry", 8, 1.0))]);
        var plan = Solve(
            graph,
            Produce([("berry", 1)], priority: [FactoryObjective.Resource, FactoryObjective.Machines, FactoryObjective.Energy]),
            new Dictionary<string, (long, long, long)> { ["farm~if3~oc"] = (200, 480, 1) },
            garageTier: 5);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        // The corridor's losing-step sliver shifts totals below a tenth of a percent.
        var line = Dominant(plan);
        Assert.Equal(2, line.OcSteps);
        Assert.Equal(1.0 / 8, line.RunsPerSecond, 1.0 / 8 * 2e-3);
        // Ten seconds base run at two halvings holds a quarter of the machines a fixed row would.
        Assert.Equal(1.0 / 8 * 2.5, plan.BusyMachines, 1.0 / 8 * 2.5 * 2e-3);
        Assert.Equal(1.0 / 8 * 200 * 480 * 4 / 20, plan.DrawEuT, 1.0 / 8 * 200 * 480 * 4 / 20 * 2e-3);
    }

    [Fact]
    public void AFixedRowNeverOverclocks()
    {
        // A crop row is exact for its tier: even chasing machines, no step is taken.
        var graph = SolverGraph.Build(
            [Leaf("water", weight: 0.001), Leaf("berry", weight: 2)],
            [Recipe("farm~cm1", tier: 1, machine: "Crop Manager", scope: RecipeScope.Factory, inputs: [("water", 10)], outputs: ("berry", 8, 1.0))]);
        var plan = Solve(
            graph,
            Produce([("berry", 1)], priority: [FactoryObjective.Resource, FactoryObjective.Machines, FactoryObjective.Energy]),
            new Dictionary<string, (long, long, long)> { ["farm~cm1"] = (200, 0, 1) },
            garageTier: 5, overclocks: new Dictionary<string, OverclockMode> { ["farm~cm1"] = OverclockMode.Fixed });

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        var line = Dominant(plan);
        Assert.Equal(0, line.OcSteps);
        Assert.Equal(1.0 / 8, line.RunsPerSecond, 1e-6);
        Assert.Equal(10.0 / 8, plan.BusyMachines, 1e-4);
        Assert.Equal(0, plan.DrawEuT, 1e-6);
    }
}
