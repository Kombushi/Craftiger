namespace Craftiger.Solver.Models;

/// <summary>Terminal state of an LP solve.</summary>
public enum LpSolveStatus
{
    Optimal,
    Infeasible,
    Unbounded,
    TimedOut,
    Error,
}
