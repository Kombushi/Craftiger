namespace Craftiger.Api.Models;

public sealed record ApiOptions
{
    /// <summary>Directory holding planner.sqlite, atlas.webp and atlas-offsets.json.</summary>
    public string ArtifactsDir { get; init; } = "artifacts";

    /// <summary>Solved cost tables kept in memory; least recently used entries evict first.</summary>
    public int SolveCacheSize { get; init; } = 16;

    public ValkeyOptions Valkey { get; init; } = new();
}
