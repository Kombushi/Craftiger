namespace Craftiger.Solver.Models.Factory;

/// <summary>Terminal state of a factory solve; Unbounded means a free-lunch cycle survived into the model, always a data defect.</summary>
public enum FactoryPlanStatus
{
    Solved,
    Infeasible,
    Unbounded,
    TimedOut,
    Failed,
}
