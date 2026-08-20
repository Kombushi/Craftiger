namespace Craftiger.Api.Models;

/// <summary>Display data for one item as planner.sqlite ships it.</summary>
public sealed record ArtifactItem(
    string Id, string Name, string? Oredict, bool IsFluid, string? LeafClass, long AtlasIdx,
    IReadOnlyList<string> Aliases);
