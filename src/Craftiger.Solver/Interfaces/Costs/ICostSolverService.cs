using Craftiger.Solver.Models.Costs;
using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Interfaces.Costs;

/// <summary>The cost engine: a strict-improvement worklist fixpoint over garage-legal recipes.</summary>
public interface ICostSolverService
{
    CostTable Solve(SolverGraph graph, Garage garage, WeightSettings weights);
}
