namespace Craftiger.Solver.Models;

/// <summary>An item or fluid with an amount — units for items, mB for fluids.</summary>
public sealed record SolverStack(string ItemId, long Amount);
