namespace Craftiger.Builder.Models;

/// <summary>Tools recognised by Forge's container-item data: an item that crafts into its own
/// worn self. Matching ignores the id's NBT segment, since a recipe references a tool at any
/// damage while the container sweep saw one particular state.</summary>
public sealed class ToolIndex
{
    private readonly HashSet<string> _byItemAndMeta = [];
    private readonly HashSet<string> _byItemFamily = [];
    private readonly Dump _dump;

    public ToolIndex(Dump dump)
    {
        _dump = dump;
        foreach (var (itemId, containerId) in dump.ItemContainers)
        {
            if (Segments(itemId, 4) == Segments(containerId, 4))
            {
                _byItemAndMeta.Add(Segments(itemId, 4));
            }
            else if (Segments(itemId, 3) == Segments(containerId, 3)
                && dump.Items.TryGetValue(itemId, out var item) && item.MaxDamage > 0)
            {
                // Wears through its metadata, so every damage state is the same tool.
                _byItemFamily.Add(Segments(itemId, 3));
            }
        }
    }

    public bool IsTool(string itemId)
    {
        if (_byItemAndMeta.Contains(Segments(itemId, 4)))
        {
            return true;
        }
        return _byItemFamily.Contains(Segments(itemId, 3))
            && _dump.Items.TryGetValue(itemId, out var item) && item.MaxDamage > 0;
    }

    /// <summary>The leading id segments: i~mod~name~meta~nbt trimmed to the first n parts.</summary>
    private static string Segments(string id, int count)
    {
        var parts = id.Split('~');
        return parts.Length <= count ? id : string.Join('~', parts[..count]);
    }
}
