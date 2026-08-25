using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Models.Factory;

/// <summary>The curated auto-infinite primitives by item id: the base of the per-solve fixpoint, bought at weight zero.</summary>
public sealed record FactorySeedData(IReadOnlyDictionary<string, SeedKind> Kinds)
{
    public static readonly FactorySeedData Empty = new(new Dictionary<string, SeedKind>());

    /// <summary>The seeds the index knows, mob seeds only when the toggle admits them.</summary>
    public HashSet<int> Resolve(SolverIndex index, bool mobFarms)
    {
        var items = new HashSet<int>();
        foreach (var (itemId, kind) in Kinds)
        {
            if ((mobFarms || kind != SeedKind.Mob) && index.TryGetItem(itemId, out var item))
            {
                items.Add(item);
            }
        }
        return items;
    }
}
