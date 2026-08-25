namespace Craftiger.Builder.Models.Planner;

/// <summary>One fuel a generator map burns at 100 % efficiency: EU per unit for standard rows, EU/t over a lifetime for timed ones.</summary>
public sealed record PlannerFuel(
    string Map,
    string ItemId,
    long Amount,
    double? EuPerUnit,
    double? EuT,
    long? DurationTicks);
