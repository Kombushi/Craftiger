namespace Craftiger.Api.Models;

/// <summary>An <c>items</c> row of planner.sqlite as read at load.</summary>
internal sealed record ItemRow(
    string Id,
    string Name,
    string? Oredict,
    long IsFluid,
    string? LeafClass,
    long AtlasIdx);
