using Dapper;
using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Craftiger.Builder.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

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

/// <summary>The same pack with the recycling exclusion switched off, so the arc-furnace loop
/// the fixture carries actually ships. Proves the price check reports a leak when there is one,
/// rather than reporting none because it never looks.</summary>
public sealed class LeakyPipelineFixture : IDisposable
{
    private readonly FixtureRun _run =
        new(new KeyValuePair<string, string?>(
            "BuilderConfig:RecyclingCategorySuffixes:0", "matches-no-category"));

    public void Dispose() => _run.Dispose();
    public T Scalar<T>(string sql) => _run.Scalar<T>(sql);
}

/// <summary>The same pack with one recipe condemned as a phantom registration, proving the
/// exclusion reaches the artifact.</summary>
public sealed class PhantomRecipeFixture : IDisposable
{
    private readonly FixtureRun _run =
        new(new KeyValuePair<string, string?>(
            "BuilderConfig:PhantomRecipeIds:r_melt", "fixture-only condemnation"));

    public void Dispose() => _run.Dispose();
    public T Scalar<T>(string sql) => _run.Scalar<T>(sql);
}

public sealed class PhantomRecipeTests(PhantomRecipeFixture fixture) : IClassFixture<PhantomRecipeFixture>
{
    [Fact]
    public void APhantomRegistrationNeverReachesTheArtifact() =>
        Assert.Equal(0, fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_melt'"));
}

/// <summary>The same pack with the fixture pipe listed as untagged recycling, proving its GT
/// consumers drop while its crafting-grid consumers survive.</summary>
public sealed class UntaggedRecyclingFixture : IDisposable
{
    private readonly FixtureRun _run =
        new(new KeyValuePair<string, string?>(
            "BuilderConfig:UntaggedRecyclingInputItems:0", "Fixture Pipe"));

    public void Dispose() => _run.Dispose();
    public T Scalar<T>(string sql) => _run.Scalar<T>(sql);
}

public sealed class UntaggedRecyclingTests(UntaggedRecyclingFixture fixture)
    : IClassFixture<UntaggedRecyclingFixture>
{
    [Fact]
    public void AListedItemsGtConsumersNeverShip()
    {
        Assert.Equal(0, fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_pipe_grind'"));
        Assert.Equal(1, fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_pipe_block'"));
    }
}

/// <summary>The same pack with an era floor under the mixer, proving a quest-anchored gate
/// outranks everything the recipe graph derives.</summary>
public sealed class EraFloorFixture : IDisposable
{
    private readonly FixtureRun _run =
        new(new KeyValuePair<string, string?>("BuilderConfig:MachineEraFloors:Mixer", "5"));

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
            "SELECT era IS NULL FROM machine_eras WHERE machine = 'Blast Furnace'"));
    }

    [Fact]
    public void ArtifactStampsItsSchemaVersion() =>
        Assert.Equal(Craftiger.Builder.Repositories.PlannerRepository.SchemaVersion.ToString(),
            _fixture.Scalar<string>("SELECT value FROM meta WHERE key = 'schema_version'"));

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
            "SELECT COUNT(*) FROM items WHERE leaf_class IN ('dust_small', 'dust_tiny', 'nugget') " +
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
    public void AnUnlistedPipeGrinderStillShips() =>
        Assert.Equal(1, _fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_pipe_grind'"));

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
    public void WildcardGroupingOredictsDoNotUnify()
    {
        Assert.Equal(FixtureDump.IronIngot, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_iron_use'"));
        Assert.Equal(FixtureDump.CastIron, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_cast_use'"));
    }

    [Fact]
    public void AcceptListOredictsClassifyWithoutUnifying()
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
        Assert.Equal("Blast Furnace", _fixture.Scalar<string>("SELECT machine FROM recipes WHERE id = 'r_ebf'"));
    }

    [Fact]
    public void FuelTabsAreExcluded() =>
        Assert.Equal(0, _fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_fuel'"));

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
