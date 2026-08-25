using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Models.Bom;

/// <summary>The walk's item space: index positions, plus a position past the index for every target the index never saw, so it walks — and warns — like any other unproducible item.</summary>
public sealed class BomItems(SolverIndex index)
{
    private readonly List<string> _extraIds = [];

    public SolverIndex Index { get; } = index;

    /// <summary>Whether the position is one the index knows, as opposed to an unknown target.</summary>
    public bool IsIndexed(int item) => item < Index.ItemCount;

    public bool IsLeaf(int item) => IsIndexed(item) && Index.IsLeaf(item);

    /// <summary>The position of an id, assigned once per unknown id.</summary>
    public int PositionOf(string itemId)
    {
        if (Index.TryGetItem(itemId, out var item))
        {
            return item;
        }
        var extra = _extraIds.IndexOf(itemId);
        if (extra < 0)
        {
            extra = _extraIds.Count;
            _extraIds.Add(itemId);
        }
        return Index.ItemCount + extra;
    }

    public string IdOf(int item) => IsIndexed(item) ? Index.ItemIds[item] : _extraIds[item - Index.ItemCount];
}
