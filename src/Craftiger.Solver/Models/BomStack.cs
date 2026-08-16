namespace Craftiger.Solver.Models;

/// <summary>An item with a fractional expected amount — units for items, mB for fluids.</summary>
public sealed record BomStack(string ItemId, double Amount);
