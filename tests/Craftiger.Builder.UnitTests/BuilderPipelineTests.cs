using Dapper;
using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Craftiger.Builder.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Craftiger.Builder.UnitTests;

/// <summary>One builder run over the fixture dump, against the settings the builder ships.</summary>
public sealed class FixtureRun : IDisposable
{
    public string PlannerPath { get; }
    private readonly string _directory;

    public FixtureRun(params KeyValuePair<string, string?>[] overrides)
    {
        _directory = Directory.CreateTempSubdirectory("craftiger-tests").FullName;
        var dumpPath = FixtureDump.Create(_directory);

        var settings = new Dictionary<string, string?>
        {
            ["BuilderOptions:DumpPath"] = dumpPath,
            ["BuilderOptions:OutputDir"] = _directory,
            ["BuilderOptions:PackVersion"] = "fixture-pack",
            ["BuilderOptions:ImagesPath"] = Path.Combine(_directory, "image.zip")
        };
        foreach (var (key, value) in overrides)
        {
            settings[key] = value;
        }

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddInMemoryCollection(settings)
            .Build();

        using var services = new ServiceCollection()
            .AddBuilderServices(configuration)
            .BuildServiceProvider();
        services.GetRequiredService<IBuilderPipeline>().Run();

        PlannerPath = Path.Combine(_directory, "planner.sqlite");
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    public T Scalar<T>(string sql)
    {
        using var db = new SqliteConnection($"Data Source={PlannerPath};Mode=ReadOnly");
        return db.ExecuteScalar<T>(sql)!;
    }
}

public sealed class BuilderPipelineFixture : IDisposable
{
    private readonly FixtureRun _run = new();

    public string PlannerPath => _run.PlannerPath;
    public void Dispose() => _run.Dispose();
    public T Scalar<T>(string sql) => _run.Scalar<T>(sql);
}

/// <summary>The same pack with the recycling exclusion switched off, so the widget arc loop
/// the fixture carries actually ships. Proves the price check reports a leak when there is one,
/// rather than reporting none because it never looks.</summary>
public sealed class LeakyPipelineFixture : IDisposable
{
    private readonly FixtureRun _run =
        new(new KeyValuePair<string, string?>(
            "RecipesConfiguration:RecyclingCategorySuffixes:0", "matches-no-category"));

    public void Dispose() => _run.Dispose();
    public T Scalar<T>(string sql) => _run.Scalar<T>(sql);
}

/// <summary>The same pack with one recipe condemned as a phantom registration, proving the
/// exclusion reaches the artifact.</summary>
public sealed class PhantomRecipeFixture : IDisposable
{
    private readonly FixtureRun _run =
        new(new KeyValuePair<string, string?>(
            "RecipesConfiguration:PhantomRecipeIds:r_melt", "fixture-only condemnation"));

    public void Dispose() => _run.Dispose();
    public T Scalar<T>(string sql) => _run.Scalar<T>(sql);
}

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
    public void AutoInfiniteSeedsMarkWorldFarmAndMobSources()
    {
        Assert.Equal("WORLD", fixture.Scalar<string>(
            "SELECT s.kind FROM renewable_seeds s JOIN items i ON i.id = s.item_id " +
            "WHERE i.name_en = 'Water'"));
        Assert.Equal("MOB", fixture.Scalar<string>(
            "SELECT s.kind FROM renewable_seeds s JOIN items i ON i.id = s.item_id " +
            "WHERE i.name_en = 'Fixture Mob Pearl'"));
        Assert.Equal(0, fixture.Scalar<int>(
            "SELECT COUNT(*) FROM renewable_seeds s JOIN items i ON i.id = s.item_id " +
            "WHERE i.name_en = 'Fixture Boss Relic'"));
        Assert.Equal(1, fixture.Scalar<int>(
            "SELECT EXISTS(SELECT 1 FROM renewable_seeds WHERE kind = 'FARM')"));
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

/// <summary>Matter conservation is derived, not configured: an untagged grind whose outputs
/// exceed what any accountable route puts into its input drops, and everything unprovable
/// stays.</summary>
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

/// <summary>The same pack with an era floor under the mixer, proving a quest-anchored gate
/// outranks everything the recipe graph derives.</summary>
public sealed class EraFloorFixture : IDisposable
{
    private readonly FixtureRun _run =
        new(new KeyValuePair<string, string?>("ErasConfiguration:MachineEraFloors:Mixer", "5"));

    public void Dispose() => _run.Dispose();
    public T Scalar<T>(string sql) => _run.Scalar<T>(sql);
}

public sealed class EraFloorTests(EraFloorFixture fixture) : IClassFixture<EraFloorFixture>
{
    [Fact]
    public void MachineEraFloorsRaiseTheGate() =>
        Assert.Equal(5, fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.WirelessIngot}'"));
}

public sealed class PriceCheckTests(LeakyPipelineFixture fixture) : IClassFixture<LeakyPipelineFixture>
{
    [Fact]
    public void ADuplicationLoopIsReported()
    {
        Assert.Equal(1, fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_recycle'"));
        Assert.True(fixture.Scalar<int>("SELECT value FROM meta WHERE key = 'price_leaks'") > 0);
    }
}

public sealed class BuilderPipelineTests : IClassFixture<BuilderPipelineFixture>
{
    private readonly BuilderPipelineFixture _fixture;

    public BuilderPipelineTests(BuilderPipelineFixture fixture) => _fixture = fixture;

    [Fact]
    public void BronzeSmeltsAtTierZeroDespiteBlockCycle() =>
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM item_tiers WHERE item_id = '{FixtureDump.GtBronze}' AND tier = 0"));

    [Fact]
    public void AluminiumTiersFromMultiAmpEbfNotFromBlockCycle() =>
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.AluIngot}'"));

    [Fact]
    public void CropHarvestsInheritTheirUnderBlockEra() =>
        Assert.Equal(4, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.BerryIngot}'"));

    [Fact]
    public void CropDropsClassifyAsCropDrops() =>
        Assert.Equal("crop_drop", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.Berry}'"));

    [Fact]
    public void HiddenCropsProduceNoHarvestRecipe() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipes WHERE id LIKE 'cnh~%'"));

    [Fact]
    public void UndergroundFluidsWaitForTheirPumpEra() =>
        Assert.Equal(4, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.OilIngot}'"));

    [Fact]
    public void PumpedFluidsAreWorldFluidsButNeverPriceThemselves()
    {
        Assert.Equal("world_fluid", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.Oil}'"));
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM recipes WHERE id = 'gtuf~{FixtureDump.Oil}'"));
    }

    [Fact]
    public void EndStoneSeedsAtItsOwnEra() =>
        Assert.Equal(3, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.EndIngot}'"));

    [Fact]
    public void GemsTierByTheirProductionEra() =>
        Assert.Equal(2, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.Gem}'"));

    [Fact]
    public void DustsInheritTheirGemTwinTier() =>
        Assert.Equal(2, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.GemDust}'"));

    [Fact]
    public void NuggetsAreLeavesOfTheirOwnClass() =>
        Assert.Equal("nugget", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.NugNugget}'"));

    [Fact]
    public void OreProcessingIntermediatesAreNeverLeaves() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(leaf_class) FROM items WHERE id = '{FixtureDump.NugImpure}'"));

    [Fact]
    public void CropDropsAnotherRecipeMakesAreNotLeaves() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(leaf_class) FROM items WHERE id = '{FixtureDump.ClayBall}'"));

    [Fact]
    public void TieredLeavesTheEraSolveNeverReachedAreDropped() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(leaf_class) FROM items WHERE id = '{FixtureDump.LostIngot}'"));

    [Fact]
    public void WorldFluidsCarryTheirOwnWeight() =>
        Assert.Equal(8.0, _fixture.Scalar<double>(
            $"SELECT weight FROM item_weights WHERE item_id = '{FixtureDump.Oil}'"));

    [Fact]
    public void ClayBallPricesFromBreakingItsBlock()
    {
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT amount FROM recipe_inputs WHERE recipe_id = 'bd~minecraft:clay~0' AND item_id = '{FixtureDump.ClayBlock}'"));
        Assert.Equal(4, _fixture.Scalar<int>(
            $"SELECT amount FROM recipe_outputs WHERE recipe_id = 'bd~minecraft:clay~0' AND item_id = '{FixtureDump.ClayBall}'"));
    }

    [Fact]
    public void OredictlessMinableBlocksStillSeedTheirDrops() =>
        Assert.Equal("minable_block", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.ClayBlock}'"));

    [Fact]
    public void BlocksDroppingThemselvesMakeNoRecipe() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipes WHERE id = 'bd~minecraft:obsidian~0'"));

    [Fact]
    public void MixedMapsDiscountOnceTheirMultiblockIsReachable() =>
        Assert.Equal(2, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.MixIngot}'"));

    [Fact]
    public void MixedMapsKeepFullTierWhileOnlyTheirSingleBlocksAreAffordable() =>
        Assert.Equal(3, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.DearIngot}'"));

    [Fact]
    public void MixedMapsShipATierForEachKindOfMachine()
    {
        Assert.Equal(3, _fixture.Scalar<int>("SELECT tier FROM recipes WHERE id = 'r_mix'"));
        Assert.Equal(2, _fixture.Scalar<int>("SELECT multi_tier FROM recipes WHERE id = 'r_mix'"));
    }

    [Fact]
    public void MultiblockOnlyMapsCarryTheAllowanceInTheirOwnTier()
    {
        Assert.Equal(1, _fixture.Scalar<int>("SELECT tier FROM recipes WHERE id = 'r_ebf'"));
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(multi_tier) FROM recipes WHERE id = 'r_ebf'"));
    }

    [Fact]
    public void SingleBlockOnlyMapsHaveNoMultiblockTier() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(multi_tier) FROM recipes WHERE id = 'r_macerate'"));

    [Fact]
    public void OresTheWorldNeverPlacesSeedNothing() =>
        Assert.Equal(2, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.PhantomIngot}'"));

    [Fact]
    public void RecyclingAManufacturedItemNeverReachesTheArtifact() =>
        Assert.Equal(0, _fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_recycle'"));

    [Fact]
    public void RecyclingOneShapeOfAMaterialIntoAnotherSurvives() =>
        Assert.Equal(1, _fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_melt'"));

    [Fact]
    public void RecyclingAWireSurvivesByGtsPrefixFlags() =>
        Assert.Equal(1, _fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_wire_recycle'"));

    [Fact]
    public void RecyclingAContainerNeverReachesTheArtifact() =>
        Assert.Equal(0, _fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_cell_recycle'"));

    [Fact]
    public void GemGradesPriceAsFractionsOfTheirGem()
    {
        Assert.Equal("gem_flawed", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.FlawedGem}'"));
        Assert.Equal(FixtureDump.Gem, _fixture.Scalar<string>(
            $"SELECT parent_item_id FROM item_parents WHERE item_id = '{FixtureDump.FlawedGem}'"));
        Assert.Equal(2.0, _fixture.Scalar<double>(
            $"SELECT divisor FROM item_parents WHERE item_id = '{FixtureDump.FlawedGem}'"));
        Assert.Equal(0.25, _fixture.Scalar<double>(
            $"SELECT divisor FROM item_parents WHERE item_id = '{FixtureDump.ExquisiteGem}'"));
    }

    [Fact]
    public void FallbackTiersComeFromRealRecipesNotPilePacking() =>
        Assert.Equal(2, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.InertDust}'"));

    [Fact]
    public void EveryMachineShipsItsAvailabilityEra()
    {
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipes WHERE machine NOT IN (SELECT machine FROM machine_eras)"));
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT era FROM machine_eras WHERE machine = 'Crafting Table'"));
        Assert.False(_fixture.Scalar<bool>(
            "SELECT era IS NULL FROM machine_eras WHERE machine = 'Electric Blast Furnace'"));
    }

    [Fact]
    public void OnlyMultiblockOnlyMapsFlagAsMultiblocks()
    {
        Assert.True(_fixture.Scalar<bool>(
            "SELECT multiblock FROM machine_eras WHERE machine = 'Electric Blast Furnace'"));
        Assert.False(_fixture.Scalar<bool>(
            "SELECT multiblock FROM machine_eras WHERE machine = 'Mixer'"));
        Assert.False(_fixture.Scalar<bool>(
            "SELECT multiblock FROM machine_eras WHERE machine = 'Macerator'"));
        Assert.False(_fixture.Scalar<bool>(
            "SELECT multiblock FROM machine_eras WHERE machine = 'Crafting Table'"));
    }

    [Fact]
    public void ArtifactStampsItsSchemaVersion() =>
        Assert.Equal(Repositories.PlannerRepository.SchemaVersion.ToString(),
            _fixture.Scalar<string>("SELECT value FROM meta WHERE key = 'schema_version'"));

    [Fact]
    public void ArtifactStampsAUniqueBuildId() =>
        Assert.Matches("^[0-9a-f]{32}$", _fixture.Scalar<string>("SELECT value FROM meta WHERE key = 'build_id'"));

    [Fact]
    public void ArtifactIndexesCaseFoldedSearchText()
    {
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM item_search WHERE item_id = '{FixtureDump.GtBronze}' AND text = 'ingotbronze'"));
        Assert.Equal(
            _fixture.Scalar<int>("SELECT COUNT(*) FROM items") + _fixture.Scalar<int>("SELECT COUNT(*) FROM item_aliases"),
            _fixture.Scalar<int>("SELECT COUNT(*) FROM item_search"));
        Assert.Equal(FixtureDump.GtBronze, _fixture.Scalar<string>(
            "SELECT item_id FROM item_search WHERE item_search MATCH '\"otbro\"'"));
    }

    [Fact]
    public void ArtifactCarriesQueryStatistics() =>
        Assert.True(_fixture.Scalar<int>("SELECT COUNT(*) FROM sqlite_stat1") > 0);

    [Fact]
    public void FractionLeavesShipTheirParentLink()
    {
        Assert.Equal(FixtureDump.NugIngot, _fixture.Scalar<string>(
            $"SELECT parent_item_id FROM item_parents WHERE item_id = '{FixtureDump.NugNugget}'"));
        Assert.Equal(9.0, _fixture.Scalar<double>(
            $"SELECT divisor FROM item_parents WHERE item_id = '{FixtureDump.NugNugget}'"));
        Assert.Equal(FixtureDump.InertDust, _fixture.Scalar<string>(
            $"SELECT parent_item_id FROM item_parents WHERE item_id = '{FixtureDump.InertSmall}'"));
    }

    [Fact]
    public void EveryShippedFractionHasAParentRow() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM items WHERE leaf_class IN ('dust_small', 'dust_tiny', 'nugget', " +
            "'gem_chipped', 'gem_flawed', 'gem_flawless', 'gem_exquisite') " +
            "AND id NOT IN (SELECT item_id FROM item_parents)"));

    [Fact]
    public void NoLeafPricesFarBelowItsOwnWeight()
    {
        Assert.Equal(0, _fixture.Scalar<int>("SELECT value FROM meta WHERE key = 'price_leaks'"));
        Assert.Equal(0, _fixture.Scalar<int>("SELECT value FROM meta WHERE key = 'price_free_items'"));
        Assert.Equal(1, _fixture.Scalar<int>("SELECT value FROM meta WHERE key = 'price_converged'"));
    }

    [Fact]
    public void EbfRecipeKeepsHeat() =>
        Assert.Equal(1700, _fixture.Scalar<int>("SELECT heat FROM recipes WHERE id = 'r_ebf'"));

    [Fact]
    public void ChancedOutputKeepsChance() =>
        Assert.Equal(0.9, _fixture.Scalar<double>(
            "SELECT chance FROM recipe_outputs WHERE recipe_id = 'r_macerate' AND chance < 1"), precision: 9);

    [Fact]
    public void CatalystsShipAsDisplayOnlyRows()
    {
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM recipe_inputs WHERE item_id IN ('{FixtureDump.Saw}', '{FixtureDump.Mold}') AND catalyst = 0"));
        Assert.Equal(1, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = 'r_planks' AND catalyst = 0"));
        Assert.Equal(FixtureDump.Saw, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_planks' AND catalyst = 1"));
        Assert.Equal(FixtureDump.Mold, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_extrude' AND catalyst = 1"));
        Assert.Equal(1, _fixture.Scalar<int>(
            "SELECT amount FROM recipe_inputs WHERE recipe_id = 'r_extrude' AND catalyst = 1"));
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM items WHERE id = '{FixtureDump.Saw}'"));
    }

    [Fact]
    public void AMetaWearingToolCondemnsItsSlot() =>
        Assert.Equal(FixtureDump.TinkerSaw, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_tinker_cut' AND catalyst = 1"));

    [Fact]
    public void OnlyWearingToolsCarryTheToolFlag()
    {
        // Both saws wear; the mold is a catalyst that never does. In a mixed slot the flag is
        // per member: the saw carries it, the ingot beside it does not.
        Assert.Equal(1, _fixture.Scalar<int>(
            "SELECT tool FROM recipe_inputs WHERE recipe_id = 'r_planks' AND catalyst = 1"));
        Assert.Equal(1, _fixture.Scalar<int>(
            "SELECT tool FROM recipe_inputs WHERE recipe_id = 'r_tinker_cut' AND catalyst = 1"));
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT tool FROM recipe_inputs WHERE recipe_id = 'r_extrude' AND catalyst = 1"));
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT tool FROM recipe_inputs WHERE recipe_id = 'r_tool_choice' AND item_id = '{FixtureDump.Saw}'"));
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT tool FROM recipe_inputs WHERE recipe_id = 'r_tool_choice' AND item_id = '{FixtureDump.IronIngot}'"));
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_inputs WHERE catalyst = 0 AND tool = 1"));
    }

    [Fact]
    public void ItemsCarryTheirStackSizeAndFluidsNone()
    {
        Assert.Equal(64, _fixture.Scalar<int>($"SELECT max_stack FROM items WHERE id = '{FixtureDump.IronIngot}'"));
        Assert.Equal(1, _fixture.Scalar<int>($"SELECT max_stack FROM items WHERE id = '{FixtureDump.Saw}'"));
        Assert.Equal(0, _fixture.Scalar<int>("SELECT COUNT(max_stack) FROM items WHERE is_fluid = 1"));
        Assert.Equal(0, _fixture.Scalar<int>("SELECT COUNT(*) FROM items WHERE is_fluid = 0 AND max_stack IS NULL"));
    }

    [Fact]
    public void ShapedRecipesKeepTheirGridOverTheFoldedSlots()
    {
        // r_planks: the log in cell 0 is ingredient slot 0, the saw in cell 1 the catalyst slot
        // after it; r_tool_choice puts the tool slot first on the grid and the log second; a
        // choice slot is addressed by its own number; furnace recipes have no shape.
        Assert.Equal("0:0,1:1", _fixture.Scalar<string>(
            "SELECT GROUP_CONCAT(cell || ':' || slot, ',') FROM (SELECT cell, slot FROM recipe_grid WHERE recipe_id = 'r_planks' ORDER BY cell)"));
        Assert.Equal("0:1,1:0", _fixture.Scalar<string>(
            "SELECT GROUP_CONCAT(cell || ':' || slot, ',') FROM (SELECT cell, slot FROM recipe_grid WHERE recipe_id = 'r_tool_choice' ORDER BY cell)"));
        Assert.Equal("0:0", _fixture.Scalar<string>(
            "SELECT GROUP_CONCAT(cell || ':' || slot, ',') FROM recipe_grid WHERE recipe_id = 'r_any_iron_use'"));
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_grid g JOIN recipes r ON r.id = g.recipe_id WHERE r.machine <> 'Crafting Table'"));
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_grid g WHERE NOT EXISTS (SELECT 1 FROM recipe_inputs i WHERE i.recipe_id = g.recipe_id AND i.slot = g.slot)"));
    }

    [Fact]
    public void AContainerReturningItemStaysAnIngredient() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = 'r_soup' AND catalyst = 1"));

    [Fact]
    public void ChoiceSlotsShipEveryAlternativeUnderOneSlot()
    {
        Assert.Equal(2, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = 'r_any_iron_use'"));
        Assert.Equal(1, _fixture.Scalar<int>(
            "SELECT COUNT(DISTINCT slot) FROM recipe_inputs WHERE recipe_id = 'r_any_iron_use'"));
    }

    [Fact]
    public void OneToolCondemnsTheWholeSlot()
    {
        Assert.Equal(1, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = 'r_tool_choice' AND catalyst = 0"));
        Assert.Equal(FixtureDump.Log, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_tool_choice' AND catalyst = 0"));
        Assert.Equal(2, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = 'r_tool_choice' AND catalyst = 1"));
        Assert.Equal(1, _fixture.Scalar<int>(
            "SELECT COUNT(DISTINCT slot) FROM recipe_inputs WHERE recipe_id = 'r_tool_choice' AND catalyst = 1"));
    }

    [Fact]
    public void ConcreteInputsAndChoicesGetSeparateSlots()
    {
        Assert.Equal(3, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = 'r_mixed_slots'"));
        Assert.Equal(2, _fixture.Scalar<int>(
            "SELECT COUNT(DISTINCT slot) FROM recipe_inputs WHERE recipe_id = 'r_mixed_slots'"));
    }

    [Fact]
    public void OredictEquivalentIngotsUnifyToOneCanonicalItem()
    {
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM items WHERE id = '{FixtureDump.Ic2Bronze}'"));
        Assert.Equal(FixtureDump.GtBronze, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_macerate'"));
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM item_aliases WHERE item_id = '{FixtureDump.GtBronze}' AND alias = 'ingotBronze'"));
    }

    [Fact]
    public void MachineAvailabilityGatesRecipeEra() =>
        Assert.Equal(4, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.ColdIngot}'"));

    [Fact]
    public void OffworldOresSeedAtTheirDimensionEra() =>
        Assert.Equal(4, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.NaqIngot}'"));

    [Fact]
    public void SpaceMiningGatesEraButNeverPrices()
    {
        Assert.Equal(4, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.KobIngot}'"));
        Assert.Equal(0, _fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_space'"));
    }

    [Fact]
    public void RawChunksSeedAtTheirVeinEra() =>
        Assert.Equal(4, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.RuniteIngot}'"));

    [Fact]
    public void WirelessRecipesTakeNoVoltageTier() =>
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM item_tiers WHERE item_id = '{FixtureDump.WirelessIngot}' AND tier = 0"));

    [Fact]
    public void StoneVariantsSeedOnlyInTheirOwnDimensions() =>
        Assert.Equal(4, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.DualIngot}'"));

    [Fact]
    public void OresWithoutWorldgenNeverSeedAnEra() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.ComIngot}'"));

    [Fact]
    public void MachineInputVoltageFloorsRecipeEra() =>
        Assert.Equal(2, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.DryIngot}'"));

    [Fact]
    public void MaceratorByproductSlotsOpenByTier()
    {
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM recipe_outputs WHERE recipe_id = 'r_cu_macerate' AND item_id = '{FixtureDump.ByDust}'"));
        Assert.Equal(3, _fixture.Scalar<int>("SELECT tier FROM recipes WHERE id = 'r_cu_macerate~b3'"));
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM recipe_outputs WHERE recipe_id = 'r_cu_macerate~b3' AND item_id = '{FixtureDump.ByDust}'"));
        Assert.Equal(3, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.ByIngot}'"));
    }

    [Fact]
    public void DustsInheritTheirIngotTier()
    {
        Assert.Equal(4, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.NaqDust}'"));
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.AluDust}'"));
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM item_tiers WHERE item_id = '{FixtureDump.BronzeDust}' AND tier != 0"));
    }

    [Fact]
    public void DerivedDustsInheritEraInsteadOfSeedingZero()
    {
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.AnnealedIngot}'"));
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.CopperIngot}'"));
    }

    [Fact]
    public void WildcardOredictsDoNotUnify()
    {
        Assert.Equal(FixtureDump.IronIngot, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_iron_use'"));
        Assert.Equal(FixtureDump.CastIron, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_cast_use'"));
    }

    [Fact]
    public void TheCanonicalIsGtsUnificationTarget()
    {
        Assert.Equal(FixtureDump.TargetGt, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_target_use'"));
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM items WHERE id = '{FixtureDump.TargetVanilla}'"));
    }

    [Fact]
    public void BlacklistedMembersKeepTheirIdentity()
    {
        Assert.Equal(2, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = 'r_blackium_use'"));
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM items WHERE id = '{FixtureDump.BlackMetal}'"));
    }

    [Fact]
    public void ConventionPrefixesNeverClassifyMaterials() =>
        Assert.Null(_fixture.Scalar<string?>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.PlanetDust}'"));

    [Fact]
    public void NonUnifiedNamesClassifyWithoutMerging()
    {
        Assert.Equal(FixtureDump.CherryLeaves, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_petals'"));
        Assert.Equal(FixtureDump.OakLeaves, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_oak_leaves_use'"));
        Assert.Equal("farmable", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.CherryLeaves}'"));
        Assert.Equal("farmable", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.OakLeaves}'"));
    }

    [Fact]
    public void MachineNamesAreNormalized()
    {
        Assert.Equal("Macerator", _fixture.Scalar<string>("SELECT machine FROM recipes WHERE id = 'r_macerate'"));
        Assert.Equal("Crafting Table", _fixture.Scalar<string>("SELECT machine FROM recipes WHERE id = 'r_block'"));
        Assert.Equal("Electric Blast Furnace", _fixture.Scalar<string>("SELECT machine FROM recipes WHERE id = 'r_ebf'"));
    }

    [Fact]
    public void FuelTabsAreExcluded() =>
        Assert.Equal(0, _fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_fuel'"));

    [Fact]
    public void SteamMachinesRelaxTheirMapsRecipesToEraZero() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.SteamIngot}'"));

    [Fact]
    public void VoltageTiersFollowTheLadder()
    {
        Assert.Equal(1, _fixture.Scalar<int>("SELECT tier FROM recipes WHERE id = 'r_ebf'"));
        Assert.Equal(1, _fixture.Scalar<int>("SELECT tier FROM recipes WHERE id = 'r_macerate'"));
        Assert.Equal(0, _fixture.Scalar<int>("SELECT tier FROM recipes WHERE id = 'r_smelt'"));
    }

    [Fact]
    public void GtTierLabelsAreAuthoritative()
    {
        Assert.Equal(1, TierLadder.LabelTier("ULV"));
        Assert.Equal(1, TierLadder.LabelTier("LV"));
        Assert.Equal(2, TierLadder.LabelTier("MV"));
        Assert.Equal(14, TierLadder.LabelTier("MAX"));
        Assert.Null(TierLadder.LabelTier(null));
        Assert.Null(TierLadder.LabelTier("bogus"));
    }

    [Fact]
    public void FilledCellsDecomposeIntoFluidAndNetOut()
    {
        Assert.Equal(FixtureDump.Water, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_electrolyze'"));
        Assert.Equal(1000, _fixture.Scalar<int>(
            "SELECT amount FROM recipe_inputs WHERE recipe_id = 'r_electrolyze'"));
        Assert.Equal(FixtureDump.Hydrogen, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_outputs WHERE recipe_id = 'r_electrolyze'"));
    }

    [Fact]
    public void AFluidSlotWithAlternativesShipsEachAtItsOwnAmount()
    {
        Assert.Equal(144, _fixture.Scalar<int>(
            $"SELECT amount FROM recipe_inputs WHERE recipe_id = 'r_fluid_choice' AND item_id = '{FixtureDump.Water}'"));
        Assert.Equal(1000, _fixture.Scalar<int>(
            $"SELECT amount FROM recipe_inputs WHERE recipe_id = 'r_fluid_choice' AND item_id = '{FixtureDump.Oxygen}'"));
        Assert.Equal(1, _fixture.Scalar<int>(
            $"""
             SELECT COUNT(DISTINCT slot) FROM recipe_inputs
             WHERE recipe_id = 'r_fluid_choice'
               AND item_id IN ('{FixtureDump.Water}', '{FixtureDump.Oxygen}')
             """));
    }

    [Fact]
    public void FluidInputsCarryMillibuckets() =>
        Assert.Equal(1000, _fixture.Scalar<int>(
            $"SELECT amount FROM recipe_inputs WHERE recipe_id = 'r_solidify' AND item_id = '{FixtureDump.Water}'"));

    [Fact]
    public void LeafClassesFollowOredict()
    {
        Assert.Equal("ingot", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.GtBronze}'"));
        Assert.Equal("dust", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.BronzeDust}'"));
        Assert.Equal("log", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.Log}'"));
        Assert.Equal("world_fluid", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.Water}'"));
        Assert.Equal("minable_block", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.ObsidianBlock}'"));
    }
}

public sealed class VoltageTierTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 1)]
    [InlineData(32, 1)]
    [InlineData(33, 2)]
    [InlineData(128, 2)]
    [InlineData(512, 3)]
    [InlineData(2048, 4)]
    [InlineData(2049, 5)]
    public void MatchesVoltageLadder(long euT, int expected) =>
        Assert.Equal(expected, TierLadder.VoltageTier(euT));
}
