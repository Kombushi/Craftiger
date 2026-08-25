using System.Text.Json;

namespace Craftiger.Builder.UnitTests;

/// <summary>The steam carrier's synthesized rows, run with the water id pointed at the fixture's own water fluid.</summary>
public sealed class SteamSynthesisFixture : IDisposable
{
    private readonly FixtureRun _run = new(
        new KeyValuePair<string, string?>("SteamConfiguration:WaterFluidId", FixtureDump.Water));

    public T Scalar<T>(string sql) => _run.Scalar<T>(sql);

    public void Dispose() => _run.Dispose();
}

public class SteamSynthesisTests(SteamSynthesisFixture fixture) : IClassFixture<SteamSynthesisFixture>
{
    [Fact]
    public void BoilerRecipeBoilsWaterIntoSteam()
    {
        var recipeId = $"gtboil~{FixtureDump.BronzeBoiler}~{FixtureDump.BronzeDust}";
        // Bronze burns the fixture fuel for 2 s at 480 EU/t: 40 ticks x 960 L/t of steam.
        Assert.Equal(40, fixture.Scalar<long>(
            $"SELECT duration_ticks FROM recipes WHERE id = '{recipeId}'"));
        Assert.Equal(0, fixture.Scalar<long>(
            $"SELECT eu_t FROM recipes WHERE id = '{recipeId}'"));
        Assert.Equal(38400, fixture.Scalar<long>(
            $"SELECT amount FROM recipe_outputs WHERE recipe_id = '{recipeId}' AND item_id = '{FixtureDump.Ic2Steam}'"));
        Assert.Equal(240, fixture.Scalar<long>(
            $"SELECT amount FROM recipe_inputs WHERE recipe_id = '{recipeId}' AND item_id = '{FixtureDump.Water}'"));
        Assert.Equal(1, fixture.Scalar<long>(
            $"SELECT amount FROM recipe_inputs WHERE recipe_id = '{recipeId}' AND item_id = '{FixtureDump.BronzeDust}'"));
        Assert.Equal("Large Bronze Boiler", fixture.Scalar<string>(
            $"SELECT machine FROM recipes WHERE id = '{recipeId}'"));
        Assert.Equal(1, fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM machine_items WHERE map = 'Large Bronze Boiler' AND item_id = '{FixtureDump.BronzeBoiler}' AND multiblock = 1"));
    }

    [Fact]
    public void SteamTurbineShipsItsRowAndPseudoFuel()
    {
        Assert.Equal(1, fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM machine_items WHERE map = 'Steam Turbine' AND item_id = '{FixtureDump.SteamTurbine}' AND tier = 1 AND multiblock = 0"));
        Assert.Equal(0.5, fixture.Scalar<double>(
            $"SELECT eu_per_unit FROM fuels WHERE map = 'Steam Turbine' AND item_id = '{FixtureDump.Ic2Steam}'"));
        // The Railcraft steam id is not in the fixture dump and must not ship a row.
        Assert.Equal(1, fixture.Scalar<int>(
            "SELECT COUNT(*) FROM fuels WHERE map = 'Steam Turbine'"));
    }

    [Fact]
    public void TheCarrierShipsInMeta()
    {
        // Only the steam the dump knows is listed, and the fixture has no distilled water.
        var steam = JsonSerializer.Deserialize<JsonElement>(
            fixture.Scalar<string>("SELECT value FROM meta WHERE key = 'steam'"));
        Assert.Equal([FixtureDump.Ic2Steam], steam.GetProperty("SteamFluidIds").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(JsonValueKind.Null, steam.GetProperty("DistilledWaterId").ValueKind);
        Assert.Equal(0.5, steam.GetProperty("EuPerLiter").GetDouble());
        Assert.Equal(160, steam.GetProperty("WaterPerSteam").GetInt64());
    }
}
