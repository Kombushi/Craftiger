namespace Craftiger.Builder.Models;

/// <summary>GT's item composition records keyed by the id's item and meta segments, since
/// recipes reference an item under NBT hashes the registry sweep never saw.</summary>
public sealed class ItemDataIndex
{
    private readonly Dictionary<string, DumpItemData> _byItemAndMeta = [];

    public ItemDataIndex(IEnumerable<DumpItemData> rows)
    {
        foreach (var row in rows)
        {
            _byItemAndMeta.TryAdd(Segments(row.ItemId, 4), row);
        }
    }

    /// <summary>The item's total matter, byproducts included, or null when GT records none
    /// or leaves the quantity undefined — undefined is unknown, never zero.</summary>
    public double? Content(string itemId)
    {
        if (!_byItemAndMeta.TryGetValue(Segments(itemId, 4), out var row) || row.Amount < 0)
        {
            return null;
        }
        return row.Amount + row.Byproducts.Where(b => b.Amount > 0).Sum(b => b.Amount);
    }

    /// <summary>The ore prefix GT's record assigns the item, for shapes whose oredict
    /// never got registered — BartWorks dusts with parenthesised names.</summary>
    public string? PrefixOf(string itemId) =>
        _byItemAndMeta.TryGetValue(Segments(itemId, 4), out var row) ? row.Prefix : null;

    private static string Segments(string id, int count)
    {
        var parts = id.Split('~');
        return parts.Length <= count ? id : string.Join('~', parts[..count]);
    }
}
