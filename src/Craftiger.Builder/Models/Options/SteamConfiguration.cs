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

    /// <summary>Liters of steam boiled from one liter of water.</summary>
    public required long WaterPerSteam { get; init; }

    /// <summary>boiler_fuels generation name to the controller item that burns those rows.</summary>
    public required IReadOnlyDictionary<string, string> Boilers { get; init; }

    /// <summary>Single-block steam turbine items; their conversion ships in machine_props.</summary>
    public required IReadOnlyList<string> SingleTurbines { get; init; }

    /// <summary>Rotor-driven steam turbine controllers, large and XL.</summary>
    public required IReadOnlyList<string> LargeTurbines { get; init; }

    public required string TurbineMap { get; init; }

    public required string LargeTurbineMap { get; init; }
}
