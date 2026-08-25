using Craftiger.Solver.Models.Lp;

namespace Craftiger.Solver.Interfaces.Lp;

/// <summary>Solves a lexicographic LP deterministically: the same program returns the same values on every call, machine and replica.</summary>
public interface ILinearProgramSolver
{
    LinearProgramResult Solve(LinearProgram program);
}
