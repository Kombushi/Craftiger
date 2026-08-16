namespace Craftiger.Api.Models;

/// <summary>One row of a solve's precomputed craft list; a null cost means unreachable.</summary>
public sealed record SortedRow(string ItemId, double? Cost);
