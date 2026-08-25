namespace Craftiger.Api.Models;

/// <summary>The artifact's steam meta entry as written by the builder.</summary>
internal sealed record SteamMeta(
    IReadOnlyList<string> SteamFluidIds,
    string? DistilledWaterId,
    double EuPerLiter,
    long WaterPerSteam);
