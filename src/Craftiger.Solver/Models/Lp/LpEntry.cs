namespace Craftiger.Solver.Models.Lp;

/// <summary>One sparse coefficient: a row index inside a column, or a column index inside an objective.</summary>
public readonly record struct LpEntry(int Index, double Value);
