namespace Craftiger.Builder.UnitTests;

/// <summary>The Tree Growth Simulator's synthesized rows: sapling and best tools in place, tier-1 amounts, the output ladder named, and its logs no longer primitive.</summary>
public sealed class TreeFarmTests(BuilderPipelineFixture fixture) : IClassFixture<BuilderPipelineFixture>
{
    private const string RecipeId = "r_tree_oak";

    [Fact]
    public void TheRowShipsOnceAtLvWithItsLadderNamed()
    {
        Assert.Equal(1, fixture.Scalar<int>($"SELECT COUNT(*) FROM recipes WHERE id = '{RecipeId}'"));
        Assert.Equal("Tree Growth Simulator", fixture.Scalar<string>($"SELECT machine FROM recipes WHERE id = '{RecipeId}'"));
        Assert.Equal(1, fixture.Scalar<long>($"SELECT tier FROM recipes WHERE id = '{RecipeId}'"));
        Assert.Equal(100, fixture.Scalar<long>($"SELECT duration_ticks FROM recipes WHERE id = '{RecipeId}'"));
        // LV's practical voltage, 30/32 of 32.
        Assert.Equal(30, fixture.Scalar<long>($"SELECT eu_t FROM recipes WHERE id = '{RecipeId}'"));
        Assert.Equal("TREE_FARM", fixture.Scalar<string>($"SELECT overclock FROM recipes WHERE id = '{RecipeId}'"));
        Assert.Equal(0, fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id != 'r_tree_oak' AND overclock IS NOT NULL"));
    }

    [Fact]
    public void OutputsCarryTheTierAndBestToolMultipliers()
    {
        // Two logs x 5 at tier 1 x 4 for the chainsaw; five saplings x 5 x 1 for the branch cutter; two leaves x 5 x 4 for the electric wire cutter.
        Assert.Equal(40, fixture.Scalar<long>(
            $"SELECT amount FROM recipe_outputs WHERE recipe_id = '{RecipeId}' AND item_id = '{FixtureDump.Log}'"));
        Assert.Equal(25, fixture.Scalar<long>(
            $"SELECT amount FROM recipe_outputs WHERE recipe_id = '{RecipeId}' AND item_id = '{FixtureDump.OakSapling}'"));
        Assert.Equal(40, fixture.Scalar<long>(
            $"SELECT amount FROM recipe_outputs WHERE recipe_id = '{RecipeId}' AND item_id = '{FixtureDump.OakLeaves}'"));
    }

    [Fact]
    public void TheSaplingAndTheBestToolsAreCatalysts()
    {
        Assert.Equal(0, fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = '{RecipeId}' AND catalyst = 0"));
        Assert.Equal(1, fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = '{RecipeId}' AND item_id = '{FixtureDump.OakSapling}' AND slot = 0 AND catalyst = 1 AND tool = 0"));
        Assert.Equal(1, fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = '{RecipeId}' AND item_id = '{FixtureDump.Chainsaw}' AND slot = 1 AND catalyst = 1 AND tool = 1"));
        Assert.Equal(1, fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = '{RecipeId}' AND item_id = '{FixtureDump.BranchCutter}' AND slot = 2 AND tool = 1"));
        Assert.Equal(1, fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = '{RecipeId}' AND item_id = '{FixtureDump.WireCutterLv}' AND slot = 3 AND tool = 1"));
        // The plain saw multiplies logs by one and never ships beside the chainsaw.
        Assert.Equal(0, fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = '{RecipeId}' AND item_id = '{FixtureDump.Saw}'"));
    }

    [Fact]
    public void ConjuredLogsAreDerivedWhileUnfarmedLogsStaySeeds()
    {
        Assert.Equal("log", fixture.Scalar<string>($"SELECT leaf_class FROM items WHERE id = '{FixtureDump.Log}'"));
        Assert.Equal(0, fixture.Scalar<int>($"SELECT COUNT(*) FROM renewable_seeds WHERE item_id = '{FixtureDump.Log}'"));
        Assert.Equal(0, fixture.Scalar<int>($"SELECT COUNT(*) FROM renewable_seeds WHERE item_id = '{FixtureDump.OakSapling}'"));
        Assert.Equal("FARM", fixture.Scalar<string>($"SELECT kind FROM renewable_seeds WHERE item_id = '{FixtureDump.PineLog}'"));
    }
}
