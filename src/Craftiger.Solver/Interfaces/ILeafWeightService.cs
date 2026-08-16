using Craftiger.Solver.Models;

namespace Craftiger.Solver.Interfaces;

/// <summary>Resolves every leaf's weight under the user's settings.</summary>
public interface ILeafWeightService
{
    IReadOnlyDictionary<string, double> Resolve(SolverGraph graph, WeightSettings weights);
}
