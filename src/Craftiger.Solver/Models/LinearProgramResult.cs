namespace Craftiger.Solver.Models;

/// <summary>Solve outcome; <paramref name="ColumnValues"/> is empty unless the status is
/// <see cref="LpSolveStatus.Optimal"/>.</summary>
public sealed record LinearProgramResult(LpSolveStatus Status, IReadOnlyList<double> ColumnValues);
