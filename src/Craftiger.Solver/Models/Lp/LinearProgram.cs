namespace Craftiger.Solver.Models.Lp;

/// <summary>A lexicographic LP: objectives highest priority first; a non-positive time limit means none.</summary>
public sealed record LinearProgram(
    IReadOnlyList<LpColumn> Columns,
    IReadOnlyList<LpRow> Rows,
    IReadOnlyList<LpObjective> Objectives,
    double TimeLimitSeconds = 0);
