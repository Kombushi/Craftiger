using Craftiger.Solver.Models.Costs;

namespace Craftiger.Api.Models;

/// <summary>One cached solve: the cost table, the settings that produced it, and the craft list as ranks into the artifact's order, cheapest first with the unreachable tail after the first ReachableCount.</summary>
public sealed record SolveEntry(
    CostTable Table,
    Garage Garage,
    WeightSettings Weights,
    IReadOnlyList<int> Sorted,
    int ReachableCount);
