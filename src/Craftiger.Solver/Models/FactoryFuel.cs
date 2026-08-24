namespace Craftiger.Solver.Models;

/// <summary>One burnable fuel on a generator map. Standard fuels carry
/// <paramref name="EuPerUnit"/> (EU per mB or per item before efficiency); timed fuels — RTG
/// pellets, naquadah bolts — carry a fixed <paramref name="EuT"/> over
/// <paramref name="DurationTicks"/> and consume <paramref name="Amount"/> per burn.</summary>
public sealed record FactoryFuel(
    string Map,
    string ItemId,
    long Amount,
    double? EuPerUnit,
    double? EuT,
    long? DurationTicks);
