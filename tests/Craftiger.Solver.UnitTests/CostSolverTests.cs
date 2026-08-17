namespace Craftiger.Solver.UnitTests;

public sealed class CostSolverTests
{
    [Fact]
    public void IngotTierStepsMultiplyByFour()
    {
        var graph = Fx.Graph([Fx.Leaf("bronze", tier: 0), Fx.Leaf("aluminium", tier: 1)]);

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal(4, table.Costs["bronze"]);
        Assert.Equal(16, table.Costs["aluminium"]);
    }

    [Fact]
    public void ARecipeSumsItsInputCosts()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("iron", tier: 0), Fx.Leaf("coal", tier: 0)],
            Fx.Recipe("r", inputs: [("iron", 1), ("coal", 1)], outputs: ("steel", 1, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal(8, table.Costs["steel"]);
        Assert.Equal("r", table.BestRecipes["steel"].Id);
    }

    [Fact]
    public void ChancedOutputsDivideByTheirChance()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("iron", tier: 0)],
            Fx.Recipe("sure", inputs: [("iron", 1)], outputs: ("plate", 1, 1.0)),
            Fx.Recipe("maybe", inputs: [("iron", 1)], outputs: ("shard", 1, 0.9)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal(table.Costs["plate"] / 0.9, table.Costs["shard"], 9);
    }

    [Fact]
    public void ACycleNeverUndercutsItself()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ingot", tier: 0)],
            Fx.Recipe("pack", inputs: [("ingot", 9)], outputs: ("block", 1, 1.0)),
            Fx.Recipe("unpack", inputs: [("block", 1)], outputs: ("ingot", 9, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal(4, table.Costs["ingot"]);
        Assert.Equal(36, table.Costs["block"]);
        Assert.False(table.BestRecipes.ContainsKey("ingot"));
        Assert.True(table.Converged);
    }

    [Fact]
    public void NoIngotPricesFromItsNuggets()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ingot", tier: 0), Fx.Leaf("nugget", parent: "ingot", divisor: 9)],
            Fx.Recipe("join", inputs: [("nugget", 9)], outputs: ("ingot", 1, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal(4, table.Costs["ingot"]);
        Assert.Equal(4.0 / 9, table.Costs["nugget"], 9);
        Assert.False(table.BestRecipes.ContainsKey("ingot"));
    }

    [Fact]
    public void ASlotWithAlternativesPricesFromTheCheapest()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("copper", tier: 0), Fx.Leaf("silver", tier: 1)],
            Fx.Recipe("r", slots: [[("copper", 1), ("silver", 1)]], outputs: ("wire", 1, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal(4, table.Costs["wire"]);
    }

    [Fact]
    public void AWeightChangeCanFlipTheCheapestAlternative()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("copper", tier: 0), Fx.Leaf("silver", tier: 1)],
            Fx.Recipe("r", slots: [[("copper", 1), ("silver", 1)]], outputs: ("wire", 1, 1.0)));

        var table = Fx.Solver().Solve(
            graph, Fx.Garage(), Fx.Weights(items: new() { ["silver"] = 1 }));

        Assert.Equal(1, table.Costs["wire"]);
    }

    [Fact]
    public void AlternativesCarryTheirOwnAmounts()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("water", weight: 1), Fx.Leaf("oxygen", weight: 2)],
            Fx.Recipe("r", slots: [[("water", 144), ("oxygen", 10)]], outputs: ("gel", 1, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal(20, table.Costs["gel"]);
    }

    [Fact]
    public void RaisingTheGarageNeverRaisesACost()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore", tier: 0)],
            Fx.Recipe("crude", machine: "Furnace", inputs: [("ore", 4)], outputs: ("metal", 1, 1.0)),
            Fx.Recipe("fine", machine: "Macerator", tier: 2, inputs: [("ore", 1)], outputs: ("metal", 1, 1.0)));

        var costs = Enumerable.Range(0, 4)
            .Select(tier => Fx.Solver()
                .Solve(graph, Fx.Garage(defaultTier: tier), Fx.Weights())
                .Costs["metal"])
            .ToList();

        Assert.Equal(costs.OrderDescending(), costs);
        Assert.Equal(16, costs[0]);
        Assert.Equal(4, costs[2]);
    }

    [Fact]
    public void UnreachableItemsHaveNoCost()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore", tier: 0)],
            Fx.Recipe("r", machine: "Extruder", inputs: [("ore", 1)], outputs: ("rod", 1, 1.0)));
        var garage = Fx.Garage(defaultTier: 14, tiers: new() { ["Extruder"] = null });

        var table = Fx.Solver().Solve(graph, garage, Fx.Weights());

        Assert.False(table.Costs.ContainsKey("rod"));
    }

    [Fact]
    public void ChancedTwinRowsPriceFromTheBestRow()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore", tier: 0)],
            Fx.Recipe("r", inputs: [("ore", 1)],
                outputs: [("dust", 1, 1.0), ("dust", 1, 0.5)]));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal(4, table.Costs["dust"]);
    }

    [Fact]
    public void CandidatesForUnreachableInputsAreInfinite()
    {
        var recipe = Fx.Recipe("r", inputs: [("ghost", 1)], outputs: ("thing", 1, 1.0));
        var graph = Fx.Graph([], recipe);

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.True(double.IsPositiveInfinity(Fx.Solver().Candidate(recipe, "thing", table.Costs)));
    }

    [Fact]
    public void DustsLoseExactTiesToSolidForms()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("steelDust", tier: 0, leafClass: "dust"), Fx.Leaf("steelIngot", tier: 0)],
            Fx.Recipe("fromDust", inputs: [("steelDust", 1)], outputs: ("molten", 144, 1.0)),
            Fx.Recipe("fromIngot", inputs: [("steelIngot", 1)], outputs: ("molten", 144, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal("fromIngot", table.BestRecipes["molten"].Id);
        Assert.Equal(4.0 / 144, table.Costs["molten"], 6);
    }

    [Fact]
    public void ARealPriceGapStillBeatsTheFormPreference()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("cheapDust", tier: 0, leafClass: "dust"), Fx.Leaf("dearIngot", tier: 1)],
            Fx.Recipe("fromDust", inputs: [("cheapDust", 1)], outputs: ("molten", 144, 1.0)),
            Fx.Recipe("fromIngot", inputs: [("dearIngot", 1)], outputs: ("molten", 144, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal("fromDust", table.BestRecipes["molten"].Id);
    }

    [Fact]
    public void TheFormRankingPrefersDustOverNugget()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("chip", tier: 0, leafClass: "nugget"), Fx.Leaf("raw", tier: 0, leafClass: "dust")],
            Fx.Recipe("fromChip", inputs: [("chip", 9)], outputs: ("molten", 9, 1.0)),
            Fx.Recipe("fromDust", inputs: [("raw", 9)], outputs: ("molten", 9, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal("fromDust", table.BestRecipes["molten"].Id);
    }

    [Fact]
    public void AMeltTieSkipsTheDetourThroughARod()
    {
        // Rodding is mass-conserving, so melting the rod ties melting the ingot exactly;
        // the shallower route must win even though the detour set the cost first.
        var graph = Fx.Graph(
            [Fx.Leaf("steel", tier: 0)],
            Fx.Recipe("rodding", inputs: [("steel", 1)], outputs: ("rod", 2, 1.0)),
            Fx.Recipe("meltRod", inputs: [("rod", 2)], outputs: ("molten", 144, 1.0)),
            Fx.Recipe("meltIngot", inputs: [("steel", 1)], outputs: ("molten", 144, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal("meltIngot", table.BestRecipes["molten"].Id);
        Assert.Equal(4.0 / 144, table.Costs["molten"], 9);
    }

    [Fact]
    public void ATieBetweenLeavesLandsOnTheLighterOne()
    {
        // The polarizer undercuts the magnetic leaf to the plain price, so both melts tie
        // on cost; the leaf with the lower era weight is the more basic material and wins.
        var graph = Fx.Graph(
            [Fx.Leaf("steel", tier: 0), Fx.Leaf("magnetic", tier: 1)],
            Fx.Recipe("polarize", inputs: [("steel", 1)], outputs: ("magnetic", 1, 1.0)),
            Fx.Recipe("meltMagnetic", inputs: [("magnetic", 1)], outputs: ("molten", 144, 1.0)),
            Fx.Recipe("meltPlain", inputs: [("steel", 1)], outputs: ("molten", 144, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal("meltPlain", table.BestRecipes["molten"].Id);
        Assert.Equal(4.0 / 144, table.Costs["molten"], 9);
    }

    [Fact]
    public void AFormRerouteNeverClosesACycle()
    {
        // "viaMid" ties with the dust route but its input chain leads back through the metal,
        // so the reroute must be refused and the dust route kept.
        var graph = Fx.Graph(
            [Fx.Leaf("dust", tier: 0, leafClass: "dust")],
            Fx.Recipe("fromDust", inputs: [("dust", 1)], outputs: ("metal", 1, 1.0)),
            Fx.Recipe("mid", inputs: [("metal", 1)], outputs: ("midItem", 1, 1.0)),
            Fx.Recipe("viaMid", inputs: [("midItem", 1)], outputs: ("metal", 1, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal("fromDust", table.BestRecipes["metal"].Id);
        Assert.Equal(4, table.Costs["metal"]);
    }
}
