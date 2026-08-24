namespace Craftiger.Solver.Models;

/// <summary>A decision variable with its bounds and its sparse constraint-row coefficients.
/// Use <see cref="double.PositiveInfinity"/> for an unbounded side.</summary>
public sealed record LpColumn(double Lower, double Upper, IReadOnlyList<LpEntry> Entries);
