namespace Craftiger.Api.Models;

/// <summary>Display data for an item a response refers to by id, so clients render icons
/// and names without extra lookups.</summary>
public sealed record ItemRefDto(
    string Name, long AtlasIdx, bool IsFluid, string? LeafClass, double? Cost);