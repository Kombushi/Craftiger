namespace Craftiger.Builder.Models.Dump;

/// <summary>GT's record of what an item is made of, M = 3628800 one ingot; -1 means a known material of unknown amount.</summary>
public sealed record DumpItemData(
    string ItemId,
    string? Prefix,
    string Material,
    long Amount,
    IReadOnlyList<DumpMaterialAmount> Byproducts);
