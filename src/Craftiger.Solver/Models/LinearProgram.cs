namespace Craftiger.Solver.Models;

/// <summary>A complete lexicographic LP: objectives are ordered highest priority first and at
/// least one is required. <paramref name="TimeLimitSeconds"/> bounds the whole solve; zero or
/// negative means no limit.</summary>
public sealed record LinearProgram(
    IReadOnlyList<LpColumn> Columns,
    IReadOnlyList<LpRow> Rows,
    IReadOnlyList<LpObjective> Objectives,
    double TimeLimitSeconds = 0);
