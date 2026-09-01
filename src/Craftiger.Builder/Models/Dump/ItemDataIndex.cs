namespace Craftiger.Builder.Models.Dump;

/// <summary>GT's composition records keyed by item and meta, since recipes reference items under NBT hashes the sweep never saw.</summary>
public sealed record ItemDataIndex
{
    private readonly Dictionary<string, DumpItemData> _byItemAndMeta = [];

    public ItemDataIndex(IEnumerable<DumpItemData> rows)
    {
        foreach (var row in rows)
        {
            _byItemAndMeta.TryAdd(ItemIdSegments.Take(row.ItemId, ItemIdSegments.ItemAndMeta), row);
        }
    }

    /// <summary>The item's total matter, byproducts included; null when GT records none or leaves the amount undefined.</summary>
    public double? Content(string itemId)
    {
        if (!_byItemAndMeta.TryGetValue(ItemIdSegments.Take(itemId, ItemIdSegments.ItemAndMeta), out var row) || row.Amount < 0)
        {
            return null;
        }
        return row.Amount + row.Byproducts.Where(b => b > 0).Sum();
    }

    /// <summary>The ore prefix GT assigns the item, for shapes whose oredict never got registered.</summary>
    public string? PrefixOf(string itemId) =>
        _byItemAndMeta.TryGetValue(ItemIdSegments.Take(itemId, ItemIdSegments.ItemAndMeta), out var row) ? row.Prefix : null;
}
