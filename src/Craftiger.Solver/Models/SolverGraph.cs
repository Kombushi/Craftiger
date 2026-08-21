namespace Craftiger.Solver.Models;

/// <summary>The graph the solver works on: the leaves with their weight sources, and the
/// recipes as the positional index — nothing else is retained. Recipe records only exist as
/// an input form (<see cref="Build"/>) for fixtures and tests; a real artifact streams into
/// the index builder directly.</summary>
public sealed class SolverGraph(IReadOnlyDictionary<string, SolverItem> items, SolverIndex index)
{
    /// <summary>The leaves by id; every other item the graph knows is an index position only.</summary>
    public IReadOnlyDictionary<string, SolverItem> Items { get; } = items;

    public SolverIndex Index { get; } = index;

    /// <summary>An item absent from the item set is simply not a leaf.</summary>
    public bool IsLeaf(string itemId) => Items.TryGetValue(itemId, out var item) && item.IsLeaf;

    public static SolverGraph Build(IEnumerable<SolverItem> items, IEnumerable<SolverRecipe> recipes)
    {
        var leaves = items.ToDictionary(item => item.Id);
        return new SolverGraph(leaves, SolverIndex.Build(leaves.Values, recipes));
    }
}
