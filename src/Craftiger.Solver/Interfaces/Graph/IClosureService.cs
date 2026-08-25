using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Interfaces.Graph;

/// <summary>Walks producible-by edges tier-agnostically from targets down to leaves.</summary>
public interface IClosureService
{
    IReadOnlyList<string> MachinesFor(SolverGraph graph, IEnumerable<string> targetIds);
}
