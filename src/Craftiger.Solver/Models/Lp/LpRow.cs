namespace Craftiger.Solver.Models.Lp;

/// <summary>A constraint row's bounds: equal sides for an equality, an infinite side for an inequality.</summary>
public sealed record LpRow(double Lower, double Upper);
