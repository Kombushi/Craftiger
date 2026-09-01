namespace Craftiger.Builder.Models.Dump;

/// <summary>GT's record of how much matter an item holds, M = 3628800 one ingot; -1 means a known material of unknown amount. Byproduct amounts count toward the total; conservation is of volume, never identity, so the material names stay behind.</summary>
public sealed record DumpItemData(
    string ItemId,
    string? Prefix,
    long Amount,
    IReadOnlyList<long> Byproducts);
