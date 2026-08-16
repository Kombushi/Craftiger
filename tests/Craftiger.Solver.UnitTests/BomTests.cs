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
