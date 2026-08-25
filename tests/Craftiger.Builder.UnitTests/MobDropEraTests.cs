namespace Craftiger.Builder.UnitTests;

/// <summary>The fixture run with the uncapturable boss dated at MV.</summary>
public sealed class MobDropEraFixture : IDisposable
{
    private readonly FixtureRun _run = new(
        new KeyValuePair<string, string?>("ErasConfiguration:MobDropEras:mob~2", "2"));

    public T Scalar<T>(string sql) => _run.Scalar<T>(sql);

    public void Dispose() => _run.Dispose();
}

/// <summary>A mob's drops date the era solve only where a checked-in list dates the mob.</summary>
public sealed class MobDropEraTests(MobDropEraFixture fixture) : IClassFixture<MobDropEraFixture>
{
    [Fact]
    public void ADatedMobsDropSeedsItsEra() =>
        Assert.Equal(2, fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.RelicIngot}'"));
}
