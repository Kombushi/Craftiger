using Craftiger.Solver.Models;

namespace Craftiger.Solver.UnitTests;

public sealed class BomTests
{
    private static readonly IReadOnlyDictionary<string, string> NoPins =
        new Dictionary<string, string>();

    private static BomResult Compute(
        SolverGraph graph, IReadOnlyList<BomTarget> targets,
        IReadOnlyDictionary<string, string>? pins = null, Garage? garage = null)
    {
        garage ??= Fx.Garage();
        var table = Fx.Solver().Solve(graph, garage, Fx.Weights());
        return Fx.Bom().Compute(graph, table, garage, targets, pins ?? NoPins);
    }

    private static double Leaf(BomResult result, string itemId) =>
        result.Leaves.Single(leaf => leaf.ItemId == itemId).Amount;

    [Fact]
    public void TargetsSharingAnIntermediateMergeTheirLeafTotals()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("copper", tier: 0)],
            Fx.Recipe("wire", inputs: [("copper", 1)], outputs: ("cable", 2, 1.0)),
            Fx.Recipe("coil", inputs: [("cable", 4)], outputs: ("coilItem", 1, 1.0)),
            Fx.Recipe("motor", inputs: [("cable", 2)], outputs: ("motorItem", 1, 1.0)));

        var result = Compute(graph, [new BomTarget("coilItem", 1), new BomTarget("motorItem", 1)]);

        Assert.Equal(3, Leaf(result, "copper"));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ASlotListingTheRecipesOwnOutputExpandsToTheOtherAlternative()
    {
        // The circuit wrapper: an oredict slot that includes the wrapper itself; once the wrapper
        // is priced, both alternatives tie exactly and the wrapper comes first in the slot.
        var graph = Fx.Graph(
            [Fx.Leaf("circuit", tier: 0)],
            Fx.Recipe("wrap", slots: [[("anyCircuit", 1), ("circuit", 1)]], outputs: ("anyCircuit", 1, 1.0)));

        var result = Compute(graph, [new BomTarget("anyCircuit", 2)]);

        Assert.Equal(2, Leaf(result, "circuit"));
        Assert.Equal("circuit", Assert.Single(result.Nodes).InputsPerRun.Single().ItemId);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void RunsAreFractionalExpectedValues()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("clayBlock", weight: 1)],
            Fx.Recipe("break", machine: "Mining", inputs: [("clayBlock", 1)], outputs: ("clayBall", 4, 1.0)));

        var result = Compute(graph, [new BomTarget("clayBall", 1)]);

        Assert.Equal(0.25, Leaf(result, "clayBlock"));
    }

    [Fact]
    public void ChancedOutputsNeedProportionallyMoreRuns()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("iron", tier: 0)],
            Fx.Recipe("r", inputs: [("iron", 1)], outputs: ("shard", 1, 0.9)));

        var result = Compute(graph, [new BomTarget("shard", 1)]);

        Assert.Equal(1 / 0.9, Leaf(result, "iron"), 9);
    }

    [Fact]
    public void ChancedTwinRowsSumTheirYield()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore", tier: 0)],
            Fx.Recipe("r", inputs: [("ore", 3)], outputs: [("dust", 1, 1.0), ("dust", 1, 0.5)]));

        var result = Compute(graph, [new BomTarget("dust", 3)]);

        Assert.Equal(6, Leaf(result, "ore"), 9);
    }

    [Fact]
    public void APinChangesTheBom()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("copper", tier: 0), Fx.Leaf("silver", tier: 1)],
            Fx.Recipe("cheap", inputs: [("copper", 1)], outputs: ("wire", 1, 1.0)),
            Fx.Recipe("dear", inputs: [("silver", 1)], outputs: ("wire", 1, 1.0)));

        var unpinned = Compute(graph, [new BomTarget("wire", 1)]);
        var pinned = Compute(graph, [new BomTarget("wire", 1)],
            new Dictionary<string, string> { ["wire"] = "dear" });

        Assert.Equal(1, Leaf(unpinned, "copper"));
        Assert.Equal(1, Leaf(pinned, "silver"));
        Assert.Equal("dear", pinned.Targets.Single().RecipeId);
    }

    [Fact]
    public void AnIllegalPinWarnsAndChangesNothing()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("copper", tier: 0), Fx.Leaf("silver", tier: 1)],
            Fx.Recipe("cheap", inputs: [("copper", 1)], outputs: ("wire", 1, 1.0)),
            Fx.Recipe("dear", machine: "Extruder", inputs: [("silver", 1)], outputs: ("wire", 1, 1.0)));
        var garage = Fx.Garage(defaultTier: 3, tiers: new() { ["Extruder"] = null });

        var result = Compute(graph, [new BomTarget("wire", 1)],
            new Dictionary<string, string> { ["wire"] = "dear" }, garage);

        Assert.Equal(1, Leaf(result, "copper"));
        Assert.Contains(result.Warnings, warning => warning is { Kind: "pin_illegal", ItemId: "wire" });
    }

    [Fact]
    public void AnUnknownPinWarns()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("copper", tier: 0)],
            Fx.Recipe("cheap", inputs: [("copper", 1)], outputs: ("wire", 1, 1.0)));

        var result = Compute(graph, [new BomTarget("wire", 1)],
            new Dictionary<string, string> { ["wire"] = "gone" });

        Assert.Contains(result.Warnings, warning => warning is { Kind: "pin_unknown", ItemId: "wire" });
    }

    [Fact]
    public void APinClosingACycleIsDroppedWithAWarning()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore", tier: 0)],
            Fx.Recipe("smelt", inputs: [("ore", 1)], outputs: ("metal", 1, 1.0)),
            Fx.Recipe("cast", inputs: [("metal", 1)], outputs: ("rod", 1, 1.0)),
            Fx.Recipe("melt", inputs: [("rod", 1)], outputs: ("metal", 1, 1.0)));

        var result = Compute(graph, [new BomTarget("rod", 1)],
            new Dictionary<string, string> { ["metal"] = "melt" });

        Assert.Contains(result.Warnings, warning => warning is { Kind: "pin_cycle", ItemId: "metal" });
        Assert.Equal(1, Leaf(result, "ore"));
    }

    /// <summary>The crystal chip loop: hammer a chip into nine parts, autoclave a part with
    /// europium back into a chip, and a 10 % gem route for the first chip.</summary>
    private static SolverGraph ChipLoop() =>
        Fx.Graph(
            [Fx.Leaf("eu", weight: 1, leafClass: "fluid"), Fx.Leaf("emerald", weight: 10, leafClass: "gem")],
            Fx.Recipe("hammer", inputs: [("chip", 1)], outputs: ("part", 9, 1.0)),
            Fx.Recipe("clave", inputs: [("part", 1), ("eu", 16)], outputs: ("chip", 1, 1.0)),
            Fx.Recipe("gem", inputs: [("emerald", 1), ("eu", 16)], outputs: ("chip", 1, 0.1)));

    [Fact]
    public void ALoopSumsItsSeriesAndSeedsOnce()
    {
        var result = Compute(ChipLoop(), [new BomTarget("chip", 8)]);

        // The seed's chip counts: the loop delivers the other seven — 7 × 9/8 autoclave runs
        // (8 whole, 128 mB), 7/8 of a hammer run (1 whole) — and the 10 % gem route runs once
        // for the seed: 10 expected runs, 10 emeralds, 160 mB.
        Assert.Empty(result.Warnings);
        Assert.Equal(7.875 * 16 + 160, Leaf(result, "eu"), 9);
        Assert.Equal(10, Leaf(result, "emerald"), 9);
        var chip = result.Nodes.Single(node => node is { ItemId: "chip", Seed: false });
        var part = result.Nodes.Single(node => node.ItemId == "part");
        var seed = result.Nodes.Single(node => node.Seed);
        Assert.Equal(7.875, chip.Amount, 9);
        Assert.Equal(7.875, chip.Runs, 9);
        Assert.Equal(8, chip.WholeRuns);
        Assert.Equal(0.875, part.Runs, 9);
        Assert.Equal(1, part.WholeRuns);
        Assert.Equal(chip.Loop, part.Loop);
        Assert.Equal(chip.Loop, seed.Loop);
        Assert.Equal("gem", seed.RecipeId);
        Assert.Equal(10, seed.Runs, 9);
        Assert.Equal(10, seed.WholeRuns);
        Assert.Equal(8 * 16 + 160, result.Leaves.Single(leaf => leaf.ItemId == "eu").WholeAmount);
    }

    [Fact]
    public void ASingleUnitOfALoopItemIsJustTheSeed()
    {
        var result = Compute(ChipLoop(), [new BomTarget("chip", 1)]);

        Assert.Empty(result.Warnings);
        var seed = Assert.Single(result.Nodes);
        Assert.True(seed.Seed);
        Assert.Equal(160, Leaf(result, "eu"), 9);
        Assert.Equal(10, Leaf(result, "emerald"), 9);
    }

    [Fact]
    public void ALoopPartDemandIsFedByHammeringTheSeed()
    {
        var result = Compute(ChipLoop(), [new BomTarget("part", 9)]);

        // Nine parts are one hammer run on the seed chip; the loop never autoclaves.
        Assert.Empty(result.Warnings);
        Assert.DoesNotContain(result.Nodes, node => node is { ItemId: "chip", Seed: false });
        var part = result.Nodes.Single(node => node.ItemId == "part");
        Assert.Equal(1, part.WholeRuns);
        Assert.Equal(160, Leaf(result, "eu"), 9);
    }

    [Fact]
    public void ALoopWithNoOutsideRouteWarnsAndKeepsItsTotals()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("eu", weight: 1, leafClass: "fluid"), Fx.Leaf("chip", weight: 4096, leafClass: "gem")],
            Fx.Recipe("hammer", inputs: [("chip", 1)], outputs: ("part", 9, 1.0)),
            Fx.Recipe("clave", inputs: [("part", 1), ("eu", 16)], outputs: ("chip", 1, 1.0)),
            Fx.Recipe("use", inputs: [("part", 2)], outputs: ("gadget", 1, 1.0)));

        var result = Compute(graph, [new BomTarget("gadget", 1)]);

        // The chip is a leaf here: the loop never forms, the walk stops at the leaf as always.
        Assert.Empty(result.Warnings);
        Assert.Equal(2.0 / 9, Leaf(result, "chip"), 9);
    }

    [Fact]
    public void ALoopWhoseOnlyOutsideRouteDrawsOnItWarnsAndKeepsItsTotals()
    {
        // The chip's other producer crushes dust, but the cheapest dust is milled from parts —
        // that route starts inside the loop, so it cannot start the loop.
        var graph = Fx.Graph(
            [Fx.Leaf("eu", weight: 1, leafClass: "fluid"), Fx.Leaf("ore", weight: 4096, leafClass: "gem")],
            Fx.Recipe("hammer", inputs: [("chip", 1)], outputs: ("part", 9, 1.0)),
            Fx.Recipe("clave", inputs: [("part", 1), ("eu", 16)], outputs: ("chip", 1, 1.0)),
            Fx.Recipe("grind", inputs: [("ore", 1)], outputs: ("dust", 1, 1.0)),
            Fx.Recipe("mill", inputs: [("part", 1), ("eu", 100)], outputs: ("dust", 1, 1.0)),
            Fx.Recipe("crush", inputs: [("dust", 1)], outputs: ("chip", 1, 1.0)));

        var result = Compute(graph, [new BomTarget("chip", 8)]);

        Assert.Contains(result.Warnings, warning => warning is { Kind: "loop_unseeded", ItemId: "chip" or "part" });
        Assert.Equal(144, Leaf(result, "eu"), 9);
        Assert.DoesNotContain(result.Nodes, node => node.Seed);
    }

    [Fact]
    public void AnUnreachableTargetWarnsAndAddsNothing()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore", tier: 0)],
            Fx.Recipe("r", machine: "Extruder", inputs: [("ore", 1)], outputs: ("rod", 1, 1.0)));
        var garage = Fx.Garage(defaultTier: 3, tiers: new() { ["Extruder"] = null });

        var result = Compute(graph, [new BomTarget("rod", 1)], garage: garage);

        Assert.Contains(result.Warnings, warning => warning is { Kind: "unreachable_target", ItemId: "rod" });
        Assert.Empty(result.Leaves);
        Assert.Null(result.Targets.Single().RecipeId);
    }

    [Fact]
    public void ALeafTargetGoesStraightToTheTotals()
    {
        var graph = Fx.Graph([Fx.Leaf("copper", tier: 0)]);

        var result = Compute(graph, [new BomTarget("copper", 5)]);

        Assert.Equal(5, Leaf(result, "copper"));
        Assert.Null(result.Targets.Single().RecipeId);
    }

    [Fact]
    public void ImmediateInputsScaleByTheTargetsRuns()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("copper", tier: 0)],
            Fx.Recipe("wire", inputs: [("copper", 3)], outputs: ("cable", 2, 1.0)));

        var result = Compute(graph, [new BomTarget("cable", 3)]);

        var target = result.Targets.Single();
        Assert.Equal("wire", target.RecipeId);
        Assert.Equal(4.5, target.Inputs.Single(input => input.ItemId == "copper").Amount);
    }

    [Fact]
    public void TheChosenAlternativeFollowsTheWeights()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("copper", tier: 0), Fx.Leaf("silver", tier: 1)],
            Fx.Recipe("r", slots: [[("copper", 1), ("silver", 1)]], outputs: ("wire", 1, 1.0)));
        var garage = Fx.Garage();

        var cheap = Fx.Solver().Solve(graph, garage, Fx.Weights());
        var flipped = Fx.Solver().Solve(graph, garage, Fx.Weights(items: new() { ["silver"] = 1 }));

        var byCopper = Fx.Bom().Compute(graph, cheap, garage, [new BomTarget("wire", 1)], NoPins);
        var bySilver = Fx.Bom().Compute(graph, flipped, garage, [new BomTarget("wire", 1)], NoPins);

        Assert.Equal(1, Leaf(byCopper, "copper"));
        Assert.Equal(1, Leaf(bySilver, "silver"));
    }

    [Fact]
    public void ARerouteLandsTheBomOnTheSolidLeaf()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("steelDust", tier: 0, leafClass: "dust"), Fx.Leaf("steelIngot", tier: 0)],
            Fx.Recipe("fromDust", inputs: [("steelDust", 1)], outputs: ("molten", 144, 1.0)),
            Fx.Recipe("fromIngot", inputs: [("steelIngot", 1)], outputs: ("molten", 144, 1.0)),
            Fx.Recipe("cast", inputs: [("molten", 144)], outputs: ("gear", 1, 1.0)));

        var result = Compute(graph, [new BomTarget("gear", 1)]);

        Assert.Equal(1, Leaf(result, "steelIngot"));
        Assert.DoesNotContain(result.Leaves, leaf => leaf.ItemId == "steelDust");
    }

    [Fact]
    public void WholeRunsRoundUpOncePerItem()
    {
        // One clay ball needs a whole block broken, even though only a quarter is consumed.
        var graph = Fx.Graph(
            [Fx.Leaf("clayBlock", weight: 1)],
            Fx.Recipe("break", machine: "Mining", inputs: [("clayBlock", 1)], outputs: ("clayBall", 4, 1.0)));

        var result = Compute(graph, [new BomTarget("clayBall", 1)]);

        var node = result.Nodes.Single();
        Assert.Equal(0.25, node.Runs);
        Assert.Equal(1, node.WholeRuns);
        var leaf = result.Leaves.Single();
        Assert.Equal(0.25, leaf.Amount);
        Assert.Equal(1, leaf.WholeAmount);
    }

    [Fact]
    public void SharedDemandMergesBeforeWholeRunsRound()
    {
        // Each consumer alone would round a batch of 16 up; merged they fill one batch.
        var graph = Fx.Graph(
            [Fx.Leaf("copper", tier: 0)],
            Fx.Recipe("batch", inputs: [("copper", 16)], outputs: ("wire", 16, 1.0)),
            Fx.Recipe("a", inputs: [("wire", 8)], outputs: ("gadgetA", 1, 1.0)),
            Fx.Recipe("b", inputs: [("wire", 8)], outputs: ("gadgetB", 1, 1.0)));

        var result = Compute(graph, [new BomTarget("gadgetA", 1), new BomTarget("gadgetB", 1)]);

        var wire = result.Nodes.Single(node => node.ItemId == "wire");
        Assert.Equal(16, wire.WholeAmount);
        Assert.Equal(1, wire.WholeRuns);
        Assert.Equal(16, result.Leaves.Single().WholeAmount);
    }

    [Fact]
    public void AnExactlyDivisibleDemandDoesNotRoundAnExtraRun()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("board", tier: 0), Fx.Leaf("plastic", weight: 1)],
            Fx.Recipe("wrap", inputs: [("board", 16), ("plastic", 72)], outputs: ("wrapItem", 1, 1.0)));

        var result = Compute(graph, [new BomTarget("wrapItem", 2)]);

        var node = result.Nodes.Single();
        Assert.Equal(2, node.WholeRuns);
        Assert.Equal(32, result.Leaves.Single(leaf => leaf.ItemId == "board").WholeAmount);
        Assert.Equal(144, result.Leaves.Single(leaf => leaf.ItemId == "plastic").WholeAmount);
    }

    [Fact]
    public void ChancedWholeRunsCoverTheDemandInExpectation()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("iron", tier: 0)],
            Fx.Recipe("r", inputs: [("iron", 1)], outputs: ("shard", 1, 0.9)));

        var result = Compute(graph, [new BomTarget("shard", 1)]);

        Assert.Equal(2, result.Nodes.Single().WholeRuns);
        Assert.Equal(2, result.Leaves.Single().WholeAmount);
    }

    [Fact]
    public void NodesListEveryExpandedStepTargetsFirst()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("copper", tier: 0)],
            Fx.Recipe("wire", inputs: [("copper", 1)], outputs: ("cable", 2, 1.0)),
            Fx.Recipe("motor", inputs: [("cable", 2)], outputs: ("motorItem", 1, 1.0)));

        var result = Compute(graph, [new BomTarget("motorItem", 3)]);

        Assert.Equal(new[] { "motorItem", "cable" }, result.Nodes.Select(node => node.ItemId));
        var motor = result.Nodes[0];
        Assert.Equal(3, motor.Amount);
        Assert.Equal(3, motor.Runs);
        Assert.Equal("motor", motor.RecipeId);
        Assert.Equal(2, motor.InputsPerRun.Single().Amount);
        var cable = result.Nodes[1];
        Assert.Equal(6, cable.Amount);
        Assert.Equal(3, cable.Runs);
        Assert.Equal("copper", cable.InputsPerRun.Single().ItemId);
    }

    [Fact]
    public void LeavesAndUnreachableItemsGetNoNode()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("copper", tier: 0)],
            Fx.Recipe("r", machine: "Extruder", inputs: [("copper", 1)], outputs: ("rod", 1, 1.0)));
        var garage = Fx.Garage(defaultTier: 3, tiers: new() { ["Extruder"] = null });

        var result = Compute(
            graph, [new BomTarget("copper", 5), new BomTarget("rod", 1)], garage: garage);

        Assert.Empty(result.Nodes);
    }

    [Fact]
    public void APinShowsUpInItsNode()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("copper", tier: 0), Fx.Leaf("silver", tier: 1)],
            Fx.Recipe("cheap", inputs: [("copper", 1)], outputs: ("wire", 1, 1.0)),
            Fx.Recipe("dear", inputs: [("silver", 1)], outputs: ("wire", 1, 1.0)));

        var result = Compute(graph, [new BomTarget("wire", 1)],
            new Dictionary<string, string> { ["wire"] = "dear" });

        var node = result.Nodes.Single();
        Assert.Equal("dear", node.RecipeId);
        Assert.Equal("silver", node.InputsPerRun.Single().ItemId);
    }

    [Fact]
    public void LeavesNeverExpandEvenWhenUndercut()
    {
        // The block leaf is deliberately dearer than packing it from ingots, so the solver
        // undercuts its weight — the BOM must still stop at the leaf.
        var graph = Fx.Graph(
            [Fx.Leaf("ingot", tier: 0), Fx.Leaf("block", weight: 100)],
            Fx.Recipe("pack", inputs: [("ingot", 9)], outputs: ("block", 1, 1.0)),
            Fx.Recipe("cut", inputs: [("block", 1)], outputs: ("slab", 2, 1.0)));

        var table = Fx.Solver().Solve(graph, Fx.Garage(), Fx.Weights());
        var result = Fx.Bom().Compute(graph, table, Fx.Garage(), [new BomTarget("slab", 2)], NoPins);

        Assert.Equal(36, table.Costs["block"]);
        Assert.Equal(1, Leaf(result, "block"));
        Assert.DoesNotContain(result.Leaves, leaf => leaf.ItemId == "ingot");
    }
}
