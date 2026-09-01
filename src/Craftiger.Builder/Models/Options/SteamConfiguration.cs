namespace Craftiger.Builder.Models.Options;

/// <summary>The steam carrier's curated wiring; an id unknown to the dump is another pack's item and skips with a warning.</summary>
public sealed record SteamConfiguration
{
    /// <summary>The fluid boilers emit.</summary>
    public required string SteamOutputFluidId { get; init; }

    /// <summary>Every fluid that counts as steam; turbines and steam machines take any of them.</summary>
    public required IReadOnlyList<string> SteamFluidIds { get; init; }

    public required string WaterFluidId { get; init; }

    /// <summary>The condensate large steam turbines return.</summary>
    public required string DistilledWaterId { get; init; }

    /// <summary>EU per liter of steam at 100 %; GT's base rate is 2 L per EU.</summary>
    public required double EuPerLiter { get; init; }

    public required string TurbineMap { get; init; }

    public required string LargeTurbineMap { get; init; }
}
