namespace Craftiger.Solver.Models;

/// <summary>An item or fluid with an amount — units for items, mB for fluids. A value type:
/// a million of them sit inline in the recipe graph, and lists built from them allocate once.</summary>
public readonly record struct SolverStack(string ItemId, long Amount);
