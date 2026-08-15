using Dapper;
using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Craftiger.Builder.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Craftiger.Builder.UnitTests;

public sealed class BuilderPipelineFixture : IDisposable
{
    public string PlannerPath { get; }
    private readonly string _directory;

    public BuilderPipelineFixture()
    {
        _directory = Directory.CreateTempSubdirectory("craftiger-tests").FullName;
        var dumpPath = FixtureDump.Create(_directory);

        using var services = new ServiceCollection().AddBuilderServices().BuildServiceProvider();
        services.GetRequiredService<IBuilderPipeline>().Run(new BuilderOptions(
            dumpPath, _directory, "fixture-pack", Path.Combine(_directory, "image.zip"), ExplainItem: null));

        PlannerPath = Path.Combine(_directory, "planner.sqlite");
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    public T Scalar<T>(string sql)
    {
        using var db = new SqliteConnection($"Data Source={PlannerPath};Mode=ReadOnly");
        return db.ExecuteScalar<T>(sql)!;
    }
}

public sealed class BuilderPipelineTests : IClassFixture<BuilderPipelineFixture>
{
    private readonly BuilderPipelineFixture _fixture;

    public BuilderPipelineTests(BuilderPipelineFixture fixture) => _fixture = fixture;

    [Fact]
    public void BronzeSmeltsAtTierZeroDespiteBlockCycle() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.GtBronze}'"));

    [Fact]
    public void AluminiumTiersFromMultiAmpEbfNotFromBlockCycle() =>
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.AluIngot}'"));

    [Fact]
    public void MixedMapsDiscountOnceTheirMultiblockIsReachable() =>
        Assert.Equal(2, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.MixIngot}'"));

    [Fact]
    public void MixedMapsKeepFullTierWhileOnlyTheirSingleBlocksAreAffordable() =>
        Assert.Equal(3, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.DearIngot}'"));

    [Fact]
    public void EbfRecipeKeepsHeat() =>
        Assert.Equal(1700, _fixture.Scalar<int>("SELECT heat FROM recipes WHERE id = 'r_ebf'"));

    [Fact]
    public void ChancedOutputKeepsChance() =>
        Assert.Equal(0.9, _fixture.Scalar<double>(
            "SELECT chance FROM recipe_outputs WHERE recipe_id = 'r_macerate' AND chance < 1"), precision: 9);

    [Fact]
    public void CatalystsAreStripped()
    {
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM recipe_inputs WHERE item_id IN ('{FixtureDump.Saw}', '{FixtureDump.Mold}')"));
        Assert.Equal(1, _fixture.Scalar<int>("SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = 'r_planks'"));
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
    public void NonSpawningOresContributeNoEra() =>
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
        Assert.Equal("free_fluid", _fixture.Scalar<string>(
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
