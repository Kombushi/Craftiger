using System.ComponentModel.DataAnnotations;
using Craftiger.Api.Interfaces;
using Craftiger.Api.Models;
using Craftiger.Api.Repositories;
using Craftiger.Api.Services;
using Craftiger.Solver.Highs.Interfaces;
using Craftiger.Solver.Highs.Models;
using Craftiger.Solver.Highs.Services;
using Craftiger.Solver.Interfaces.Bom;
using Craftiger.Solver.Interfaces.Costs;
using Craftiger.Solver.Interfaces.Factory;
using Craftiger.Solver.Interfaces.Graph;
using Craftiger.Solver.Interfaces.Lp;
using Craftiger.Solver.Models.Options;
using Craftiger.Solver.Services.Bom;
using Craftiger.Solver.Services.Costs;
using Craftiger.Solver.Services.Factory;
using Craftiger.Solver.Services.Graph;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(nameof(ApiOptions)));
builder.Services.Configure<GarageRules>(builder.Configuration.GetSection(nameof(GarageRules)));
builder.Services.Configure<SolverPreferences>(builder.Configuration.GetSection(nameof(SolverPreferences)));
builder.Services.Configure<CostSolverOptions>(builder.Configuration.GetSection(nameof(CostSolverOptions)));
builder.Services.Configure<BomOptions>(builder.Configuration.GetSection(nameof(BomOptions)));
builder.Services.Configure<FactorySolverOptions>(builder.Configuration.GetSection(nameof(FactorySolverOptions)));
builder.Services.Configure<HighsOptions>(builder.Configuration.GetSection(nameof(HighsOptions)));

builder.Services.AddSingleton<IFactoryArtifactReader, FactoryArtifactReader>();
builder.Services.AddSingleton<IPlannerArtifactRepository, PlannerArtifactRepository>();
builder.Services.AddSingleton(provider => provider
    .GetRequiredService<IPlannerArtifactRepository>()
    .Load(provider.GetRequiredService<IOptions<ApiOptions>>().Value.ArtifactsDir));

builder.Services.AddSingleton<ILeafWeightService, LeafWeightService>();
builder.Services.AddSingleton<IGarageLegalityService, GarageLegalityService>();
builder.Services.AddSingleton<IRoutePreferenceService, RoutePreferenceService>();
builder.Services.AddSingleton<ICostSolverService, CostSolverService>();
builder.Services.AddSingleton<IChosenEdgeGraphService, ChosenEdgeGraphService>();
builder.Services.AddSingleton<ILoopSeedService, LoopSeedService>();
builder.Services.AddSingleton<IBomService, BomService>();
builder.Services.AddSingleton<IClosureService, ClosureService>();
builder.Services.AddSingleton<ISolveEntryCodec, SolveEntryCodec>();
builder.Services.AddSingleton<ISolveStore, ValkeySolveStore>();
builder.Services.AddSingleton<ISolveCacheService, SolveCacheService>();
builder.Services.AddSingleton<IPlannerQueryService, PlannerQueryService>();

// The factory solver stack, with the native HiGHS adapter as its LP engine.
builder.Services.AddSingleton<IHighsModelLoader, HighsModelLoader>();
builder.Services.AddSingleton<ILexicographicLayerRunner, LexicographicLayerRunner>();
builder.Services.AddSingleton<ILinearProgramSolver, HighsLinearProgramSolver>();
builder.Services.AddSingleton<IRunVariantService, RunVariantService>();
builder.Services.AddSingleton<IFactoryTargetService, FactoryTargetService>();
builder.Services.AddSingleton<IGeneratorCatalogService, GeneratorCatalogService>();
builder.Services.AddSingleton<ICandidateWalkService, CandidateWalkService>();
builder.Services.AddSingleton<IFactoryModelService, FactoryModelService>();
builder.Services.AddSingleton<IAutoInfiniteService, AutoInfiniteService>();
builder.Services.AddSingleton<IFactoryDiagnosisService, FactoryDiagnosisService>();
builder.Services.AddSingleton<IFactoryPlanInterpreter, FactoryPlanInterpreter>();
builder.Services.AddSingleton<IFactorySolverService, FactorySolverService>();
builder.Services.AddSingleton<IFactoryRequestService, FactoryRequestService>();
builder.Services.AddSingleton<IFactoryPlanCodec, FactoryPlanCodec>();
builder.Services.AddSingleton<IFactoryCacheService, FactoryCacheService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Load the artifact and connect the store eagerly: a wrong schema_version, a missing connection string or an unreachable Valkey refuse at startup, not on the first request.
app.Services.GetRequiredService<PlannerArtifact>();
app.Services.GetRequiredService<ISolveStore>();

// Validation problems surface as 400s rather than 500s.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (ValidationException e)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = e.Message });
    }
});

app.MapGet("/api/meta", (IPlannerQueryService query) => query.Meta());

app.MapPost("/api/solve", (SolveRequest request, ISolveCacheService cache) => cache.SolveAsync(request));

app.MapGet("/api/list", async (
    string solveId, int? page, int? pageSize, bool? hideUnreachable,
    ISolveCacheService cache, IPlannerQueryService query) =>
    await cache.GetAsync(solveId) is { } entry
        ? Results.Ok(query.List(
            entry, Math.Max(0, page ?? 0), Math.Clamp(pageSize ?? 100, 1, 500),
            hideUnreachable ?? false))
        : Results.NotFound());

// Search works before the first solve; without a solveId every cost is null.
app.MapGet("/api/search", async (
    string q, string? solveId, ISolveCacheService cache, IPlannerQueryService query) =>
    solveId is null
        ? Results.Ok(query.Search(null, q))
        : await cache.GetAsync(solveId) is { } entry ? Results.Ok(query.Search(entry, q)) : Results.NotFound());

app.MapGet("/api/item/{id}", async (
    string id, string solveId, ISolveCacheService cache, IPlannerQueryService query) =>
    await cache.GetAsync(solveId) is { } entry
        ? query.ItemDetail(entry, id) is { } detail ? Results.Ok(detail) : Results.NotFound()
        : Results.NotFound());

app.MapGet("/api/machines", (string targets, bool? deep, IPlannerQueryService query) =>
    query.Machines(
        targets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        deep ?? false));

app.MapPost("/api/bom", async (
    BomRequest request, ISolveCacheService cache, IPlannerQueryService query) =>
    await cache.GetAsync(request.SolveId) is { } entry
        ? Results.Ok(query.Bom(entry, request))
        : Results.NotFound());

app.MapPost("/api/factory/solve", (FactorySolveRequest request, IFactoryCacheService cache) =>
    cache.SolveAsync(request));

app.MapPost("/api/factory/generators", (GeneratorCatalogRequest request, IFactoryCacheService cache) =>
    cache.GeneratorsAsync(request));

// The pipeline picker's producer list: like /api/item, but farm-scoped rows included.
app.MapPost("/api/factory/producers", async (
    FactoryProducersRequest request, ISolveCacheService cache, IPlannerQueryService query) =>
{
    var solve = await cache.SolveAsync(new SolveRequest(request.Garage, request.B, request.Weights));
    return await cache.GetAsync(solve.SolveId) is { } entry
        && query.ItemDetail(entry, request.ItemId, allScopes: true) is { } detail
            ? Results.Ok(detail)
            : Results.NotFound();
});

app.MapGet("/atlas.webp", (IOptions<ApiOptions> options) => StaticArtifact(options.Value.ArtifactsDir, "atlas.webp", "image/webp"));
app.MapGet("/atlas-offsets.json", (IOptions<ApiOptions> options) => StaticArtifact(options.Value.ArtifactsDir, "atlas-offsets.json", "application/json"));

// Both probes are bare: the artifact loads and the store connects eagerly, so a live process is ready.
app.MapHealthChecks("/livez");
app.MapHealthChecks("/readyz");

await app.RunAsync();

return;

static IResult StaticArtifact(string artifactsDir, string name, string contentType)
{
    var path = Path.GetFullPath(Path.Combine(artifactsDir, name));
    return File.Exists(path) ? Results.File(path, contentType) : Results.NotFound();
}

/// <summary>Exposes the entry point to WebApplicationFactory-based tests.</summary>
public partial class Program;
