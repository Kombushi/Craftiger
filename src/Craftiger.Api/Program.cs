using System.ComponentModel.DataAnnotations;
using Craftiger.Api.Interfaces;
using Craftiger.Api.Models;
using Craftiger.Api.Repositories;
using Craftiger.Api.Services;
using Craftiger.Solver.Interfaces;
using Craftiger.Solver.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection("ApiOptions"));

builder.Services.AddSingleton(
    builder.Configuration.GetSection("GarageRules").Get<GarageRulesOptions>()?.ToRules() ?? new GarageRulesOptions().ToRules());

builder.Services.AddSingleton(
    builder.Configuration.GetSection("SolverPreferences").Get<SolverPreferencesOptions>()?.ToPreferences() ?? new SolverPreferencesOptions().ToPreferences());

builder.Services.AddSingleton<IPlannerArtifactRepository, PlannerArtifactRepository>();
builder.Services.AddSingleton(provider => provider
    .GetRequiredService<IPlannerArtifactRepository>()
    .Load(provider.GetRequiredService<IOptions<ApiOptions>>().Value.ArtifactsDir));

builder.Services.AddSingleton<ILeafWeightService, LeafWeightService>();
builder.Services.AddSingleton<IGarageLegalityService, GarageLegalityService>();
builder.Services.AddSingleton<ICostSolverService, CostSolverService>();
builder.Services.AddSingleton<IBomService, BomService>();
builder.Services.AddSingleton<IClosureService, ClosureService>();
builder.Services.AddSingleton<ISolveEntryCodec, SolveEntryCodec>();
builder.Services.AddSingleton<ISolveStore, ValkeySolveStore>();
builder.Services.AddSingleton<ISolveCacheService, SolveCacheService>();
builder.Services.AddSingleton<IPlannerQueryService, PlannerQueryService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Load the artifact and connect the store eagerly: a wrong schema_version, a missing
// connection string or an unreachable Valkey refuse at startup, not on the first request.
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

app.MapGet("/api/machines", (string targets, IPlannerQueryService query) =>
    query.Machines(targets.Split(
        ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));

app.MapPost("/api/bom", async (
    BomRequest request, ISolveCacheService cache, IPlannerQueryService query) =>
    await cache.GetAsync(request.SolveId) is { } entry
        ? Results.Ok(query.Bom(entry, request))
        : Results.NotFound());

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
