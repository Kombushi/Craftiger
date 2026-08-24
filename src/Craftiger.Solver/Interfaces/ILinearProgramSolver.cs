using Craftiger.Solver.Models;

namespace Craftiger.Solver.Interfaces;

/// <summary>Solves a lexicographic LP. Implementations must be deterministic: the same program
/// returns the same values on every call, machine, and replica.</summary>
public interface ILinearProgramSolver
{
    LinearProgramResult Solve(LinearProgram program);
}
