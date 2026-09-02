namespace Craftiger.Builder.UnitTests;

/// <summary>The Algae Pond's synthesized rows: one per hatch tier at the hatch's power, compost twins for the factory, and its algae farmable leaves.</summary>
public sealed class AlgaePondTests(BuilderPipelineFixture fixture) : IClassFixture<BuilderPipelineFixture>
{
    [Fact]
    public void RowsShipPerHatchTierAtNineTenthsOfItsVoltage()
    {
        Assert.Equal("Algae Pond", fixture.Scalar<string>("SELECT machine FROM recipes WHERE id = 'r_algae_t0'"));
        // A ULV hatch draws 7 EU/t but the ladder starts at LV; the LV hatch draws 28 at its own tier.
        Assert.Equal(1, fixture.Scalar<long>("SELECT tier FROM recipes WHERE id = 'r_algae_t0'"));
        Assert.Equal(7, fixture.Scalar<long>("SELECT eu_t FROM recipes WHERE id = 'r_algae_t0'"));
        Assert.Equal(2000, fixture.Scalar<long>("SELECT duration_ticks FROM recipes WHERE id = 'r_algae_t0'"));
        Assert.Equal(1, fixture.Scalar<long>("SELECT tier FROM recipes WHERE id = 'r_algae_t1'"));
        Assert.Equal(28, fixture.Scalar<long>("SELECT eu_t FROM recipes WHERE id = 'r_algae_t1'"));
        Assert.Equal("FIXED", fixture.Scalar<string>("SELECT overclock FROM recipes WHERE id = 'r_algae_t0'"));
        Assert.Equal(0, fixture.Scalar<int>("SELECT COUNT(scope) FROM recipes WHERE id = 'r_algae_t0'"));
        Assert.Equal(0, fixture.Scalar<int>("SELECT COUNT(multi_tier) FROM recipes WHERE id = 'r_algae_t1'"));
        Assert.Equal(0, fixture.Scalar<int>("SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = 'r_algae_t0'"));
    }

    [Fact]
    public void ChancedOutputsMergePerItem()
    {
        Assert.Equal(2, fixture.Scalar<long>(
            $"SELECT amount FROM recipe_outputs WHERE recipe_id = 'r_algae_t0' AND item_id = '{FixtureDump.GreenAlgae}'"));
        Assert.Equal(0.9, fixture.Scalar<double>(
            $"SELECT chance FROM recipe_outputs WHERE recipe_id = 'r_algae_t0' AND item_id = '{FixtureDump.GreenAlgae}'"), 9);
        // Two sure and six at 90 % are 7.4 expected: eight at 92.5 %.
        Assert.Equal(8, fixture.Scalar<long>(
            $"SELECT amount FROM recipe_outputs WHERE recipe_id = 'r_algae_t1' AND item_id = '{FixtureDump.GreenAlgae}'"));
        Assert.Equal(0.925, fixture.Scalar<double>(
            $"SELECT chance FROM recipe_outputs WHERE recipe_id = 'r_algae_t1' AND item_id = '{FixtureDump.GreenAlgae}'"), 9);
    }

    [Fact]
    public void CompostBuysTheNextTiersRowForTheFactory()
    {
        Assert.Equal("FACTORY", fixture.Scalar<string>("SELECT scope FROM recipes WHERE id = 'r_algae_t0~c'"));
        Assert.Equal(1, fixture.Scalar<long>(
            $"SELECT amount FROM recipe_inputs WHERE recipe_id = 'r_algae_t0~c' AND item_id = '{FixtureDump.Compost}'"));
        Assert.Equal(10, fixture.Scalar<long>(
            $"SELECT amount FROM recipe_outputs WHERE recipe_id = 'r_algae_t0~c' AND item_id = '{FixtureDump.Algae}'"));
        Assert.Equal(1800, fixture.Scalar<long>("SELECT duration_ticks FROM recipes WHERE id = 'r_algae_t0~c'"));
        Assert.Equal(7, fixture.Scalar<long>("SELECT eu_t FROM recipes WHERE id = 'r_algae_t0~c'"));
        // No tier-2 row exists to buy, so the LV hatch has no twin.
        Assert.Equal(0, fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_algae_t1~c'"));
    }

    [Fact]
    public void WhatThePondGrowsIsAFarmableLeaf()
    {
        Assert.Equal("farmable", fixture.Scalar<string>($"SELECT leaf_class FROM items WHERE id = '{FixtureDump.Algae}'"));
        Assert.Equal("farmable", fixture.Scalar<string>($"SELECT leaf_class FROM items WHERE id = '{FixtureDump.GreenAlgae}'"));
    }
}
