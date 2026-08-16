using Craftiger.Solver.Interfaces;
using Craftiger.Solver.Models;

namespace Craftiger.Solver.Services;

public sealed class ClosureService : IClosureService
{
    /// <summary>Every machine whose recipes could take part in any production route of the
    /// targets, walking producible-by edges tier-agnostically and stopping at leaves — plans
    /// end there, so recipes below a leaf are not relevant to the cart.</summary>
    public IReadOnlyList<string> MachinesFor(SolverGraph graph, IEnumerable<string> targetIds)
    {
        var seen = new HashSet<string>();
        var machines = new HashSet<string>();
        var pending = new Stack<string>(targetIds);
        while (pending.TryPop(out var itemId))
        {
            if (!seen.Add(itemId) || graph.IsLeaf(itemId))
            {
                continue;
            }
            foreach (var recipe in graph.Producers.GetValueOrDefault(itemId) ?? [])
            {
                machines.Add(recipe.Machine);
                foreach (var alternative in recipe.Slots.SelectMany(slot => slot.Alternatives))
                {
                    pending.Push(alternative.ItemId);
                }
            }
        }
        return machines.Order(StringComparer.Ordinal).ToList();
    }
}
