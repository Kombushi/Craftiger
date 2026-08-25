namespace Craftiger.Api.Models;

/// <summary>Display data for one item as the artifact ships it; MaxStack is null for fluids, Uncraftable marks an item nothing produces that is no raw material either.</summary>
public sealed record ArtifactItem(
    string Id,
    string Name,
    string? Oredict,
    bool IsFluid,
    string? LeafClass,
    long AtlasIdx,
    int? MaxStack,
    IReadOnlyList<string> Aliases,
    bool Uncraftable = false);
