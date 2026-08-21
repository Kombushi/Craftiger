using Craftiger.Solver.Models;

namespace Craftiger.Api.Models;

/// <summary>One cached solve: the cost table, the settings that produced it, and the craft
/// list as ranks into the artifact's <see cref="PlannerArtifact.CraftListOrder"/>, already
/// ordered cheapest-first with unreachable items at the bottom — the first
/// <paramref name="ReachableCount"/> of them are priced.</summary>
public sealed record SolveEntry(
    CostTable Table,
    Garage Garage,
    WeightSettings Weights,
    IReadOnlyList<int> Sorted,
    int ReachableCount);
