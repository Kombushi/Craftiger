namespace Craftiger.Builder.Models.Dump;

/// <summary>Tools by Forge's container-item data: an item that crafts into its own worn self, at any damage.</summary>
public sealed record ToolIndex
{
    private readonly HashSet<string> _byItemAndMeta = [];
    private readonly HashSet<string> _byItemFamily = [];
    private readonly IReadOnlyDictionary<string, DumpItem> _items;

    public ToolIndex(IReadOnlyDictionary<string, DumpItem> items, IReadOnlyDictionary<string, string> containers)
    {
        _items = items;
        foreach (var (itemId, containerId) in containers)
        {
            var exact = ItemIdSegments.Take(itemId, ItemIdSegments.ItemAndMeta);
            if (exact == ItemIdSegments.Take(containerId, ItemIdSegments.ItemAndMeta))
            {
                _byItemAndMeta.Add(exact);
            }
            else if (ItemIdSegments.Take(itemId, ItemIdSegments.ItemFamily) == ItemIdSegments.Take(containerId, ItemIdSegments.ItemFamily)
                && Wears(itemId))
            {
                // Wears through its metadata, so every damage state is the same tool.
                _byItemFamily.Add(ItemIdSegments.Take(itemId, ItemIdSegments.ItemFamily));
            }
        }
    }

    public bool IsTool(string itemId) =>
        _byItemAndMeta.Contains(ItemIdSegments.Take(itemId, ItemIdSegments.ItemAndMeta))
        || (_byItemFamily.Contains(ItemIdSegments.Take(itemId, ItemIdSegments.ItemFamily)) && Wears(itemId));

    private bool Wears(string itemId) => _items.TryGetValue(itemId, out var item) && item.MaxDamage > 0;
}
