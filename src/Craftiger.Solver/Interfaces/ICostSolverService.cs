using Craftiger.Solver.Models;

namespace Craftiger.Solver.Interfaces;

/// <summary>The cost engine: a strict-improvement worklist fixpoint over garage-legal recipes.</summary>
public interface ICostSolverService
{
    CostTable Solve(SolverGraph graph, Garage garage, WeightSettings weights);

    /// <summary>One recipe's candidate cost for one of its outputs against solved costs,
    /// or +∞ where an input is unreachable.</summary>
    double Candidate(CostTable table, int recipe, string itemId);
}
