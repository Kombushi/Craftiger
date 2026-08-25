using System.Net;
using System.Net.Http.Json;
using Craftiger.Api.Models;
using Craftiger.Solver.Models.Bom;

namespace Craftiger.Api.UnitTests;

public sealed class ApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private HttpClient Client => fixture.Client;

    private static readonly object _hvGarage = new
    {
        defaultTier = 3,
        coils = new Dictionary<string, string> { ["Electric Blast Furnace"] = "Kanthal" }
    };

    private async Task<string> SolveAsync(object? garage = null, double b = 4)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/solve", new { garage = garage ?? _hvGarage, b });
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
        Assert.True(meta.Machines.Single(machine => machine.Name == "Electric Blast Furnace").HeatGated);
        Assert.False(meta.Machines.Single(machine => machine.Name == "Wiremill").HeatGated);
        Assert.True(meta.Machines.Single(machine => machine.Name == "Electric Blast Furnace").MultiblockOnly);
        Assert.False(meta.Machines.Single(machine => machine.Name == "Wiremill").MultiblockOnly);
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

        Assert.Equal(10, list!.Total);
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
            coils = new Dictionary<string, string> { ["Electric Blast Furnace"] = "Kanthal" }
        });

        var full = await Client.GetFromJsonAsync<ListResponse>(
            $"/api/list?solveId={solveId}&pageSize=10");
        var reachable = await Client.GetFromJsonAsync<ListResponse>(
            $"/api/list?solveId={solveId}&pageSize=10&hideUnreachable=true");

        Assert.Equal(10, full!.Total);
        Assert.Contains(full.Items, item => item.ItemId == "rod" && item.Cost is null);
        Assert.Null(full.Items[^1].Cost);
        Assert.Equal(7, reachable!.Total);
        Assert.DoesNotContain(reachable.Items, item => item.ItemId == "rod");
        Assert.DoesNotContain(reachable.Items, item => item.ItemId == "saw");
    }

    [Fact]
    public async Task ALateMachineIsNotOwnedByDefault()
    {
        var mvDefault = await SolveAsync(new { defaultTier = 2 });
        var explicitlyBuilt = await SolveAsync(new
        {
            defaultTier = 2,
            machines = new Dictionary<string, int?> { ["Circuit Assembly Line"] = 2 }
        });
        var hvDefault = await SolveAsync(new { defaultTier = 3 });

        var locked = await Client.GetFromJsonAsync<ItemDetailResponse>($"/api/item/chip?solveId={mvDefault}");
        var built = await Client.GetFromJsonAsync<ItemDetailResponse>($"/api/item/chip?solveId={explicitlyBuilt}");
        var reached = await Client.GetFromJsonAsync<ItemDetailResponse>($"/api/item/chip?solveId={hvDefault}");

        Assert.Null(locked!.Cost);
        Assert.Empty(locked.Recipes);
        Assert.NotNull(built!.Cost);
        Assert.NotNull(reached!.Cost);
    }

    [Fact]
    public async Task MachinesCarryTheirEra()
    {
        var meta = await Client.GetFromJsonAsync<MetaResponse>("/api/meta");

        Assert.Equal(3, meta!.Machines.Single(machine => machine.Name == "Circuit Assembly Line").Era);
        Assert.Equal(0, meta.Machines.Single(machine => machine.Name == "Wiremill").Era);
        Assert.Null(meta.Machines.Single(machine => machine.Name == "Extruder").Era);
    }

    [Fact]
    public async Task AnUnknownEraStaysOwnedByDefault()
    {
        var solveId = await SolveAsync(new { defaultTier = 2 });

        var rod = await Client.GetFromJsonAsync<ItemDetailResponse>($"/api/item/rod?solveId={solveId}");

        Assert.NotNull(rod!.Cost);
    }

    [Fact]
    public async Task SearchFindsByAlias()
    {
        var solveId = await SolveAsync();

        var results = await Client.GetFromJsonAsync<List<ItemSummaryDto>>(
            $"/api/search?q=Ferrum&solveId={solveId}");

        Assert.NotNull(results);
        Assert.Equal("ing", results.Single().ItemId);
        Assert.Equal(4, results.Single().Cost);
    }

    [Fact]
    public async Task SearchRanksTheCheapestMatchFirstThenByName()
    {
        var solveId = await SolveAsync();

        var results = await Client.GetFromJsonAsync<List<ItemSummaryDto>>($"/api/search?q=ir&solveId={solveId}");

        Assert.NotNull(results);
        Assert.Equal(["Iron Nugget", "Iron Wire", "Iron Ingot"], results.Select(item => item.Name));
    }

    [Fact]
    public async Task SearchFoldsCaseOnEveryScript()
    {
        var byIndex = await Client.GetFromJsonAsync<List<ItemSummaryDto>>("/api/search?q=L%C3%96TZ");
        var byScan = await Client.GetFromJsonAsync<List<ItemSummaryDto>>("/api/search?q=%C3%96");

        Assert.Equal("sil", Assert.Single(byIndex ?? []).ItemId);
        Assert.Equal("sil", Assert.Single(byScan ?? []).ItemId);
    }

    [Fact]
    public async Task SearchWorksBeforeAnySolve()
    {
        var results = await Client.GetFromJsonAsync<List<ItemSummaryDto>>("/api/search?q=Ferrum");

        Assert.NotNull(results);
        Assert.Equal("ing", results.Single().ItemId);
        Assert.Null(results[0].Cost);
    }

    [Fact]
    public async Task AnItemNothingProducesReadsAsUncraftable()
    {
        var saw = Assert.Single(await Client.GetFromJsonAsync<List<ItemSummaryDto>>("/api/search?q=Test%20Saw") ?? []);
        var wire = Assert.Single(await Client.GetFromJsonAsync<List<ItemSummaryDto>>("/api/search?q=Iron%20Wire") ?? []);
        var silver = Assert.Single(await Client.GetFromJsonAsync<List<ItemSummaryDto>>("/api/search?q=Silver%20Ingot") ?? []);

        Assert.True(saw.Uncraftable);
        Assert.False(wire.Uncraftable);
        Assert.False(silver.Uncraftable);
    }

    [Fact]
    public async Task ItemDetailShowsLegalRecipesWithCandidates()
    {
        var solveId = await SolveAsync();

        var detail = await Client.GetFromJsonAsync<ItemDetailResponse>(
            $"/api/item/wire?solveId={solveId}");

        Assert.Equal(2, detail!.Cost);
        Assert.Equal("r_wire", detail.BestRecipeId);
        var recipe = Assert.Single(detail.Recipes);
        Assert.Equal("r_wire", recipe.RecipeId);
        Assert.Equal("Wiremill", recipe.Machine);
        Assert.Equal(2, recipe.CandidateCost);
        Assert.Equal(4, Assert.Single(Assert.Single(recipe.Slots)).Cost);
        var catalyst = Assert.Single(Assert.Single(recipe.Catalysts));
        Assert.Equal("saw", catalyst.ItemId);
        Assert.Null(catalyst.Cost);
        Assert.Equal(4, detail.Items["ing"].Cost);
        Assert.Equal("Test Saw", detail.Items["saw"].Name);
        Assert.False(detail.Items["ing"].IsFluid);
        Assert.Equal(64, detail.Items["ing"].MaxStack);
        Assert.Equal(1, detail.Items["saw"].MaxStack);
    }

    [Fact]
    public async Task ItemRefsCarryDisplayAliasesOnly()
    {
        var solveId = await SolveAsync();

        var detail = await Client.GetFromJsonAsync<ItemDetailResponse>(
            $"/api/item/wire?solveId={solveId}");

        Assert.Equal(["Ferrum Ingot"], detail!.Items["ing"].Aliases);
        Assert.Null(detail.Items["wire"].Aliases);
    }

    [Fact]
    public async Task TheHatchBonusStretchesTheEbfCoil()
    {
        var cupronickel = new Dictionary<string, string> { ["Electric Blast Furnace"] = "Cupronickel" };
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
                coils = new Dictionary<string, string> { ["Electric Blast Furnace"] = "Adamantium" }
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
        var result = await response.Content.ReadFromJsonAsync<BomResponse>();

        Assert.Equal(1.5, Assert.Single(result!.Leaves).Amount);
        Assert.Equal("ing", result.Leaves[0].ItemId);
        Assert.Equal("r_wire", result.Targets.Single().RecipeId);
        Assert.Contains(result.Warnings, warning => warning is { Kind: BomWarningKind.PinUnknown, ItemId: "hot" });

        var node = Assert.Single(result.Nodes);
        Assert.Equal("wire", node.ItemId);
        Assert.Equal("Wiremill", node.Machine);
        Assert.Equal(1.5, node.Runs);
        Assert.Equal(2, node.WholeRuns);
        Assert.Equal(2, result.Leaves[0].WholeAmount);
        Assert.Equal("ing", Assert.Single(node.InputsPerRun).ItemId);
        Assert.Equal("saw", Assert.Single(node.Catalysts).ItemId);
        Assert.Null(result.Items["saw"].Cost);
        Assert.Equal(4, result.Items["ing"].Cost);
        Assert.Equal(2, result.Items["wire"].Cost);
    }

    [Fact]
    public async Task AToolFreeRouteWinsTheTieOverTheWearingTool()
    {
        var solveId = await SolveAsync();

        var detail = await Client.GetFromJsonAsync<ItemDetailResponse>($"/api/item/frame?solveId={solveId}");

        Assert.Equal(4, detail!.Cost);
        Assert.Equal("r_frame_asm", detail.BestRecipeId);
        Assert.Equal(2, detail.Recipes.Count);
        Assert.All(detail.Recipes, recipe => Assert.Equal(4, recipe.CandidateCost));
    }

    [Fact]
    public async Task AShapedRecipeCarriesItsGridAndOthersDoNot()
    {
        var solveId = await SolveAsync();

        var detail = await Client.GetFromJsonAsync<ItemDetailResponse>($"/api/item/frame?solveId={solveId}");

        var hand = detail!.Recipes.Single(recipe => recipe.RecipeId == "r_frame_hand");
        var assembled = detail.Recipes.Single(recipe => recipe.RecipeId == "r_frame_asm");
        Assert.Equal([0, null, null, 0, 1, null, null, null, null], hand.Grid);
        Assert.Null(assembled.Grid);

        var response = await Client.PostAsJsonAsync("/api/bom", new
        {
            solveId,
            targets = new[] { new { itemId = "frame", count = 1 } },
            pins = new Dictionary<string, string> { ["frame"] = "r_frame_hand" },
        });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BomResponse>();

        var node = Assert.Single(result!.Nodes);
        Assert.Equal("r_frame_hand", node.RecipeId);
        Assert.Equal([0, null, null, 0, 1, null, null, null, null], node.Grid);
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
