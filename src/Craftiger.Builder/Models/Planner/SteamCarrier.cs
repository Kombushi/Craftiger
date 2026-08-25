namespace Craftiger.Builder.Models.Planner;

/// <summary>The steam carrier's pack facts as the artifact ships them: which fluids are steam, the condensate, and the rates.</summary>
public sealed record SteamCarrier(
    IReadOnlyList<string> SteamFluidIds,
    string? DistilledWaterId,
    double EuPerLiter,
    long WaterPerSteam);
