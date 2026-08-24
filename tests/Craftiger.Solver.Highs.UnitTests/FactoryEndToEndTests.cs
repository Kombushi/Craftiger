using Craftiger.Solver.Highs;
using Craftiger.Solver.Models;
using Craftiger.Solver.Services;

namespace Craftiger.Solver.Highs.UnitTests;

/// <summary>Factory solves running the real HiGHS adapter over hand-built fixtures.</summary>
public class FactoryEndToEndTests
{
    private const double Tolerance = 1e-6;

    private static readonly GarageRules Rules = new(
        AlwaysOwnedMachines: new HashSet<string> { "Crafting Table" },
        HeatExemptMachines: new HashSet<string>(),
        HeatBonusMachines: new HashSet<string>());

    private static SolverItem Leaf(string id, double? weight = null) =>
        new(id, "dust", null, weight, null);

    private static SolverRecipe Recipe(
        string id,
        (string ItemId, long Amount)[]? inputs = null,
        (string ItemId, long Amount)[][]? slots = null,
        int tier = 0,
        params (string ItemId, long Amount, double Chance)[] outputs)
    {
        var slotList = new List<SolverSlot>();
        foreach (var (itemId, amount) in inputs ?? [])
        {
            slotList.Add(new SolverSlot([new SolverStack(itemId, amount)]));
        }
        foreach (var alternatives in slots ?? [])
        {
            slotList.Add(new SolverSlot(alternatives.Select(a => new SolverStack(a.ItemId, a.Amount)).ToList()));
        }
        return new SolverRecipe(
            id, "Crafting Table", tier, null, null, slotList,
            outputs.Select(o => new SolverOutput(o.ItemId, o.Amount, o.Chance)).ToList());
    }

    private static FactoryPlan Solve(
        SolverGraph graph,
        FactoryRequest request,
        Dictionary<string, (long DurationTicks, long EuT, long Amps)>? data = null,
        FactoryMachineData? machines = null,
        int garageTier = 0,
        FactorySeedData? seeds = null)
    {
        var costSolver = new CostSolverService(
            new LeafWeightService(),
            new GarageLegalityService(Rules),
            new SolverPreferences(["ingot", "gem", "dust", "nugget", "dust_small", "dust_tiny"]));
        var service = new PipelineSolverService(
            new LeafWeightService(), new GarageLegalityService(Rules), costSolver, new HighsLinearProgramSolver());
        var garage = new Garage(garageTier, new Dictionary<string, int?>(), new HashSet<string>(), new Dictionary<string, int>());
        var weights = new WeightSettings(4, new Dictionary<string, double>());
        return service.Solve(
            graph,
            FactoryRecipeData.Build(graph.Index, data),
            machines ?? FactoryMachineData.Empty,
            seeds ?? FactorySeedData.Empty,
            costSolver.Solve(graph, garage, weights),
            garage,
            weights,
            request);
    }

    private static FactoryRequest Produce(
        (string ItemId, double Rate)[] targets,
        FactoryObjective[]? priority = null,
        Dictionary<string, string>? pins = null,
        bool mobFarms = false) =>
        new(
            targets.Select(t => new FactoryTarget(FactoryTargetKind.Produce, t.ItemId, t.Rate)).ToList(),
            priority ?? [],
            pins ?? new Dictionary<string, string>(),
            mobFarms);

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
    public void PinForcesTheRouteAndDiagnosesConflicts()
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

        // The layer tolerance legitimately leaves a sub-percent sliver on the losing route;
        // the winning route must carry effectively all of the flow.
        static void AssertDominant(FactoryPlan plan, string recipeId)
        {
            var total = plan.Lines.Sum(l => l.RunsPerSecond);
            var dominant = plan.Lines.MaxBy(l => l.RunsPerSecond)!;
            Assert.Equal(recipeId, dominant.RecipeId);
            Assert.True(dominant.RunsPerSecond >= total * 0.99);
        }

        AssertDominant(energyFirst, "slowCool");
        AssertDominant(machinesFirst, "fastHot");
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
        Assert.Equal(first.Lines, second.Lines);
        Assert.Equal(first.Inflows, second.Inflows);
        Assert.Equal(first.Flows, second.Flows);
    }

    [Fact]
    public void PriorityPicksTheOverclockLevel()
    {
        // Fewer overclocks halve power per step; more halve machine time. The priority order
        // decides, on the same block.
        var graph = SolverGraph.Build(
            [Leaf("ore")],
            [Recipe("smelt", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0))]);
        var machines = new FactoryMachineData(
            new Dictionary<string, IReadOnlyList<FactoryMachineBlock>>
            {
                ["Crafting Table"] = [new FactoryMachineBlock("mac-hv", 3, false, false, 3, 1, [])],
            },
            [],
            []);
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
            [],
            []);

        var plan = Solve(
            graph, Produce([("ingot", 1)]),
            new Dictionary<string, (long, long, long)> { ["smelt"] = (80, 4, 1) }, machines);

        var line = Assert.Single(plan.Lines);
        Assert.Equal("multi", line.MachineItemId);
        Assert.Equal(4, line.Parallels);
        Assert.Equal(1, line.BusyMachines, 5);
        Assert.Equal(1, plan.BusyMachines, 5);
    }

    [Fact]
    public void EnergyTargetBurnsFuelThroughItsChain()
    {
        // Example 2 in miniature: logs pyrolyse into benzene, a gas turbine burns it, and the
        // pyrolyse oven's own draw is netted out of the export.
        var graph = SolverGraph.Build(
            [Leaf("log", weight: 1)],
            [Recipe("pyro", inputs: [("log", 1)], outputs: ("benzene", 100, 1.0))]);
        var machines = new FactoryMachineData(
            new Dictionary<string, IReadOnlyList<FactoryMachineBlock>>
            {
                ["Gas Turbine Fuel"] =
                [
                    new FactoryMachineBlock("turbine-mv", 2, false, false, 2, 1, [],
                        GeneratorEfficiency: 95, GeneratorEuT: 128, GeneratorAmps: 1),
                ],
            },
            [],
            [new FactoryFuel("Gas Turbine Fuel", "benzene", 1, 360, null, null)]);

        var plan = Solve(
            graph,
            new FactoryRequest(
                [new FactoryTarget(FactoryTargetKind.Energy, null, 128, GeneratorTier: 2)],
                [], new Dictionary<string, string>()),
            new Dictionary<string, (long, long, long)> { ["pyro"] = (100, 20, 1) },
            machines,
            garageTier: 2);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        var generator = Assert.Single(plan.Lines, l => l.MachineItemId == "turbine-mv");
        // 126 net EU/t per machine after the 2 EU/amp loss; the plan overshoots to cover the
        // pyrolyse ovens' own draw.
        Assert.Equal(128, plan.ExportEuT - plan.DrawEuT, 1e-2);
        Assert.True(generator.RunsPerSecond > 128.0 / 126);
        Assert.True(Assert.Single(plan.Inflows, i => i.ItemId == "log").Rate > 0);
    }

    [Fact]
    public void TimedFuelBurnsAtItsLifetime()
    {
        var graph = SolverGraph.Build([Leaf("pellet", weight: 5)], []);
        var machines = new FactoryMachineData(
            new Dictionary<string, IReadOnlyList<FactoryMachineBlock>>
            {
                ["Rtg"] =
                [
                    new FactoryMachineBlock("rtg-hv", 3, false, false, 3, 1, [],
                        GeneratorEfficiency: null, GeneratorEuT: 512, GeneratorAmps: 1),
                ],
            },
            [],
            [new FactoryFuel("Rtg", "pellet", 1, null, 480, 2000)]);

        var plan = Solve(
            graph,
            new FactoryRequest(
                [new FactoryTarget(FactoryTargetKind.Energy, null, 476)],
                [], new Dictionary<string, string>()),
            machines: machines,
            garageTier: 3);

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        // 480 EU/t minus the 4 EU/amp HV loss nets 476: one machine, consuming its pellet
        // over the 2,000-tick lifetime.
        var line = Assert.Single(plan.Lines);
        Assert.Equal(1, line.RunsPerSecond, 1e-3);
        Assert.Equal(0.01, Assert.Single(plan.Inflows).Rate, 1e-5);
    }

    [Fact]
    public void EnergyTierBandRejectsLowGenerators()
    {
        var graph = SolverGraph.Build([Leaf("fuel", weight: 1)], []);
        var machines = new FactoryMachineData(
            new Dictionary<string, IReadOnlyList<FactoryMachineBlock>>
            {
                ["Gen"] =
                [
                    new FactoryMachineBlock("gen-lv", 1, false, false, 1, 1, [],
                        GeneratorEfficiency: 100, GeneratorEuT: 32, GeneratorAmps: 1),
                ],
            },
            [],
            [new FactoryFuel("Gen", "fuel", 1, 100, null, null)]);

        var plan = Solve(
            graph,
            new FactoryRequest(
                [new FactoryTarget(FactoryTargetKind.Energy, null, 512, GeneratorTier: 3)],
                [], new Dictionary<string, string>()),
            machines: machines,
            garageTier: 3);

        Assert.Equal(FactoryPlanStatus.Infeasible, plan.Status);
        Assert.Contains(new FactoryWarning("no_generator", ""), plan.Warnings);
    }

    [Fact]
    public void ConsumeTargetProcessesTheSupply()
    {
        // Example 3 in miniature: supplied raw ore macerates into dusts with a chanced
        // byproduct; everything supplied is absorbed and the outputs land as surplus.
        var graph = SolverGraph.Build(
            [Leaf("dust", weight: 2), Leaf("bonus", weight: 3)],
            [Recipe("macerate", inputs: [("rawore", 1)], outputs: [("dust", 2, 1.0), ("bonus", 1, 0.5)])]);

        var plan = Solve(
            graph,
            new FactoryRequest(
                [new FactoryTarget(FactoryTargetKind.Consume, "rawore", 2)],
                [], new Dictionary<string, string>()));

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        Assert.DoesNotContain(plan.Warnings, w => w.Kind == "consume_shortfall");
        Assert.Equal(2, Assert.Single(plan.Lines).RunsPerSecond, Tolerance);
        var ore = Assert.Single(plan.Flows, f => f.ItemId == "rawore");
        Assert.Equal(2, ore.Supplied, Tolerance);
        Assert.Equal(2, ore.Consumed, Tolerance);
        Assert.Equal(4, Assert.Single(plan.Flows, f => f.ItemId == "dust").Surplus, Tolerance);
        Assert.Equal(1, Assert.Single(plan.Flows, f => f.ItemId == "bonus").Surplus, Tolerance);
    }

    [Fact]
    public void ConsumeShortfallNamesTheItem()
    {
        // The only consumer needs an unmakeable co-input, so nothing can be absorbed — the
        // plan still solves and reports the shortfall instead of a bare infeasibility.
        var graph = SolverGraph.Build(
            [Leaf("dust", weight: 1)],
            [Recipe("wash", inputs: [("rawore", 1), ("mystery", 1)], outputs: ("dust", 1, 1.0))]);

        var plan = Solve(
            graph,
            new FactoryRequest(
                [new FactoryTarget(FactoryTargetKind.Consume, "rawore", 2)],
                [], new Dictionary<string, string>()));

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        Assert.Contains(new FactoryWarning("consume_shortfall", "rawore"), plan.Warnings);
        Assert.Empty(plan.Lines);
    }

    [Fact]
    public void AutoInfiniteBadgesFollowTheGarage()
    {
        // Oxygen is auto-infinite only while the tier-1 water chain is legal; without it the
        // plan buys oxygen at its weight and nothing downstream badges.
        var graph = SolverGraph.Build(
            [Leaf("water", weight: 2), Leaf("oxygen", weight: 1)],
            [
                Recipe("electrolyze", inputs: [("water", 1)], tier: 1, outputs: ("oxygen", 1, 1.0)),
                Recipe("bottle", inputs: [("oxygen", 1)], outputs: ("gas", 1, 1.0)),
            ]);
        var seeds = new FactorySeedData(new Dictionary<string, string> { ["water"] = "WORLD" });

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
        var seeds = new FactorySeedData(new Dictionary<string, string> { ["bone"] = "MOB" });

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
        // The index carries neither catalysts nor EU as slots, so a recipe needing only
        // those has zero slots and seeds the fixpoint by itself.
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

    [Fact]
    public void ElasticDiagnosisNamesTheMissingItem()
    {
        // The pinned route needs an unmakeable input; the elastic re-solve keeps the cheaper
        // slack — the missing input, not the target — and names it.
        var graph = SolverGraph.Build(
            [],
            [Recipe("make", inputs: [("x", 1)], outputs: ("t", 2, 1.0))]);

        var plan = Solve(
            graph,
            Produce([("t", 4)], pins: new Dictionary<string, string> { ["t"] = "make" }));

        Assert.Equal(FactoryPlanStatus.Infeasible, plan.Status);
        Assert.Contains(new FactoryWarning("infeasible_item", "x"), plan.Warnings);
        Assert.DoesNotContain(plan.Warnings, w => w.Kind == "infeasible");
        Assert.DoesNotContain(new FactoryWarning("infeasible_item", "t"), plan.Warnings);
    }

    [Fact]
    public void PinConflictIsDiagnosed()
    {
        // The pinned recipe is garage-illegal and the pin removes the only legal route:
        // lifting the pins restores feasibility, so the pin is named instead of the item.
        var graph = SolverGraph.Build(
            [Leaf("l", weight: 1), Leaf("m", weight: 1)],
            [
                Recipe("alpha", inputs: [("l", 1)], tier: 1, outputs: ("t", 1, 1.0)),
                Recipe("beta", inputs: [("m", 1)], outputs: ("t", 1, 1.0)),
            ]);

        var plan = Solve(
            graph,
            Produce([("t", 1)], pins: new Dictionary<string, string> { ["t"] = "alpha" }));

        Assert.Equal(FactoryPlanStatus.Infeasible, plan.Status);
        Assert.Contains(new FactoryWarning("pin_illegal", "t"), plan.Warnings);
        Assert.Contains(new FactoryWarning("pin_conflict", "t"), plan.Warnings);
        Assert.DoesNotContain(plan.Warnings, w => w.Kind.StartsWith("infeasible"));
    }
}
