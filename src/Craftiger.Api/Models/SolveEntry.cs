using Craftiger.Solver.Models;

namespace Craftiger.Api.Models;

/// <summary>One cached solve: the cost table, the settings that produced it, and the craft
/// list already sorted cheapest-first with unreachable items at the bottom.</summary>
public sealed record SolveEntry(
    CostTable Table,
    Garage Garage,
    WeightSettings Weights,
    IReadOnlyList<SortedRow> Sorted,
    int ReachableCount);
