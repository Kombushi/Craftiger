using Craftiger.Solver.Models.Costs;
using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Interfaces.Costs;

/// <summary>Resolves every leaf's weight under the user's settings.</summary>
public interface ILeafWeightService
{
    IReadOnlyDictionary<string, double> Resolve(SolverGraph graph, WeightSettings weights);
}
