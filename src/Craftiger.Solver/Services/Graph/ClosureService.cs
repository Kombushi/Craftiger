using Craftiger.Solver.Interfaces.Graph;
using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Services.Graph;

public sealed class ClosureService : IClosureService
{
    /// <summary>Every machine whose recipes could take part in any route to the targets; crafting plans end at leaves, so recipes below one only matter to the deep walk.</summary>
    public IReadOnlyList<string> MachinesFor(SolverGraph graph, IEnumerable<string> targetIds, bool deep = false)
    {
        var index = graph.Index;
        var seen = new HashSet<int>();
        var machines = new HashSet<string>();
        var pending = new Stack<int>();
        foreach (var targetId in targetIds)
        {
            if (index.TryGetItem(targetId, out var target))
            {
                pending.Push(target);
            }
        }
        while (pending.TryPop(out var item))
        {
            if (!seen.Add(item) || (!deep && index.IsLeaf(item)))
            {
                continue;
            }
            for (var p = index.ProducerStart[item]; p < index.ProducerStart[item + 1]; p++)
            {
                var recipe = index.ProducerRecipe[p];
                machines.Add(index.Machine[recipe]);
                for (var a = index.FirstAlternative(recipe); a < index.EndAlternative(recipe); a++)
                {
                    pending.Push(index.AlternativeItem[a]);
                }
            }
        }
        return [.. machines.Order(StringComparer.Ordinal)];
    }
}
