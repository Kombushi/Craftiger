using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;
using static Craftiger.Solver.Highs.UnitTests.FactoryHarness;

namespace Craftiger.Solver.Highs.UnitTests;

/// <summary>Auto-infinite seeds: weightless purchases and per-solve badges that follow the garage.</summary>
public class FactorySeedTests
{
    [Fact]
    public void AutoInfiniteBadgesFollowTheGarage()
    {
        // Oxygen is auto-infinite only while the tier-1 water chain is legal; without it the plan buys oxygen at its weight and nothing downstream badges.
        var graph = SolverGraph.Build(
            [Leaf("water", weight: 2), Leaf("oxygen", weight: 1)],
            [
                Recipe("electrolyze", inputs: [("water", 1)], tier: 1, outputs: ("oxygen", 1, 1.0)),
                Recipe("bottle", inputs: [("oxygen", 1)], outputs: ("gas", 1, 1.0)),
            ]);
        var seeds = new FactorySeedData(new Dictionary<string, SeedKind> { ["water"] = SeedKind.World });

        var chained = Solve(graph, Produce([("gas", 1)]), garageTier: 1, seeds: seeds);
        Assert.Equal(FactoryPlanStatus.Solved, chained.Status);
        var water = Assert.Single(chained.Inflows, i => i.ItemId == "water");
        Assert.Equal(0, water.Weight);
        Assert.True(water.AutoInfinite);
        Assert.Equal(0, chained.PricedInflowCost, Tolerance);
        Assert.True(Assert.Single(chained.Flows, f => f.ItemId == "oxygen").AutoInfinite);
        Assert.True(Assert.Single(chained.Flows, f => f.ItemId == "gas").AutoInfinite);

        var bought = Solve(graph, Produce([("gas", 1)]), seeds: seeds);
        Assert.Equal(FactoryPlanStatus.Solved, bought.Status);
        var oxygen = Assert.Single(bought.Inflows, i => i.ItemId == "oxygen");
        Assert.Equal(1, oxygen.Weight);
        Assert.False(oxygen.AutoInfinite);
        Assert.False(Assert.Single(bought.Flows, f => f.ItemId == "gas").AutoInfinite);
    }

    [Fact]
    public void MobFarmToggleAddsTheMobSeeds()
    {
        var graph = SolverGraph.Build(
            [Leaf("bone", weight: 3)],
            [Recipe("grind", inputs: [("bone", 1)], outputs: ("meal", 3, 1.0))]);
        var seeds = new FactorySeedData(new Dictionary<string, SeedKind> { ["bone"] = SeedKind.Mob });

        var farmless = Solve(graph, Produce([("meal", 3)]), seeds: seeds);
        var farmed = Solve(graph, Produce([("meal", 3)], mobFarms: true), seeds: seeds);

        var priced = Assert.Single(farmless.Inflows);
        Assert.Equal(3, priced.Weight);
        Assert.False(priced.AutoInfinite);
        Assert.Equal(3, farmless.PricedInflowCost, Tolerance);
        var free = Assert.Single(farmed.Inflows);
        Assert.Equal(0, free.Weight);
        Assert.True(free.AutoInfinite);
        Assert.Equal(0, farmed.PricedInflowCost, Tolerance);
        Assert.True(Assert.Single(farmed.Flows, f => f.ItemId == "meal").AutoInfinite);
    }

    [Fact]
    public void CatalystOnlyRecipesQualifyAsAutoInfinite()
    {
        // The index carries neither catalysts nor EU as slots, so a recipe needing only those has zero slots and seeds the fixpoint by itself.
        var graph = SolverGraph.Build(
            [],
            [
                Recipe("sprout", outputs: ("seedling", 1, 1.0)),
                Recipe("grow", inputs: [("seedling", 1)], outputs: ("wood", 1, 1.0)),
            ]);

        var plan = Solve(graph, Produce([("wood", 1)]));

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        Assert.Empty(plan.Inflows);
        Assert.True(Assert.Single(plan.Flows, f => f.ItemId == "seedling").AutoInfinite);
        Assert.True(Assert.Single(plan.Flows, f => f.ItemId == "wood").AutoInfinite);
    }
}
