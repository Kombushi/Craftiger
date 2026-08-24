using Craftiger.Solver.Interfaces;
using Craftiger.Solver.Models;

namespace Craftiger.Solver.UnitTests;

/// <summary>Captures the program a solve builds; answers all-zero columns unless a canned
/// result is set.</summary>
internal sealed class RecordingLpSolver : ILinearProgramSolver
{
    public LinearProgram? Program { get; private set; }

    public LinearProgramResult? Result { get; set; }

    public LinearProgramResult Solve(LinearProgram program)
    {
        Program = program;
        return Result ?? new LinearProgramResult(LpSolveStatus.Optimal, new double[program.Columns.Count]);
    }
}
