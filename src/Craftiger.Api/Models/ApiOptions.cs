namespace Craftiger.Api.Models;

public sealed record ApiOptions
{
    /// <summary>Directory holding planner.sqlite, atlas.webp and atlas-offsets.json.</summary>
    public string ArtifactsDir { get; init; } = "artifacts";

    /// <summary>Solved cost tables kept in memory; least recently used entries evict first.</summary>
    public int SolveCacheSize { get; init; } = 16;

    /// <summary>Solved factory plans kept in memory; plans are small, so many fit.</summary>
    public int FactoryCacheSize { get; init; } = 64;

    /// <summary>Wall-clock budget of one factory solve; a plan past it answers timed_out and is never cached.</summary>
    public double FactoryTimeLimitSeconds { get; init; } = 120;

    public ValkeyOptions Valkey { get; init; } = new();
}
