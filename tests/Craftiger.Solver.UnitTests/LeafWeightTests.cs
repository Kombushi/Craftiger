using Craftiger.Solver.Services.Costs;

namespace Craftiger.Solver.UnitTests;

public sealed class LeafWeightTests
{
    private readonly LeafWeightService _service = new();

    [Fact]
    public void TieredLeavesCostBaseTimesFourToTheTier()
    {
        var graph = Fx.Graph([Fx.Leaf("bronze", tier: 0), Fx.Leaf("aluminium", tier: 2)]);

        var weights = _service.Resolve(graph, Fx.Weights());

        Assert.Equal(4, weights["bronze"]);
        Assert.Equal(64, weights["aluminium"]);
    }

    [Fact]
    public void ShippedWeightBeatsTheTierRule()
    {
        var graph = Fx.Graph([Fx.Leaf("lava", tier: 3, weight: 2)]);

        Assert.Equal(2, _service.Resolve(graph, Fx.Weights())["lava"]);
    }

    [Fact]
    public void UserOverrideBeatsTheShippedWeight()
    {
        var graph = Fx.Graph([Fx.Leaf("oil", weight: 8)]);

        var weights = _service.Resolve(graph, Fx.Weights(items: new() { ["oil"] = 16 }));

        Assert.Equal(16, weights["oil"]);
    }

    [Fact]
    public void FractionsDivideTheirParentsWeight()
    {
        var graph = Fx.Graph([
            Fx.Leaf("ingot", tier: 1),
            Fx.Leaf("nugget", parent: "ingot", divisor: 9)]);

        Assert.Equal(16.0 / 9, _service.Resolve(graph, Fx.Weights())["nugget"], 9);
    }

    [Fact]
    public void FractionsFollowTheirParentsOverride()
    {
        var graph = Fx.Graph([
            Fx.Leaf("ingot", tier: 1),
            Fx.Leaf("nugget", parent: "ingot", divisor: 9)]);

        var weights = _service.Resolve(graph, Fx.Weights(items: new() { ["ingot"] = 90 }));

        Assert.Equal(10, weights["nugget"]);
    }

    [Fact]
    public void FlatLeavesDefaultToOne()
    {
        var graph = Fx.Graph([Fx.Leaf("log")]);

        Assert.Equal(1, _service.Resolve(graph, Fx.Weights())["log"]);
    }
}
