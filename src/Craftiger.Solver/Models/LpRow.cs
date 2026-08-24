namespace Craftiger.Solver.Models;

/// <summary>A constraint row's bounds: lower = upper for an equality, an infinite side for a
/// one-sided inequality.</summary>
public sealed record LpRow(double Lower, double Upper);
