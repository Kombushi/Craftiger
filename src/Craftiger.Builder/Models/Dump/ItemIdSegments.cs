namespace Craftiger.Builder.Models.Dump;

/// <summary>Trims an i~mod~name~meta~nbt id to its leading segments, so NBT variants match one item.</summary>
public static class ItemIdSegments
{
    public const int ItemAndMeta = 4;

    public const int ItemFamily = 3;

    public static string Take(string id, int count)
    {
        var parts = id.Split('~');
        return parts.Length <= count ? id : string.Join('~', parts[..count]);
    }
}
