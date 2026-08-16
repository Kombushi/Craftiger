namespace Craftiger.Solver.Models;

/// <summary>What the solver needs to know about an item: whether it is a leaf, and where its
/// weight comes from — a tier, a shipped weight, or a parent it is a fraction of.</summary>
public sealed record SolverItem(
    string Id, bool IsLeaf, int? Tier, double? Weight, ItemParentLink? Parent);
