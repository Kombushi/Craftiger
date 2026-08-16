namespace Craftiger.Solver.Models;

/// <summary>One output row; <paramref name="Chance"/> is in (0, 1].</summary>
public sealed record SolverOutput(string ItemId, long Amount, double Chance);
