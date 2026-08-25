namespace Craftiger.Builder.UnitTests;

public sealed class EraFloorTests(EraFloorFixture fixture) : IClassFixture<EraFloorFixture>
{
    [Fact]
    public void MachineEraFloorsRaiseTheGate() =>
        Assert.Equal(5, fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.WirelessIngot}'"));
}
