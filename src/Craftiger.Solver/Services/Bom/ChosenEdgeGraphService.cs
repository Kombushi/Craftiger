using Craftiger.Solver.Interfaces.Bom;
using Craftiger.Solver.Models.Bom;
using Craftiger.Solver.Models.Costs;
using Craftiger.Solver.Models.Options;
using Microsoft.Extensions.Options;

namespace Craftiger.Solver.Services.Bom;

public sealed class ChosenEdgeGraphService(IOptions<BomOptions> options) : IChosenEdgeGraphService
{
    private readonly BomOptions _options = options.Value;

    public (IReadOnlyList<BomComponent> Components, int? CyclePin) Walk(
        BomItems items, CostTable costs, BomPins pins, IReadOnlyList<int> roots, IReadOnlyDictionary<int, LoopSeed> seeds)
    {
        var order = 0;
        var indices = new Dictionary<int, int>();
        var low = new Dictionary<int, int>();
        var onStack = new HashSet<int>();
        var stack = new Stack<int>();
        var components = new List<BomComponent>();
        foreach (var root in roots)
        {
            if (indices.ContainsKey(root))
            {
                continue;
            }
            var work = new Stack<(int Item, int Next, IReadOnlyList<int> Children)>();
            indices[root] = low[root] = order++;
            stack.Push(root);
            onStack.Add(root);
            work.Push((root, 0, Children(items, costs, pins, seeds, root)));
            while (work.Count > 0)
            {
                var (item, next, children) = work.Pop();
                if (next < children.Count)
                {
                    work.Push((item, next + 1, children));
                    var child = children[next];
                    if (!indices.ContainsKey(child))
                    {
                        indices[child] = low[child] = order++;
                        stack.Push(child);
                        onStack.Add(child);
                        work.Push((child, 0, Children(items, costs, pins, seeds, child)));
                    }
                    else if (onStack.Contains(child))
                    {
                        low[item] = Math.Min(low[item], indices[child]);
                    }
                    continue;
                }
                if (work.Count > 0)
                {
                    var parent = work.Peek().Item;
                    low[parent] = Math.Min(low[parent], low[item]);
                }
                if (low[item] != indices[item])
                {
                    continue;
                }
                var members = new List<int>();
                int member;
                do
                {
                    member = stack.Pop();
                    onStack.Remove(member);
                    members.Add(member);
                }
                while (member != item);
                var loop = members.Count > 1 || children.Contains(item);
                if (loop && !LoopSystem.Analyze(costs, pins, members).HasFinitePlan(_options.PivotEpsilon))
                {
                    // A pin is the only way to build a loop with no finite plan; the solve's own loops converge.
                    foreach (var candidate in members)
                    {
                        if (pins.Contains(candidate))
                        {
                            return ([], candidate);
                        }
                    }
                    throw new InvalidOperationException(
                        $"recipe loop through '{items.IdOf(item)}' never converges and contains no pin; the solve is inconsistent");
                }
                components.Add(new BomComponent(members, loop));
            }
        }
        components.Reverse();
        return (components, null);
    }

    public IReadOnlyList<int> Children(
        BomItems items, CostTable costs, BomPins pins, IReadOnlyDictionary<int, LoopSeed> seeds, int item)
    {
        if (items.IsLeaf(item) || !items.IsIndexed(item))
        {
            return [];
        }
        var recipe = pins.Chosen(costs, item);
        if (recipe < 0)
        {
            return [];
        }
        var children = PickedItems(costs, recipe, costs.PicksFor(item, recipe)).ToList();
        if (seeds.TryGetValue(item, out var seed))
        {
            children.AddRange(PickedItems(costs, seed.Recipe, seed.Picks));
        }
        return children;
    }

    public bool Reaches(
        BomItems items, CostTable costs, BomPins pins, IReadOnlyDictionary<int, LoopSeed> seeds,
        IEnumerable<int> from, IReadOnlySet<int> members)
    {
        var pending = new Stack<int>(from);
        var seen = new HashSet<int>();
        while (pending.TryPop(out var item))
        {
            if (members.Contains(item))
            {
                return true;
            }
            if (!seen.Add(item))
            {
                continue;
            }
            foreach (var child in Children(items, costs, pins, seeds, item))
            {
                pending.Push(child);
            }
        }
        return false;
    }

    private static IEnumerable<int> PickedItems(CostTable costs, int recipe, int[] picks)
    {
        var index = costs.Index;
        for (var s = 0; s < picks.Length; s++)
        {
            yield return index.AlternativeItem[index.AlternativeAt(recipe, s, picks[s])];
        }
    }
}
