using Craftiger.Solver.Models;

namespace Craftiger.Solver.Interfaces;

/// <summary>The factory solve: builds the lexicographic flow LP over the garage-legal,
/// cost-banded candidate set and interprets the solution as a steady-state plan.
/// <paramref name="costs"/> must be solved on the same graph, garage and weights.</summary>
public interface IPipelineSolverService
{
    FactoryPlan Solve(
        SolverGraph graph,
        FactoryRecipeData recipes,
        FactoryMachineData machines,
        FactorySeedData seeds,
        CostTable costs,
        Garage garage,
        WeightSettings weights,
        FactoryRequest request);
}
