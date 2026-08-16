namespace Craftiger.Solver.Models;

/// <summary>The user's tuning surface: the ingot price base B and per-item weight overrides.
/// An override beats the artifact's shipped weight, which beats the item's class rule.</summary>
public sealed record WeightSettings(
    double PriceBase,
    IReadOnlyDictionary<string, double> ItemWeights);
