namespace Craftiger.Solver.Models.Lp;

/// <summary>A decision variable: bounds (infinite sides allowed) and sparse row coefficients.</summary>
public sealed record LpColumn(double Lower, double Upper, IReadOnlyList<LpEntry> Entries);
