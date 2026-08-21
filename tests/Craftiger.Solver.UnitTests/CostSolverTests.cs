using Craftiger.Solver.Models;
namespace Craftiger.Solver.UnitTests;

public sealed class CostSolverTests
{
    /// <summary>The priced cost of an item; a test asking for an unpriced one has failed already.</summary>
    private static double Cost(CostTable table, string itemId) =>
        table.Cost(itemId) ?? throw new KeyNotFoundException(itemId);

    [Fact]
    public void IngotTierStepsMultiplyByFour()
    {
        var graph = Fx.Graph([Fx.Leaf("bronze", tier: 0), Fx.Leaf("aluminium", tier: 1)]);

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal(4, Cost(table, "bronze"));
        Assert.Equal(16, Cost(table, "aluminium"));
    }

    [Fact]
    public void ARecipeSumsItsInputCosts()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("iron", tier: 0), Fx.Leaf("coal", tier: 0)],
            Fx.Recipe("r", inputs: [("iron", 1), ("coal", 1)], outputs: ("steel", 1, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal(8, Cost(table, "steel"));
        Assert.Equal("r", table.BestRecipeId("steel"));
    }

    [Fact]
    public void TheSolveRecordsTheAlternativeEachWinningRecipeWasPricedWith()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("circuit", tier: 0)],
            Fx.Recipe("wrap", slots: [[("anyCircuit", 1), ("circuit", 1)]], outputs: ("anyCircuit", 1, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal(Cost(table, "circuit"), Cost(table, "anyCircuit"));
        Assert.Equal("circuit", Assert.Single(table.ChosenInputs("anyCircuit")).ItemId);
        var wrapper = table.BestRecipe("anyCircuit");
        Assert.Equal(1, graph.Index.SlotCount(wrapper));
        Assert.Equal("anyCircuit", graph.Index.ItemIds[graph.Index.AlternativeItem[graph.Index.AlternativeAt(wrapper, 0, 0)]]);
    }

    [Fact]
    public void ChancedOutputsDivideByTheirChance()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("iron", tier: 0)],
            Fx.Recipe("sure", inputs: [("iron", 1)], outputs: ("plate", 1, 1.0)),
            Fx.Recipe("maybe", inputs: [("iron", 1)], outputs: ("shard", 1, 0.9)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal(Cost(table, "plate") / 0.9, Cost(table, "shard"), 9);
    }

    [Fact]
    public void ACycleNeverUndercutsItself()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ingot", tier: 0)],
            Fx.Recipe("pack", inputs: [("ingot", 9)], outputs: ("block", 1, 1.0)),
            Fx.Recipe("unpack", inputs: [("block", 1)], outputs: ("ingot", 9, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal(4, Cost(table, "ingot"));
        Assert.Equal(36, Cost(table, "block"));
        Assert.Null(table.BestRecipeId("ingot"));
        Assert.True(table.Converged);
    }

    [Fact]
    public void NoIngotPricesFromItsNuggets()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ingot", tier: 0), Fx.Leaf("nugget", parent: "ingot", divisor: 9)],
            Fx.Recipe("join", inputs: [("nugget", 9)], outputs: ("ingot", 1, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal(4, Cost(table, "ingot"));
        Assert.Equal(4.0 / 9, Cost(table, "nugget"), 9);
        Assert.Null(table.BestRecipeId("ingot"));
    }

    [Fact]
    public void ASlotWithAlternativesPricesFromTheCheapest()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("copper", tier: 0), Fx.Leaf("silver", tier: 1)],
            Fx.Recipe("r", slots: [[("copper", 1), ("silver", 1)]], outputs: ("wire", 1, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal(4, Cost(table, "wire"));
    }

    [Fact]
    public void AWeightChangeCanFlipTheCheapestAlternative()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("copper", tier: 0), Fx.Leaf("silver", tier: 1)],
            Fx.Recipe("r", slots: [[("copper", 1), ("silver", 1)]], outputs: ("wire", 1, 1.0)));

        var table = Fx.Solver().Solve(
            graph, Fx.Garage(), Fx.Weights(items: new() { ["silver"] = 1 }));

        Assert.Equal(1, Cost(table, "wire"));
    }

    [Fact]
    public void AlternativesCarryTheirOwnAmounts()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("water", weight: 1), Fx.Leaf("oxygen", weight: 2)],
            Fx.Recipe("r", slots: [[("water", 144), ("oxygen", 10)]], outputs: ("gel", 1, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal(20, Cost(table, "gel"));
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
                .Cost("metal")!.Value)
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

        Assert.False(table.IsPriced("rod"));
    }

    [Fact]
    public void ChancedTwinRowsPriceFromTheBestRow()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore", tier: 0)],
            Fx.Recipe("r", inputs: [("ore", 1)],
                outputs: [("dust", 1, 1.0), ("dust", 1, 0.5)]));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal(4, Cost(table, "dust"));
    }

    [Fact]
    public void CandidatesForUnreachableInputsAreInfinite()
    {
        var recipe = Fx.Recipe("r", inputs: [("ghost", 1)], outputs: ("thing", 1, 1.0));
        var graph = Fx.Graph([], recipe);

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.True(double.IsPositiveInfinity(Fx.Solver().Candidate(table, graph.Index.RecipeIndex["r"], "thing")));
    }

    [Fact]
    public void DustsLoseExactTiesToSolidForms()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("steelDust", tier: 0, leafClass: "dust"), Fx.Leaf("steelIngot", tier: 0)],
            Fx.Recipe("fromDust", inputs: [("steelDust", 1)], outputs: ("molten", 144, 1.0)),
            Fx.Recipe("fromIngot", inputs: [("steelIngot", 1)], outputs: ("molten", 144, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal("fromIngot", table.BestRecipeId("molten"));
        Assert.Equal(4.0 / 144, Cost(table, "molten"), 6);
    }

    [Fact]
    public void ARealPriceGapStillBeatsTheFormPreference()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("cheapDust", tier: 0, leafClass: "dust"), Fx.Leaf("dearIngot", tier: 1)],
            Fx.Recipe("fromDust", inputs: [("cheapDust", 1)], outputs: ("molten", 144, 1.0)),
            Fx.Recipe("fromIngot", inputs: [("dearIngot", 1)], outputs: ("molten", 144, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal("fromDust", table.BestRecipeId("molten"));
    }

    [Fact]
    public void TheFormRankingPrefersDustOverNugget()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("chip", tier: 0, leafClass: "nugget"), Fx.Leaf("raw", tier: 0, leafClass: "dust")],
            Fx.Recipe("fromChip", inputs: [("chip", 9)], outputs: ("molten", 9, 1.0)),
            Fx.Recipe("fromDust", inputs: [("raw", 9)], outputs: ("molten", 9, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal("fromDust", table.BestRecipeId("molten"));
    }

    [Fact]
    public void AFormTieNeverPointsTwoItemsAtEachOther()
    {
        // Macerating the ingot ties the dust's mix price and consumes a better form, but
        // the ingot's own best recipe is the blast from that dust: rerouting the dust to
        // the macerator would leave the pair explaining each other's price in a circle.
        var graph = Fx.Graph(
            [
                Fx.Leaf("rawDust", tier: 0, leafClass: "dust"),
                Fx.Leaf("alloyDust", tier: 0, leafClass: "dust"),
                Fx.Leaf("alloyIngot", tier: 0, leafClass: "ingot"),
            ],
            Fx.Recipe("mix", inputs: [("rawDust", 2)], outputs: ("alloyDust", 3, 1.0)),
            Fx.Recipe("blast", inputs: [("alloyDust", 1)], outputs: ("alloyIngot", 1, 1.0)),
            Fx.Recipe("macerate", inputs: [("alloyIngot", 1)], outputs: ("alloyDust", 1, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal("mix", table.BestRecipeId("alloyDust"));
        Assert.Equal("blast", table.BestRecipeId("alloyIngot"));
        Assert.Equal(8.0 / 3, Cost(table, "alloyDust"), 9);
        Assert.Equal(8.0 / 3, Cost(table, "alloyIngot"), 9);
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

        Assert.Equal("meltIngot", table.BestRecipeId("molten"));
        Assert.Equal(4.0 / 144, Cost(table, "molten"), 9);
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

        Assert.Equal("meltPlain", table.BestRecipeId("molten"));
        Assert.Equal(4.0 / 144, Cost(table, "molten"), 9);
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

        Assert.Equal("fromDust", table.BestRecipeId("metal"));
        Assert.Equal(4, Cost(table, "metal"));
    }

    [Fact]
    public void AToolFreeRouteWinsAnExactTie()
    {
        // Wrenching eight rods into two frames ties assembling four into one exactly, and the
        // hand craft set the price first; the recipe that wears no tool takes the pointer.
        var graph = Fx.Graph(
            [Fx.Leaf("rod", tier: 0)],
            Fx.Recipe("wrench", toolSlots: 1, inputs: [("rod", 8)], outputs: ("frame", 2, 1.0)),
            Fx.Recipe("assemble", machine: "Assembler", tier: 1, inputs: [("rod", 4)], outputs: ("frame", 1, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(defaultTier: 1), Fx.Weights());

        Assert.Equal("assemble", table.BestRecipeId("frame"));
        Assert.Equal(16, Cost(table, "frame"));
    }

    [Fact]
    public void FewerToolSlotsWinAmongToolRoutes()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("plate", tier: 0)],
            Fx.Recipe("twoTools", toolSlots: 2, inputs: [("plate", 1)], outputs: ("casing", 1, 1.0)),
            Fx.Recipe("oneTool", toolSlots: 1, inputs: [("plate", 1)], outputs: ("casing", 1, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());

        Assert.Equal("oneTool", table.BestRecipeId("casing"));
    }

    [Fact]
    public void ACheaperToolRouteStillWins()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("rod", tier: 0)],
            Fx.Recipe("wrench", toolSlots: 1, inputs: [("rod", 3)], outputs: ("frame", 1, 1.0)),
            Fx.Recipe("assemble", machine: "Assembler", tier: 1, inputs: [("rod", 4)], outputs: ("frame", 1, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(defaultTier: 1), Fx.Weights());

        Assert.Equal("wrench", table.BestRecipeId("frame"));
        Assert.Equal(12, Cost(table, "frame"));
    }

    [Fact]
    public void TheToolKeyComesAfterTheChainDepth()
    {
        // The tool-free route goes through a rod bent from the ingot; the hand craft takes the
        // ingot directly. Depth is judged first, so the shallower tool route keeps the item.
        var graph = Fx.Graph(
            [Fx.Leaf("steel", tier: 0)],
            Fx.Recipe("bend", inputs: [("steel", 1)], outputs: ("rod", 1, 1.0)),
            Fx.Recipe("hand", toolSlots: 1, inputs: [("steel", 1)], outputs: ("part", 1, 1.0)),
            Fx.Recipe("assemble", machine: "Assembler", tier: 1, inputs: [("rod", 1)], outputs: ("part", 1, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(defaultTier: 1), Fx.Weights());

        Assert.Equal("hand", table.BestRecipeId("part"));
    }
}
