namespace Craftiger.Solver.Models.Graph;

/// <summary>An item or fluid with an amount — units for items, mB for fluids; a value type because a million sit inline in the graph.</summary>
public readonly record struct SolverStack(string ItemId, long Amount);
