namespace Craftiger.Solver.Models.Graph;

/// <summary>An item as the solver sees it: its leaf class (null = not a leaf) and where its weight comes from.</summary>
public sealed record SolverItem(string Id, string? LeafClass, int? Tier, double? Weight, ItemParentLink? Parent)
{
    public bool IsLeaf => LeafClass is not null;

    /// <summary>A fraction leaf prices from its parent unless a weight of its own is known.</summary>
    public bool IsFraction => Parent is not null && Weight is null;
}
