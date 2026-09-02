namespace Craftiger.Builder.UnitTests;

/// <summary>Air and Nether Air are gathered by the intake hatch, which no recipe records, so the world-fluid list seeds them.</summary>
public sealed class WorldFluidTests(BuilderPipelineFixture fixture) : IClassFixture<BuilderPipelineFixture>
{
    [Fact]
    public void AirAndNetherAirAreWorldFluidLeavesAtWeightOne()
    {
        foreach (var fluid in new[] { FixtureDump.Air, FixtureDump.NetherAir })
        {
            Assert.Equal("world_fluid", fixture.Scalar<string>($"SELECT leaf_class FROM items WHERE id = '{fluid}'"));
            Assert.Equal(1.0, fixture.Scalar<double>($"SELECT weight FROM item_weights WHERE item_id = '{fluid}'"));
        }
    }

    [Fact]
    public void AirSeedsAtSteamAndNetherAirAtIv()
    {
        // The solidifier row is LV, so air's Steam seed leaves the ingot at the recipe's own era.
        Assert.Equal(1, fixture.Scalar<int>($"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.AirIngot}'"));
        Assert.Equal(5, fixture.Scalar<int>($"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.NetherAirIngot}'"));
    }
}
