using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;
using static Craftiger.Solver.Highs.UnitTests.FactoryHarness;

namespace Craftiger.Solver.Highs.UnitTests;

/// <summary>The steam carrier: boilers make it, steam machines drink it in place of EU, turbines convert it.</summary>
public class FactorySteamTests
{
    [Fact]
    public void BoilerSteamAndSteamMachineBalanceAsACarrier()
    {
        // A boiler line boils fuel and water into steam, a steam machine drinks it in place of EU, and the carrier balances as ordinary item flow.
        // The ore is dug for free; its leaf weight only keeps the chain priceable, since a dig consuming nothing never prices.
        var graph = SolverGraph.Build(
            [Leaf("fuelwood", weight: 1), Leaf("water", weight: 0), Leaf("ore", weight: 1)],
            [
                Recipe("boil", inputs: [("fuelwood", 1), ("water", 240)], machine: "Large Bronze Boiler",
                    outputs: ("f~IC2~ic2steam", 38400, 1.0)),
                Recipe("grind", inputs: [("ore", 1)], tier: 1, machine: "Mill",
                    outputs: ("dust", 1, 1.0)),
                Recipe("dig", machine: "Mining", outputs: ("ore", 1, 1.0)),
            ]);
        var machines = new FactoryMachineData(
            new Dictionary<string, IReadOnlyList<FactoryMachineBlock>>
            {
                ["Large Bronze Boiler"] = [new FactoryMachineBlock("boiler", null, true, false, 0, 1, [])],
                ["Mill"] =
                [
                    // A GT++-shape steam multi: eight parallels at 125 % speed and 62.5 % steam use — the machines layer is where steam legitimately wins.
                    new FactoryMachineBlock("steam-mill", null, true, true, 0, 8,
                    [
                        new FactoryMachineBonus("PARALLEL", 8, false, null),
                        new FactoryMachineBonus("SPEED", 125, false, null),
                        new FactoryMachineBonus("EU_DISCOUNT", 62.5, false, null),
                    ]),
                ],
            },
            [], [], [], []);

        var plan = Solve(
            graph,
            Produce([("dust", 1)], priority: [FactoryObjective.Machines, FactoryObjective.Resource, FactoryObjective.Energy]),
            new Dictionary<string, (long, long, long)> { ["boil"] = (40, 0, 1), ["grind"] = (100, 30, 1) },
            machines,
            garageTier: 1);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        // The corridor leaves an electric sliver; the steam multi carries the flow.
        var grind = plan.Lines.Where(l => l.RecipeId == "grind").MaxBy(l => l.RunsPerSecond)!;
        Assert.Equal("steam-mill", grind.MachineItemId);
        Assert.Equal(1, grind.RunsPerSecond, 1e-3);
        // 4 L per recipe EU at 62.5 % usage: 7,500 L/s, boiled at 38,400 L per 2 s run.
        var steam = Assert.Single(plan.Flows, f => f.ItemId == "f~IC2~ic2steam");
        Assert.Equal(7500, steam.Consumed, 10.0);
        Assert.Equal(0.1953, plan.Lines.Single(l => l.RecipeId == "boil").RunsPerSecond, 1e-3);
        Assert.Equal(46.875, Assert.Single(plan.Inflows, i => i.ItemId == "water").Rate, 0.5);
        // The bulk of the work draws no EU; only the corridor sliver's electric run does.
        Assert.True(plan.DrawEuT < 1);
    }

    [Fact]
    public void SingleSteamTurbineConvertsAtItsEfficiency()
    {
        // 32 EU/t out at 85.714 %: 1,493 L/s of steam in, one Enet EU per amp lost.
        var graph = SolverGraph.Build([Leaf("f~IC2~ic2steam", weight: 1)], []);
        var machines = Generators(
            "Steam Turbine",
            new FactoryMachineBlock("basic-st", 1, false, false, 0, 1, [], 85.714285714285708, 32, 1),
            new FactoryFuel("Steam Turbine", "f~IC2~ic2steam", 1, 0.5, null, null));

        var plan = Solve(graph, Energy(31), machines: machines);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        Assert.Equal(1, Assert.Single(plan.Lines).RunsPerSecond, 1e-4);
        Assert.Equal(1493.33, Assert.Single(plan.Inflows).Rate, 0.01);
    }

    [Fact]
    public void LargeSteamTurbineReturnsDistilledWater()
    {
        var graph = SolverGraph.Build(
            [Leaf("f~IC2~ic2steam", weight: 1), Leaf("f~IC2~ic2distilledwater", weight: 1), Leaf("rotor-a", weight: 5)],
            []);
        var machines = new FactoryMachineData(
            new Dictionary<string, IReadOnlyList<FactoryMachineBlock>>
            {
                ["Large Steam Turbine"] =
                [
                    new FactoryMachineBlock("lst", null, true, false, 0, 1, [], RotorFuel: "STEAM"),
                ],
            },
            [],
            [new FactoryFuel("Large Steam Turbine", "f~IC2~ic2steam", 1, 0.5, null, null)],
            [new FactoryRotorStats("rotor-a", "STEAM", 0.75, 0.4, 3600, 0, 2700, 0)],
            [new FactoryDynamo("dyn-hv", 0, 512, 4)]);

        var plan = Solve(graph, Energy(2032), machines: machines);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        // 3,600 EU/t of flow at 0.5 EU/L: 144,000 L/s of steam, one liter of distilled water back per 160.
        var line = plan.Lines.MaxBy(l => l.RunsPerSecond)!;
        Assert.Contains("|rotor-a|tight", line.RecipeId);
        var distilled = Assert.Single(plan.Flows, f => f.ItemId == "f~IC2~ic2distilledwater");
        Assert.Equal(900, distilled.Produced, 2);
        Assert.Equal(144000, Assert.Single(plan.Inflows, i => i.ItemId == "f~IC2~ic2steam").Rate, 200.0);
    }
}
