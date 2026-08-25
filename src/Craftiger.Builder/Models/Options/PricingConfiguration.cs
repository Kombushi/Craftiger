namespace Craftiger.Builder.Models.Options;

/// <summary>Defaults of the cost model, used by the build-time price check.</summary>
public sealed record PricingConfiguration
{
    /// <summary>Ingot price at tier 0, the cost model's default B; the app lets the user retune it.</summary>
    public required double PriceBase { get; init; }

    /// <summary>How far below its own weight a leaf may price before the build reports it as a matter leak.</summary>
    public required double PriceLeakRatio { get; init; }
}
