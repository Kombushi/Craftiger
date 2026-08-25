namespace Craftiger.Builder.UnitTests;

/// <summary>Matter conservation is derived, not configured: an untagged grind that outputs more than any accountable route puts in drops.</summary>
public sealed class ConservationTests(BuilderPipelineFixture fixture) : IClassFixture<BuilderPipelineFixture>
{
    [Fact]
    public void AmplifyingGrindsOfCraftedItemsDrop()
    {
        Assert.Equal(0, fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_pipe_grind'"));
        Assert.Equal(1, fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_pipe_block'"));
    }

    [Fact]
    public void ExactGrindsSurvive() =>
        Assert.Equal(1, fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_pipe_shave'"));

    [Fact]
    public void GrindsOfUnproducibleItemsSurvive() =>
        Assert.Equal(1, fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_shard_grind'"));

    [Fact]
    public void GrindsOfWorldMinableBlocksSurvive() =>
        Assert.Equal(1, fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_clay_grind'"));

    [Fact]
    public void ItemDataBoundsAnUntaggedGrind()
    {
        Assert.Equal(0, fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_databox_grind'"));
        Assert.Equal(1, fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_databox_shred'"));
    }

    [Fact]
    public void AnUndefinedItemDataAmountIsUnknownNotZero() =>
        Assert.Equal(1, fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_ghost_grind'"));

    [Fact]
    public void AWhiffOfGasCannotLaunderAnAmplifier() =>
        Assert.Equal(0, fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_pipe_gasarc'"));

    [Fact]
    public void AMoltenMeasureCarriesItsMatterIn() =>
        Assert.Equal(1, fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_pipe_infuse'"));
}
