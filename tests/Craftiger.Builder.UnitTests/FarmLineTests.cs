namespace Craftiger.Builder.UnitTests;

/// <summary>Farm and mob lines ship factory-scoped, priced by no engine and dated by no era.</summary>
public class FarmLineTests(BuilderPipelineFixture fixture) : IClassFixture<BuilderPipelineFixture>
{
    [Fact]
    public void ACropGetsManagerAndFarmRowsAtEveryTier()
    {
        // The LV manager (the only manager item) hosts plain and fertilized rows, each with a bred twin.
        Assert.Equal(4, fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipes WHERE id LIKE 'farm~%naqBerry~cm%'"));
        // Twelve bed tiers: base, all-accelerator and overclocked builds carry the fertilizer axis, the
        // harvest build is always enriched, the overclocked build exists from ZPM — all doubled bred.
        Assert.Equal(148, fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipes WHERE id LIKE 'farm~%naqBerry~if%'"));
        Assert.Equal("FACTORY", fixture.Scalar<string>(
            "SELECT scope FROM recipes WHERE id LIKE 'farm~%naqBerry~cm1'"));
        Assert.Equal("FIXED", fixture.Scalar<string>(
            "SELECT overclock FROM recipes WHERE id LIKE 'farm~%naqBerry~cm1'"));
    }

    [Fact]
    public void AManagerRowBakesTheFieldAtItsTier()
    {
        // Tier-1 crop at 600 growth points: a fresh 1/1 seed rates 12, 50 cycles of 256 ticks; the LV field is 121 sticks drinking one potency per cycle.
        Assert.Equal(12_800, fixture.Scalar<long>(
            "SELECT duration_ticks FROM recipes WHERE id LIKE 'farm~%naqBerry~cm1'"));
        Assert.Equal(0, fixture.Scalar<long>(
            "SELECT eu_t FROM recipes WHERE id LIKE 'farm~%naqBerry~cm1'"));
        Assert.Equal(6_050, fixture.Scalar<long>(
            "SELECT i.amount FROM recipe_inputs i JOIN recipes r ON r.id = i.recipe_id " +
            "WHERE r.id LIKE 'farm~%naqBerry~cm1' AND i.item_id LIKE 'f~%water%' AND i.catalyst = 0"));
        // The seed sits in the row as a non-consumed catalyst, one per stick.
        Assert.Equal(121, fixture.Scalar<long>(
            "SELECT i.amount FROM recipe_inputs i JOIN recipes r ON r.id = i.recipe_id " +
            "WHERE r.id LIKE 'farm~%naqBerry~cm1' AND i.catalyst = 1 AND i.item_id LIKE '%seed%'"));
    }

    [Fact]
    public void AFertilizedRowGrowsFasterForItsFertilizer()
    {
        // Fertilized sticks rate 15: 40 cycles, and the manager spends items worth the water potency.
        Assert.Equal(10_240, fixture.Scalar<long>(
            "SELECT duration_ticks FROM recipes WHERE id LIKE 'farm~%naqBerry~cm1~f'"));
        Assert.Equal(49, fixture.Scalar<long>(
            "SELECT i.amount FROM recipe_inputs i JOIN recipes r ON r.id = i.recipe_id " +
            "WHERE r.id LIKE 'farm~%naqBerry~cm1~f' AND i.item_id = 'i~cropsnh~fertilizer~0'"));
    }

    [Fact]
    public void FarmBuildsBakeTheirUnitsIntoPowerAndSpeed()
    {
        // Four slots of accelerators at bed tier 5: five-fold speed, 1 + 4 x 1.25 times the power.
        Assert.Equal(2_560, fixture.Scalar<long>(
            "SELECT duration_ticks FROM recipes WHERE id LIKE 'farm~%naqBerry~if5~gau'"));
        Assert.Equal(46_080, fixture.Scalar<long>(
            "SELECT eu_t FROM recipes WHERE id LIKE 'farm~%naqBerry~if5~gau'"));
        // The harvest build always runs enriched liquid fertilizer: the 729-stick bed at 14
        // cycles drinks 10,206 potency, ten per mB.
        Assert.Equal(1_021, fixture.Scalar<long>(
            "SELECT i.amount FROM recipe_inputs i JOIN recipes r ON r.id = i.recipe_id " +
            "WHERE r.id LIKE 'farm~%naqBerry~if5~hrv' AND i.item_id = 'f~cropsnh~cropsnh.enrichedfertilizer'"));
        // The overclocked build alone climbs the standard ladder.
        Assert.Null(fixture.Scalar<string?>(
            "SELECT overclock FROM recipes WHERE id LIKE 'farm~%naqBerry~if7~oc'"));
        Assert.Equal("FIXED", fixture.Scalar<string>(
            "SELECT overclock FROM recipes WHERE id LIKE 'farm~%naqBerry~if7~gau'"));
    }

    [Fact]
    public void ABredRowWaitsForItsToggle()
    {
        // A 31/31 seed rates 64: ten cycles, gated behind the bred-seeds scope.
        Assert.Equal("FACTORY_BRED", fixture.Scalar<string>(
            "SELECT scope FROM recipes WHERE id LIKE 'farm~%naqBerry~cm1~b'"));
        Assert.Equal(2_560, fixture.Scalar<long>(
            "SELECT duration_ticks FROM recipes WHERE id LIKE 'farm~%naqBerry~cm1~b'"));
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
