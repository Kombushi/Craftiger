using Gtnh.Planner.Builder;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gtnh.Planner.Tests.Builder;

public sealed class BuilderPipelineFixture : IDisposable
{
    public string PlannerPath { get; }
    private readonly string _directory;

    public BuilderPipelineFixture()
    {
        _directory = Directory.CreateTempSubdirectory("craftiger-tests").FullName;
        var dumpPath = FixtureDump.Create(_directory);

        var config = BuilderConfig.Default;
        var dump = DumpReader.Read(dumpPath);
        var unified = Unification.Run(dump, config);
        var recipes = RecipeTransform.Run(dump, unified, config);
        var itemIds = PlannerWriter.CollectItemIds(recipes);
        var leafClasses = LeafTagging.Run(itemIds, dump, unified, config);
        var ingotTiers = IngotTiers.Run(recipes, leafClasses, unified, config).Tiers;

        PlannerPath = Path.Combine(_directory, "planner.sqlite");
        PlannerWriter.Write(PlannerPath, dump, unified, recipes, leafClasses, ingotTiers, config, "fixture-pack");
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    public T Scalar<T>(string sql)
    {
        using var db = new SqliteConnection($"Data Source={PlannerPath};Mode=ReadOnly");
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        return (T)Convert.ChangeType(cmd.ExecuteScalar()!, typeof(T));
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
    public void MultiAmpMachinesTierAtFourAmps()
    {
        Assert.Equal(1, RecipeTransform.VoltageTier(120, amps: 4));
        Assert.Equal(2, RecipeTransform.VoltageTier(120));
        Assert.Equal(2, RecipeTransform.VoltageTier(480, amps: 4));
        Assert.Equal(3, RecipeTransform.VoltageTier(480));
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
        Assert.Equal(expected, RecipeTransform.VoltageTier(euT));
}