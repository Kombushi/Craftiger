namespace Craftiger.Solver.Models;

/// <summary>One factory target. <paramref name="Rate"/> is units per second for item kinds and
/// EU/t of net export for energy; <paramref name="GeneratorTier"/> is the minimum voltage tier
/// the exporting generators must emit at, energy targets only.</summary>
public sealed record FactoryTarget(FactoryTargetKind Kind, string? ItemId, double Rate, int? GeneratorTier = null);
