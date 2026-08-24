namespace Craftiger.Solver.Models;

/// <summary>Per-item steady-state rates. <paramref name="Surplus"/> is what the plan makes
/// beyond consumption and targets — reported, never silently discarded;
/// <paramref name="Supplied"/> is the externally supplied rate a consume target feeds in;
/// <paramref name="AutoInfinite"/> marks items the auto-infinite fixpoint reaches.</summary>
public sealed record FactoryItemFlow(
    string ItemId, double Produced, double Consumed, double Surplus, double Supplied = 0,
    bool AutoInfinite = false);
