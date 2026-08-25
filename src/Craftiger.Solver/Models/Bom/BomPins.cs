using Craftiger.Solver.Models.Costs;

namespace Craftiger.Solver.Models.Bom;

/// <summary>The active pins of a walk, item position to recipe position, overlaid on the solve's own choices.</summary>
public sealed record BomPins(IReadOnlyDictionary<int, int> Active)
{
    public static readonly BomPins None = new(new Dictionary<int, int>());

    public bool Contains(int item) => Active.ContainsKey(item);

    /// <summary>The pinned recipe, else the solve's, else -1; a position past the index has neither.</summary>
    public int Chosen(CostTable costs, int item) =>
        Active.TryGetValue(item, out var pinned)
            ? pinned
            : item < costs.Index.ItemCount ? costs.BestRecipe(item) : -1;

    public BomPins Without(int item)
    {
        var active = new Dictionary<int, int>(Active);
        active.Remove(item);
        return new BomPins(active);
    }
}
