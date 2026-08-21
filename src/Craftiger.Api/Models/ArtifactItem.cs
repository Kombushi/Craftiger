namespace Craftiger.Api.Models;

/// <summary>Display data for one item as planner.sqlite ships it. <paramref name="MaxStack"/>
/// is the stack size the pack gives the item, null for fluids. <paramref name="Uncraftable"/>
/// marks an item no recipe in the pack produces and that is not a raw material either — it
/// is only ever an input, so it reads as uncraftable under every garage.</summary>
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
