namespace Craftiger.Builder.Models;

/// <summary>One fuel a generator map burns, normalized to 100 % generator efficiency.
/// Standard rows carry <paramref name="EuPerUnit"/> — EU per mB for fluids, EU per item for
/// solids; lifetime rows (RTG, GoodGenerator naquadah) carry <paramref name="EuT"/> over
/// <paramref name="DurationTicks"/> per <paramref name="Amount"/> consumed.</summary>
public sealed record PlannerFuel(
    string Map,
    string ItemId,
    long Amount,
    double? EuPerUnit,
    double? EuT,
    long? DurationTicks);
