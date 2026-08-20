namespace Craftiger.Builder.Models;

/// <summary>GT's own record of what an item is made of, with M = 3628800 one ingot.
/// An amount of -1 means GT knows the material but not the quantity.</summary>
public sealed record DumpItemData(
    string ItemId,
    string? Prefix,
    string Material,
    long Amount,
    IReadOnlyList<(string Material, long Amount)> Byproducts);
