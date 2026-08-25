using Craftiger.Solver.Interfaces.Costs;
using Craftiger.Solver.Models.Costs;
using Craftiger.Solver.Models.Graph;
using Craftiger.Solver.Models.Options;
using Microsoft.Extensions.Options;

namespace Craftiger.Solver.Services.Costs;

public sealed class RoutePreferenceService(
    IOptions<SolverPreferences> preferences,
    IOptions<CostSolverOptions> options) : IRoutePreferenceService
{
    private readonly SolverPreferences _preferences = preferences.Value;
    private readonly CostSolverOptions _options = options.Value;

    /// <summary>Where another priceable producer offers the same price, the pointer moves to the best-scoring one — unless its inputs reach the item over chosen edges, which would close a pointer loop.</summary>
    public void Apply(CostTableBuilder table, IReadOnlyList<bool> priceable, IReadOnlyDictionary<string, double> leafWeights)
    {
        var index = table.Index;
        var depths = Depths(table);
        var rank = new int[index.ItemCount];
        var leafWeight = new double[index.ItemCount];
        for (var i = 0; i < index.ItemCount; i++)
        {
            rank[i] = _preferences.Rank(index.LeafClass[i]);
            if (index.IsLeaf(i))
            {
                leafWeight[i] = leafWeights.GetValueOrDefault(index.ItemIds[i]);
            }
        }

        var reach = new ReachWalk(index.ItemCount);
        foreach (var item in table.Won)
        {
            var current = table.BestRecipe(item);
            var currentScore = Score(table, current, table.Picks(item), rank, depths, leafWeight);
            var candidates = new List<(int Recipe, ushort[] Picks, RouteScore Score)>();
            for (var p = index.ProducerStart[item]; p < index.ProducerStart[item + 1]; p++)
            {
                var producer = index.ProducerRecipe[p];
                if (producer == current || !priceable[producer])
                {
                    continue;
                }
                var picks = table.CurrentPicks(producer);
                var score = Score(table, producer, picks, rank, depths, leafWeight);
                if (score.Beats(currentScore))
                {
                    candidates.Add((producer, picks, score));
                }
            }
            // A stable sort, so equally scored producers keep graph order.
            foreach (var (candidate, picks, _) in candidates.OrderBy(c => c.Score))
            {
                if (table.Candidate(candidate, item) > table.Cost(item) + _options.Epsilon
                    || reach.Reaches(table, candidate, picks, item))
                {
                    continue;
                }
                table.Reroute(item, candidate, picks);
                break;
            }
        }
    }

    private static RouteScore Score(
        CostTableBuilder table, int recipe, ReadOnlySpan<ushort> picks, int[] rank, int[] depths, double[] leafWeight)
    {
        var index = table.Index;
        var worstRank = 0;
        var depth = 0;
        var weight = 0.0;
        var slots = index.SlotCount(recipe);
        for (var s = 0; s < slots; s++)
        {
            var item = table.PickedItem(recipe, picks, s);
            worstRank = Math.Max(worstRank, rank[item]);
            depth = Math.Max(depth, depths[item]);
            if (index.IsLeaf(item))
            {
                weight = Math.Max(weight, leafWeight[item]);
            }
        }
        return new RouteScore(worstRank, depth, weight, index.ToolSlots[recipe]);
    }

    /// <summary>Chain depth per item over chosen edges: leaves and unproduced items at 0, an expanded item one past its deepest chosen input; the DAG is acyclic, so a post-order walk settles each item once.</summary>
    private static int[] Depths(CostTableBuilder table)
    {
        var index = table.Index;
        var depths = new int[index.ItemCount];
        var started = new bool[index.ItemCount];
        var done = new bool[index.ItemCount];
        var stack = new List<(int Item, int Next)>();
        foreach (var root in table.Won)
        {
            if (started[root])
            {
                continue;
            }
            started[root] = true;
            stack.Add((root, 0));
            while (stack.Count > 0)
            {
                var (item, next) = stack[^1];
                if (index.IsLeaf(item) || table.BestRecipe(item) < 0)
                {
                    depths[item] = 0;
                    done[item] = true;
                    stack.RemoveAt(stack.Count - 1);
                    continue;
                }
                var recipe = table.BestRecipe(item);
                var slots = index.SlotCount(recipe);
                if (next < slots)
                {
                    stack[^1] = (item, next + 1);
                    var child = table.PickedItem(recipe, table.Picks(item), next);
                    if (!done[child] && !started[child])
                    {
                        started[child] = true;
                        stack.Add((child, 0));
                    }
                }
                else
                {
                    var deepest = 0;
                    for (var s = 0; s < slots; s++)
                    {
                        deepest = Math.Max(deepest, depths[table.PickedItem(recipe, table.Picks(item), s)]);
                    }
                    depths[item] = deepest + 1;
                    done[item] = true;
                    stack.RemoveAt(stack.Count - 1);
                }
            }
        }
        return depths;
    }

    /// <summary>Reachability over chosen edges with scratch arrays reused across calls; the walk runs through produced leaves because the pointer graph itself must stay a DAG.</summary>
    private sealed class ReachWalk(int itemCount)
    {
        private readonly int[] _seenAt = new int[itemCount];
        private readonly Stack<int> _pending = new();
        private int _stamp;

        public bool Reaches(CostTableBuilder table, int recipe, ReadOnlySpan<ushort> picks, int target)
        {
            _stamp++;
            _pending.Clear();
            var slots = table.Index.SlotCount(recipe);
            for (var s = 0; s < slots; s++)
            {
                _pending.Push(table.PickedItem(recipe, picks, s));
            }
            while (_pending.TryPop(out var item))
            {
                if (item == target)
                {
                    return true;
                }
                if (_seenAt[item] == _stamp || table.BestRecipe(item) < 0)
                {
                    continue;
                }
                _seenAt[item] = _stamp;
                var viaRecipe = table.BestRecipe(item);
                var viaSlots = table.Index.SlotCount(viaRecipe);
                for (var s = 0; s < viaSlots; s++)
                {
                    _pending.Push(table.PickedItem(viaRecipe, table.Picks(item), s));
                }
            }
            return false;
        }
    }
}
