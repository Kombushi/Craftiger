using Craftiger.Solver.Models.Factory;

namespace Craftiger.Solver.Interfaces.Factory;

/// <summary>The factory solve: the lexicographic flow LP over the garage-legal, cost-banded candidate set, read back as a steady-state plan.</summary>
public interface IFactorySolverService
{
    FactoryPlan Solve(FactoryContext context, FactoryRequest request);
}
