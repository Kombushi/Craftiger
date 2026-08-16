namespace Craftiger.Api.Models;

/// <summary>A search or craft-list row; a null cost renders as ∞.</summary>
public sealed record ItemSummaryDto(string ItemId, string Name, long AtlasIdx, double? Cost);
