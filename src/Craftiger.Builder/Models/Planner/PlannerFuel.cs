namespace Craftiger.Builder.Models.Planner;

/// <summary>One fuel a generator map burns at 100 % efficiency: EU per unit for standard rows, EU/t over a lifetime per Amount for timed ones, which may return a spent fluid.</summary>
public sealed record PlannerFuel(
    string Map,
    string ItemId,
    long Amount,
    double? EuPerUnit,
    double? EuT,
    long? DurationTicks,
    string? ReturnItemId = null,
    long ReturnAmount = 0);
