namespace Craftiger.Solver.Models.Graph;

/// <summary>One output row; Chance is in (0, 1].</summary>
public sealed record SolverOutput(string ItemId, long Amount, double Chance);
