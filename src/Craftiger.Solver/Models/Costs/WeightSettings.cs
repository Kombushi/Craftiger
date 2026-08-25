namespace Craftiger.Solver.Models.Costs;

/// <summary>The user's tuning surface: the ingot price base and per-item weight overrides, which beat shipped weights and class rules.</summary>
public sealed record WeightSettings(double PriceBase, IReadOnlyDictionary<string, double> ItemWeights);
