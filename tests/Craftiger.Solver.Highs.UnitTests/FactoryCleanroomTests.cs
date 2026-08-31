using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;
using static Craftiger.Solver.Highs.UnitTests.FactoryHarness;

namespace Craftiger.Solver.Highs.UnitTests;

/// <summary>Environment walls: cleanroom lines run inside one hosted, continuously drawing room; low-gravity lines wait for the rocket.</summary>
public class FactoryCleanroomTests
{
    private static readonly FactoryEnvironment Walls = new("clean", CleanroomEra: 3, LowGravityEra: 3);

    private static readonly Dictionary<string, (long, long, long)> Data = new() { ["etch"] = (100, 30, 1) };

    private static SolverGraph Graph(string flagged = "etch") =>
        SolverGraph.Build(
            [Leaf("dust", weight: 1)],
            [Recipe(flagged, tier: 1, machine: "Laser Engraver", inputs: [("dust", 1)], outputs: ("chip", 1, 1.0))]);

    [Fact]
    public void TheCleanroomHostsItsLinesAtAContinuousDraw()
    {
        // One run a second etches at 150 EU/t on five machines; the hosting room adds one machine and the HV-hatch draw of 16 EU/t.
        var plan = Solve(
            Graph(), Produce([("chip", 1)]), Data, garageTier: 3, cleanroom: ["etch"], environment: Walls);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        var host = Assert.Single(plan.Lines, line => line.RecipeId == FactoryEnvironment.CleanroomLineId);
        Assert.Equal("Cleanroom", host.Machine);
        Assert.Equal("clean", host.MachineItemId);
        Assert.Equal(1, host.BusyMachines, Tolerance);
        Assert.Equal(150 + 16, plan.DrawEuT, 0.5);
        Assert.Equal(5 + 1, plan.BusyMachines, 5e-3);
    }

    [Fact]
    public void ASubWallGarageCannotRunCleanroomLines()
    {
        // The recipe is voltage-legal at MV, but the garage's era never built a cleanroom.
        var plan = Solve(
            Graph(), Produce([("chip", 1)]), Data, garageTier: 2, cleanroom: ["etch"], environment: Walls);

        Assert.Equal(FactoryPlanStatus.Infeasible, plan.Status);
        Assert.Contains(FactoryWarning.UnreachableTarget("chip"), plan.Warnings);
    }

    [Fact]
    public void LowGravityWaitsForTheRocketEra()
    {
        var grounded = Solve(
            Graph(), Produce([("chip", 1)]), Data, garageTier: 2, lowGravity: ["etch"], environment: Walls);
        var lifted = Solve(
            Graph(), Produce([("chip", 1)]), Data, garageTier: 3, lowGravity: ["etch"], environment: Walls);

        Assert.Equal(FactoryPlanStatus.Infeasible, grounded.Status);
        Assert.Equal(FactoryPlanStatus.Solved, lifted.Status);
        // Low gravity is a place, not a machine: no hosting line and no extra draw.
        Assert.DoesNotContain(lifted.Lines, line => line.RecipeId == FactoryEnvironment.CleanroomLineId);
        Assert.Equal(150, lifted.DrawEuT, 0.5);
        Assert.Equal(5, lifted.BusyMachines, 5e-3);
    }
}
