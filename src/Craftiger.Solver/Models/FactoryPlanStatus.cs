namespace Craftiger.Solver.Models;

/// <summary>Terminal state of a factory solve. <see cref="Unbounded"/> means a free-lunch
/// cycle survived into the model — always a data or model defect, reported loudly.</summary>
public enum FactoryPlanStatus
{
    Solved,
    Infeasible,
    Unbounded,
    TimedOut,
    Failed,
}
