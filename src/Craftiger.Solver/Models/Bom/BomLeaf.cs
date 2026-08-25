namespace Craftiger.Solver.Models.Bom;

/// <summary>A raw-material total: the fractional expected amount beside the whole amount the whole-run plan gathers.</summary>
public sealed record BomLeaf(string ItemId, double Amount, long WholeAmount);
