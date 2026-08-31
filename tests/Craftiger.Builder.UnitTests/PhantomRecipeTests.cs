namespace Craftiger.Builder.UnitTests;

public sealed class PhantomRecipeTests(PhantomRecipeFixture fixture) : IClassFixture<PhantomRecipeFixture>
{
    [Fact]
    public void APhantomRegistrationNeverReachesTheArtifact() =>
        Assert.Equal(0, fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_melt'"));

    [Fact]
    public void RecipesCarryTheirAmperage()
    {
        Assert.Equal(2, fixture.Scalar<int>("SELECT amps FROM recipes WHERE id = 'r_macerate'"));
        Assert.Equal(1, fixture.Scalar<int>("SELECT amps FROM recipes WHERE id = 'r_ebf'"));
    }

    [Fact]
    public void RecipesCarryTheirRequirementFlags()
    {
        Assert.Equal(2, fixture.Scalar<int>(
            "SELECT cleanroom + low_gravity FROM recipes WHERE id = 'r_flags'"));
        Assert.Equal(0, fixture.Scalar<int>(
            "SELECT cleanroom + low_gravity FROM recipes WHERE id = 'r_extrude'"));
    }

    [Fact]
    public void ACellFuelResolvesToItsFluidPerMillibucket() =>
        Assert.Equal(360.0, fixture.Scalar<double>(
            "SELECT f.eu_per_unit FROM fuels f JOIN items i ON i.id = f.item_id " +
            "WHERE f.map = 'Gas Turbine Fuel' AND i.name_en = 'Benzene'"));

    [Fact]
    public void ASmallCellStillReadsPerMillibucket() =>
        Assert.Equal(999.0, fixture.Scalar<double>(
            "SELECT f.eu_per_unit FROM fuels f JOIN items i ON i.id = f.item_id " +
            "WHERE i.name_en = 'Fixture Plasma'"));

    [Fact]
    public void ASolidFuelBurnsAsAThousandMillibuckets() =>
        Assert.Equal(20000.0, fixture.Scalar<double>(
            "SELECT f.eu_per_unit FROM fuels f JOIN items i ON i.id = f.item_id " +
            "WHERE i.name_en = 'Fixture Solid Fuel'"));

    [Fact]
    public void AnRtgPelletCarriesItsLifetime()
    {
        Assert.Equal(480.0, fixture.Scalar<double>(
            "SELECT f.eu_t FROM fuels f JOIN items i ON i.id = f.item_id " +
            "WHERE i.name_en = 'Fixture Pellet'"));
        Assert.Equal(365L * 24000L, fixture.Scalar<long>(
            "SELECT f.duration_ticks FROM fuels f JOIN items i ON i.id = f.item_id " +
            "WHERE i.name_en = 'Fixture Pellet'"));
    }

    [Fact]
    public void ATimedFuelSplitsTotalEuOverItsBurn()
    {
        Assert.Equal(10000.0, fixture.Scalar<double>(
            "SELECT f.eu_t FROM fuels f JOIN items i ON i.id = f.item_id " +
            "WHERE i.name_en = 'Fixture Naquadah Fuel'"));
        Assert.Equal(160, fixture.Scalar<int>(
            "SELECT f.duration_ticks FROM fuels f JOIN items i ON i.id = f.item_id " +
            "WHERE i.name_en = 'Fixture Naquadah Fuel'"));
    }

    [Fact]
    public void MachinePropsMergePerMachineItem()
    {
        Assert.Equal(95.0, fixture.Scalar<double>(
            "SELECT p.generator_efficiency FROM machine_props p JOIN items i ON i.id = p.item_id " +
            "WHERE i.name_en = 'Fixture Gas Turbine'"));
        Assert.Equal(4 * 512, fixture.Scalar<int>(
            "SELECT p.dynamo_eu_t * p.dynamo_amps FROM machine_props p JOIN items i ON i.id = p.item_id " +
            "WHERE i.name_en = 'Fixture Dynamo Hatch'"));
        Assert.Equal(480, fixture.Scalar<int>(
            "SELECT p.boiler_eu_t FROM machine_props p JOIN items i ON i.id = p.item_id " +
            "WHERE i.name_en = 'Fixture Large Boiler'"));
    }

    [Fact]
    public void MultiblockBonusesShipWithTheirScalingAxis()
    {
        Assert.Equal(8, fixture.Scalar<int>(
            "SELECT max_parallel FROM machine_props WHERE item_id = " +
            "(SELECT item_id FROM machine_bonuses WHERE kind = 'PARALLEL' LIMIT 1)"));
        Assert.Equal("COIL", fixture.Scalar<string>(
            "SELECT tier_axis FROM machine_bonuses WHERE kind = 'PARALLEL_PER_TIER'"));
        Assert.Equal(1, fixture.Scalar<int>(
            "SELECT multiplicative FROM machine_bonuses WHERE kind = 'PARALLEL_PER_TIER'"));
    }

    [Fact]
    public void ARotorShipsItsStatsPerFuelClass()
    {
        Assert.Equal(0.85, fixture.Scalar<double>(
            "SELECT base_efficiency FROM turbine_rotors WHERE material = 'Fixture'"));
        Assert.Equal(212.5, fixture.Scalar<double>(
            "SELECT s.optimal_eut FROM rotor_fuel_stats s JOIN turbine_rotors r " +
            "ON r.item_id = s.item_id WHERE r.material = 'Fixture' AND s.fuel = 'STEAM'"));
        Assert.Equal(3, fixture.Scalar<int>(
            "SELECT COUNT(*) FROM rotor_fuel_stats s JOIN turbine_rotors r " +
            "ON r.item_id = s.item_id WHERE r.material = 'Fixture'"));
    }

    [Fact]
    public void AutoInfiniteSeedsMarkOnlyWorldSources()
    {
        // Crops, farmables and mob drops are farm-lined, never free: WORLD is the only seed kind left.
        Assert.Equal("WORLD", fixture.Scalar<string>(
            "SELECT s.kind FROM renewable_seeds s JOIN items i ON i.id = s.item_id " +
            "WHERE i.name_en = 'Water'"));
        Assert.Equal(0, fixture.Scalar<int>(
            "SELECT COUNT(*) FROM renewable_seeds WHERE kind != 'WORLD'"));
    }

    [Fact]
    public void TierVoltagesMirrorTheLadder() =>
        Assert.StartsWith("[0,32,128,512", fixture.Scalar<string>(
            "SELECT value FROM meta WHERE key = 'tier_voltages'"));

    [Fact]
    public void MachineItemsMirrorTheMapMachineLists() =>
        Assert.Equal(1, fixture.Scalar<int>(
            "SELECT COUNT(*) FROM machine_items WHERE map = 'Blast Furnace' AND multiblock = 1"));

    [Fact]
    public void DeprecatedControllersNeverShipAsMachineBlocks()
    {
        Assert.Equal(0, fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM machine_items WHERE item_id = '{FixtureDump.DeadTurbine}'"));
        Assert.Equal(2, fixture.Scalar<int>(
            "SELECT COUNT(*) FROM machine_items WHERE map = 'Gas Turbine Fuel'"));
    }

    [Fact]
    public void OverlayParallelsLandInMachineProps() =>
        Assert.Equal(16, fixture.Scalar<int>(
            $"SELECT max_parallel FROM machine_props WHERE item_id = '{FixtureDump.XlTurbine}'"));

    [Fact]
    public void RotorTurbinesCarryTheirFuelClass()
    {
        Assert.Equal("GAS", fixture.Scalar<string>(
            $"SELECT rotor_fuel FROM machine_props WHERE item_id = '{FixtureDump.XlTurbine}'"));
        Assert.Equal(1, fixture.Scalar<int>(
            "SELECT COUNT(*) FROM machine_props WHERE rotor_fuel IS NOT NULL"));
    }

    [Fact]
    public void MachineItemsCarryTheirOwnEra() =>
        Assert.Equal(1, fixture.Scalar<int>(
            "SELECT EXISTS(SELECT 1 FROM machine_items WHERE era IS NOT NULL)"));

    [Fact]
    public void BoilerBurnTimesParsePerGenerationAndSkipNotAllowed()
    {
        Assert.Equal(2.0, fixture.Scalar<double>(
            "SELECT burn_seconds FROM boiler_fuels WHERE boiler = 'Bronze'"));
        Assert.Equal(1.0, fixture.Scalar<double>(
            "SELECT burn_seconds FROM boiler_fuels WHERE boiler = 'Steel'"));
        Assert.Equal(0, fixture.Scalar<int>(
            "SELECT COUNT(*) FROM boiler_fuels WHERE boiler LIKE 'Titanium%' OR boiler LIKE 'Tungsten%'"));
    }
}
