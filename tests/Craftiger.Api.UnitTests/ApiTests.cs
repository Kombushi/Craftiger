using System.Net;
using System.Net.Http.Json;
using Craftiger.Api.Models;
using Craftiger.Solver.Models;

namespace Craftiger.Api.UnitTests;

public sealed class ApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private HttpClient Client => fixture.Client;

    private static readonly object HvGarage = new
    {
        defaultTier = 3,
        coils = new Dictionary<string, string> { ["Blast Furnace"] = "Kanthal" }
    };

    private async Task<string> SolveAsync(object? garage = null, double b = 4)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/solve", new { garage = garage ?? HvGarage, b });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SolveResponse>())!.SolveId;
    }

    [Fact]
    public async Task MetaDescribesThePack()
    {
        var meta = await Client.GetFromJsonAsync<MetaResponse>("/api/meta");

        Assert.Equal("test-pack", meta!.PackVersion);
        Assert.Equal(["Steam", "LV", "MV", "HV"], meta.TierNames);
        Assert.Equal(2, meta.Coils.Count);
        Assert.True(meta.Machines.Single(machine => machine.Name == "Blast Furnace").HeatGated);
        Assert.False(meta.Machines.Single(machine => machine.Name == "Wiremill").HeatGated);
        Assert.Equal(32, meta.Atlas!.Cell);
    }

    [Fact]
    public async Task SolvingTwiceLandsOnTheSameId()
    {
        Assert.Equal(await SolveAsync(), await SolveAsync());
        Assert.NotEqual(await SolveAsync(), await SolveAsync(b: 5));
    }

    [Fact]
    public async Task TheListSortsCheapestFirst()
    {
        var solveId = await SolveAsync();

        var list = await Client.GetFromJsonAsync<ListResponse>(
            $"/api/list?solveId={solveId}&pageSize=10");

        Assert.Equal(6, list!.Total);
        Assert.Equal("nug", list.Items[0].ItemId);
        Assert.Equal(4.0 / 9, list.Items[0].Cost!.Value, 9);
        Assert.Equal("wire", list.Items[1].ItemId);
    }

    [Fact]
    public async Task HidingUnreachableDropsTheGrayTail()
    {
        var solveId = await SolveAsync(new
        {
            defaultTier = 3,
            machines = new Dictionary<string, int?> { ["Extruder"] = null },
            coils = new Dictionary<string, string> { ["Blast Furnace"] = "Kanthal" }
        });

        var full = await Client.GetFromJsonAsync<ListResponse>(
            $"/api/list?solveId={solveId}&pageSize=10");
        var reachable = await Client.GetFromJsonAsync<ListResponse>(
            $"/api/list?solveId={solveId}&pageSize=10&hideUnreachable=true");

        Assert.Equal(6, full!.Total);
        Assert.Equal("rod", full.Items[^1].ItemId);
        Assert.Null(full.Items[^1].Cost);
        Assert.Equal(5, reachable!.Total);
        Assert.DoesNotContain(reachable.Items, item => item.ItemId == "rod");
    }

    [Fact]
    public async Task SearchFindsByAlias()
    {
        var solveId = await SolveAsync();

        var results = await Client.GetFromJsonAsync<List<ItemSummaryDto>>(
            $"/api/search?q=Ferrum&solveId={solveId}");

        Assert.Equal("ing", results!.Single().ItemId);
        Assert.Equal(4, results.Single().Cost);
    }

    [Fact]
    public async Task ItemDetailShowsLegalRecipesWithCandidates()
    {
        var solveId = await SolveAsync();

        var detail = await Client.GetFromJsonAsync<ItemDetailResponse>(
            $"/api/item/wire?solveId={solveId}");

        Assert.Equal(2, detail!.Cost);
        var recipe = Assert.Single(detail.Recipes);
        Assert.Equal("r_wire", recipe.RecipeId);
        Assert.Equal("Wiremill", recipe.Machine);
        Assert.Equal(2, recipe.CandidateCost);
        Assert.Equal(4, Assert.Single(Assert.Single(recipe.Slots)).Cost);
    }

    [Fact]
    public async Task TheHatchBonusStretchesTheEbfCoil()
    {
        var cupronickel = new Dictionary<string, string> { ["Blast Furnace"] = "Cupronickel" };
        var hv = await SolveAsync(new { defaultTier = 3, coils = cupronickel });
        var mv = await SolveAsync(new { defaultTier = 2, coils = cupronickel });

        var atHv = await Client.GetFromJsonAsync<ItemDetailResponse>($"/api/item/hot?solveId={hv}");
        var atMv = await Client.GetFromJsonAsync<ItemDetailResponse>($"/api/item/hot?solveId={mv}");

        Assert.Equal(4, atHv!.Cost);
        Assert.Null(atMv!.Cost);
        Assert.Empty(atMv.Recipes);
    }

    [Fact]
    public async Task AnUnknownCoilIsRejected()
    {
        var response = await Client.PostAsJsonAsync("/api/solve", new
        {
            garage = new
            {
                defaultTier = 3,
                coils = new Dictionary<string, string> { ["Blast Furnace"] = "Adamantium" }
            },
            b = 4
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnUnknownSolveIdIsGone()
    {
        var response = await Client.GetAsync("/api/list?solveId=evicted");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheBomFlowsThroughTheSolver()
    {
        var solveId = await SolveAsync();

        var response = await Client.PostAsJsonAsync("/api/bom", new
        {
            solveId,
            targets = new[] { new { itemId = "wire", count = 3 } },
            pins = new Dictionary<string, string> { ["hot"] = "bogus" }
        });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BomResult>();

        Assert.Equal(1.5, Assert.Single(result!.Leaves).Amount);
        Assert.Equal("ing", result.Leaves[0].ItemId);
        Assert.Equal("r_wire", result.Targets.Single().RecipeId);
        Assert.Contains(result.Warnings, warning => warning is { Kind: "pin_unknown", ItemId: "hot" });
    }

    [Fact]
    public async Task MachinesComeFromTheUpstreamClosure()
    {
        var machines = await Client.GetFromJsonAsync<List<string>>("/api/machines?targets=wire");

        Assert.Equal(["Wiremill"], machines);
    }

    [Fact]
    public async Task TheOffsetsFileIsServedAndTheMissingAtlasIsNot()
    {
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/atlas-offsets.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/atlas.webp")).StatusCode);
    }

    [Fact]
    public void AWrongSchemaVersionRefusesToStart()
    {
        var dir = Path.Combine(Path.GetTempPath(), "craftiger-api-v1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            ApiFixture.WriteArtifact(Path.Combine(dir, "planner.sqlite"), schemaVersion: 1);
            using var factory = ApiFixture.Create(dir);

            var refusal = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

            Assert.Contains("schema_version", refusal.ToString());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(dir, recursive: true);
        }
    }
}
