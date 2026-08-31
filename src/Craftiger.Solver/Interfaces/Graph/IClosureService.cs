using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Interfaces.Graph;

/// <summary>Walks producible-by edges tier-agnostically from targets down to leaves; the deep walk continues through leaf-class items, the way a factory solve expands them.</summary>
public interface IClosureService
{
    IReadOnlyList<string> MachinesFor(SolverGraph graph, IEnumerable<string> targetIds, bool deep = false);
}
