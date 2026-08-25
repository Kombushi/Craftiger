namespace Craftiger.Solver.Models.Factory;

/// <summary>One factory target: units per second for item kinds, EU/t of net export for energy; GeneratorTier is the minimum tier the exporting generators emit at.</summary>
public sealed record FactoryTarget(FactoryTargetKind Kind, string? ItemId, double Rate, int? GeneratorTier = null);
