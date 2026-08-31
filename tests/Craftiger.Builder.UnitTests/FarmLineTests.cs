namespace Craftiger.Builder.UnitTests;

/// <summary>Farm and mob lines ship factory-scoped, priced by no engine and dated by no era.</summary>
public class FarmLineTests(BuilderPipelineFixture fixture) : IClassFixture<BuilderPipelineFixture>
{
    [Fact]
    public void ACropGetsManagerAndFarmRowsAtEveryTier()
    {
        // One LV manager row (only the LV manager item exists) and one farm row per seed-bed tier.
        Assert.Equal(1, fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipes WHERE id LIKE 'farm~%naqBerry~cm%'"));
        Assert.Equal(12, fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipes WHERE id LIKE 'farm~%naqBerry~if%'"));
        Assert.Equal("FACTORY", fixture.Scalar<string>(
            "SELECT scope FROM recipes WHERE id LIKE 'farm~%naqBerry~cm1'"));
        Assert.Equal("FIXED", fixture.Scalar<string>(
            "SELECT overclock FROM recipes WHERE id LIKE 'farm~%naqBerry~cm1'"));
    }

    [Fact]
    public void AManagerRowBakesTheFieldAtItsTier()
    {
        // Tier-1 crop at 600 growth points: rate 10, 60 cycles of 256 ticks; the LV field is 121 sticks drinking one potency per cycle.
        Assert.Equal(15_360, fixture.Scalar<long>(
            "SELECT duration_ticks FROM recipes WHERE id LIKE 'farm~%naqBerry~cm1'"));
        Assert.Equal(0, fixture.Scalar<long>(
            "SELECT eu_t FROM recipes WHERE id LIKE 'farm~%naqBerry~cm1'"));
        Assert.Equal(7_260, fixture.Scalar<long>(
            "SELECT i.amount FROM recipe_inputs i JOIN recipes r ON r.id = i.recipe_id " +
            "WHERE r.id LIKE 'farm~%naqBerry~cm1' AND i.item_id LIKE 'f~%water%' AND i.catalyst = 0"));
        // The seed sits in the row as a non-consumed catalyst, one per stick.
        Assert.Equal(121, fixture.Scalar<long>(
            "SELECT i.amount FROM recipe_inputs i JOIN recipes r ON r.id = i.recipe_id " +
            "WHERE r.id LIKE 'farm~%naqBerry~cm1' AND i.catalyst = 1 AND i.item_id LIKE '%seed%'"));
    }

    [Fact]
    public void ACapturableMobGetsACrusherRow()
    {
        // Health 20 on nine-damage spikes stays under the spawn interval; the drop ships at its base probability.
        Assert.Equal("FACTORY_MOB", fixture.Scalar<string>(
            "SELECT scope FROM recipes WHERE id = 'eec~mob~1'"));
        Assert.Equal("EEC", fixture.Scalar<string>(
            "SELECT overclock FROM recipes WHERE id = 'eec~mob~1'"));
        Assert.Equal(55, fixture.Scalar<long>(
            "SELECT duration_ticks FROM recipes WHERE id = 'eec~mob~1'"));
        Assert.Equal(1920, fixture.Scalar<long>(
            "SELECT eu_t FROM recipes WHERE id = 'eec~mob~1'"));
        Assert.Equal(0.5, fixture.Scalar<double>(
            "SELECT chance FROM recipe_outputs WHERE recipe_id = 'eec~mob~1' AND item_id = 'i~fixture~mob_pearl'"));
        // The uncapturable boss gets no row.
        Assert.Equal(0, fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipes WHERE id = 'eec~mob~2'"));
    }
}
