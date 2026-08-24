namespace Craftiger.Solver.Models;

/// <summary>Per-item steady-state rates. <paramref name="Surplus"/> is what the plan makes
/// beyond consumption and targets — reported, never silently discarded.</summary>
public sealed record FactoryItemFlow(string ItemId, double Produced, double Consumed, double Surplus);
