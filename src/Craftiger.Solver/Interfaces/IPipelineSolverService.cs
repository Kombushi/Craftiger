using Craftiger.Solver.Models;

namespace Craftiger.Solver.Interfaces;

/// <summary>The factory solve: builds the lexicographic flow LP over the garage-legal
/// candidate set and interprets the solution as a steady-state plan.</summary>
public interface IPipelineSolverService
{
    FactoryPlan Solve(
        SolverGraph graph,
        FactoryRecipeData recipes,
        Garage garage,
        WeightSettings weights,
        FactoryRequest request);
}
