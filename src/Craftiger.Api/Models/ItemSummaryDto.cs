namespace Craftiger.Api.Models;

/// <summary>A search or craft-list row; a null cost renders as ∞, or as uncraftable when nothing in the pack produces the item.</summary>
public sealed record ItemSummaryDto(string ItemId, string Name, long AtlasIdx, double? Cost, bool Uncraftable);
