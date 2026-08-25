namespace Craftiger.Solver.Models.Lp;

/// <summary>Solve outcome; the column values are empty unless the status is optimal.</summary>
public sealed record LinearProgramResult(LpSolveStatus Status, IReadOnlyList<double> ColumnValues)
{
    public static LinearProgramResult Failed(LpSolveStatus status) => new(status, []);
}
