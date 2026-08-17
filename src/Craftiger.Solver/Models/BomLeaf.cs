namespace Craftiger.Solver.Models;

/// <summary>A raw-material total: the fractional expected amount beside the whole amount the
/// whole-run plan actually gathers — units for items, mB for fluids.</summary>
public sealed record BomLeaf(string ItemId, double Amount, long WholeAmount);
