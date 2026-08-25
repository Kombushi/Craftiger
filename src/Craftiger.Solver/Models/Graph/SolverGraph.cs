namespace Craftiger.Solver.Models.Graph;

/// <summary>The graph the solver works on: the leaves with their weight sources, and every recipe as the positional index.</summary>
public sealed record SolverGraph(IReadOnlyDictionary<string, SolverItem> Items, SolverIndex Index)
{
    /// <summary>An item absent from the item set is simply not a leaf.</summary>
    public bool IsLeaf(string itemId) => Items.TryGetValue(itemId, out var item) && item.IsLeaf;

    public static SolverGraph Build(IEnumerable<SolverItem> items, IEnumerable<SolverRecipe> recipes)
    {
        var leaves = items.ToDictionary(item => item.Id);
        return new SolverGraph(leaves, SolverIndex.Build(leaves.Values, recipes));
    }
}
