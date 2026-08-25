namespace Craftiger.Builder.UnitTests;

public sealed class PriceCheckTests(LeakyPipelineFixture fixture) : IClassFixture<LeakyPipelineFixture>
{
    [Fact]
    public void ADuplicationLoopIsReported()
    {
        Assert.Equal(1, fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_recycle'"));
        Assert.True(fixture.Scalar<int>("SELECT value FROM meta WHERE key = 'price_leaks'") > 0);
    }
}
