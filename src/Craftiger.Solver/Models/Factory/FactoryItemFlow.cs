namespace Craftiger.Solver.Models.Factory;

/// <summary>Per-item steady-state rates; Surplus is what the plan makes beyond consumption and targets, Supplied the externally supplied rate, AutoInfinite the per-solve badge.</summary>
public sealed record FactoryItemFlow(
    string ItemId, double Produced, double Consumed, double Surplus, double Supplied = 0,
    bool AutoInfinite = false);
