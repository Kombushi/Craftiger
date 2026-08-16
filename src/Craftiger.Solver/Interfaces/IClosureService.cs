using Craftiger.Solver.Models;

namespace Craftiger.Solver.Interfaces;

/// <summary>Walks producible-by edges tier-agnostically from targets down to leaves.</summary>
public interface IClosureService
{
    IReadOnlyList<string> MachinesFor(SolverGraph graph, IEnumerable<string> targetIds);
}
