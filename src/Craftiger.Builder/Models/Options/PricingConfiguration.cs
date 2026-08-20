namespace Craftiger.Builder.Models.Options;

/// <summary>Defaults of the cost model, used by the build-time price check.</summary>
public sealed record PricingConfiguration
{
    /// <summary>Ingot price at tier 0, the default of the cost model's B; the app lets the
    /// user retune it, and the build-time price check uses this value.</summary>
    public required double PriceBase { get; init; }

    /// <summary>How far below its own weight a leaf may price before the build reports it.
    /// Undercutting a leaf is normal — its weight is only the price when no route exists, and a
    /// cheap route beats it honestly. Undercutting it by orders of magnitude is not: that is a
    /// recipe loop handing back more material than it consumed.</summary>
    public required double PriceLeakRatio { get; init; }
}
