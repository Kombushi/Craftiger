namespace Craftiger.Builder.Models.Planner;

/// <summary>What one grid cell turned into: a flat ingredient by canonical id, the n-th choice slot, or the n-th catalyst slot.</summary>
public sealed record GridCellRef(int Cell, string? Item, int? Choice, int? Catalyst);
