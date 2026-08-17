using Craftiger.Solver.Interfaces;
using Craftiger.Solver.Models;

namespace Craftiger.Solver.Services;

public sealed class CostSolverService(
    ILeafWeightService leafWeights, IGarageLegalityService legality,
    SolverPreferences preferences) : ICostSolverService
{
    private const double Epsilon = 1e-9;

    /// <summary>A duplication loop never settles on its own, so the walk is bounded.</summary>
    private const int MaxPassesPerRecipe = 200;

    /// <summary>The spec's strict-improvement fixpoint: leaves start at their weight, a recipe
    /// only wins where it strictly beats what an output already costs, so cycles starve and
    /// the bestRecipe pointers stay acyclic.</summary>
    public CostTable Solve(SolverGraph graph, Garage garage, WeightSettings weights)
    {
        var legal = graph.Recipes.Where(recipe => legality.IsLegal(recipe, garage)).ToList();

        var consumers = new Dictionary<string, List<SolverRecipe>>();
        var producers = new Dictionary<string, List<SolverRecipe>>();
        foreach (var recipe in legal)
        {
            foreach (var itemId in recipe.Slots
                .SelectMany(slot => slot.Alternatives)
                .Select(a => a.ItemId)
                .Distinct())
            {
                Index(consumers, itemId, recipe);
            }
            foreach (var itemId in recipe.Outputs.Select(output => output.ItemId).Distinct())
            {
                Index(producers, itemId, recipe);
            }
        }

        var seeds = leafWeights.Resolve(graph, weights);
        var cost = new Dictionary<string, double>(seeds);
        var best = new Dictionary<string, SolverRecipe>();
        var queue = new Queue<SolverRecipe>(legal);
        var queued = new HashSet<string>(legal.Select(recipe => recipe.Id));
        var budget = legal.Count * MaxPassesPerRecipe;
        while (queue.TryDequeue(out var recipe))
        {
            queued.Remove(recipe.Id);
            if (budget-- <= 0)
            {
                return new CostTable(cost, best, Converged: false);
            }

            var total = SlotTotal(recipe, cost);
            if (double.IsPositiveInfinity(total))
            {
                continue;
            }

            foreach (var output in recipe.Outputs)
            {
                var candidate = total / (output.Amount * output.Chance);
                if (cost.TryGetValue(output.ItemId, out var current) && candidate >= current - Epsilon)
                {
                    continue;
                }
                cost[output.ItemId] = candidate;
                best[output.ItemId] = recipe;
                foreach (var consumer in consumers.GetValueOrDefault(output.ItemId) ?? [])
                {
                    if (queued.Add(consumer.Id))
                    {
                        queue.Enqueue(consumer);
                    }
                }
            }
        }

        PreferForms(graph, producers, cost, best, seeds);
        return new CostTable(cost, best, Converged: true);
    }

    public double Candidate(SolverRecipe recipe, string itemId, IReadOnlyDictionary<string, double> costs)
    {
        var total = SlotTotal(recipe, costs);
        if (double.IsPositiveInfinity(total))
        {
            return double.PositiveInfinity;
        }

        var candidate = double.PositiveInfinity;
        foreach (var output in recipe.Outputs)
        {
            if (output.ItemId == itemId)
            {
                candidate = Math.Min(candidate, total / (output.Amount * output.Chance));
            }
        }
        return candidate;
    }

    /// <summary>Every slot at its cheapest alternative, or +∞ when one has no known price.</summary>
    private static double SlotTotal(SolverRecipe recipe, IReadOnlyDictionary<string, double> costs)
    {
        var total = 0.0;
        foreach (var slot in recipe.Slots)
        {
            var cheapest = double.PositiveInfinity;
            foreach (var alternative in slot.Alternatives)
            {
                if (costs.TryGetValue(alternative.ItemId, out var unit)
                    && unit * alternative.Amount < cheapest)
                {
                    cheapest = unit * alternative.Amount;
                }
            }
            if (double.IsPositiveInfinity(cheapest))
            {
                return double.PositiveInfinity;
            }
            total += cheapest;
        }
        return total;
    }

    /// <summary>Reroutes ties toward better routes (§5): where another legal producer offers
    /// the same price, the pointer moves to the one with the better composite score — form
    /// rank first, then chain depth of the chosen inputs (a leaf beats a detour through
    /// intermediates), then the leaf weight of the chosen leaves (a lighter-era material
    /// beats a heavier one) — unless the new recipe's inputs can reach the item over chosen
    /// edges, which would hand the BOM walk a cycle. Costs never change here, only
    /// pointers.</summary>
    private void PreferForms(
        SolverGraph graph, Dictionary<string, List<SolverRecipe>> producers,
        Dictionary<string, double> cost, Dictionary<string, SolverRecipe> best,
        IReadOnlyDictionary<string, double> weights)
    {
        var depths = Depths(graph, cost, best);
        foreach (var (itemId, current) in best.ToList())
        {
            var currentScore = Score(graph, current, cost, depths, weights);
            var candidates = (producers.GetValueOrDefault(itemId) ?? [])
                .Where(candidate => !ReferenceEquals(candidate, current))
                .Select(candidate => (
                    Recipe: candidate,
                    Score: Score(graph, candidate, cost, depths, weights)))
                .Where(candidate => candidate.Score.CompareTo(currentScore) < 0)
                .OrderBy(candidate => candidate.Score);
            foreach (var (candidate, _) in candidates)
            {
                if (Candidate(candidate, itemId, cost) > cost[itemId] + Epsilon
                    || Reaches(graph, cost, best, candidate, itemId))
                {
                    continue;
                }
                best[itemId] = candidate;
                break;
            }
        }
    }

    /// <summary>The recipe's tie-break key over its chosen inputs: worst form rank, deepest
    /// chain, heaviest chosen leaf. Lexicographically smaller is better.</summary>
    private (int Rank, int Depth, double Weight) Score(
        SolverGraph graph, SolverRecipe recipe, IReadOnlyDictionary<string, double> costs,
        IReadOnlyDictionary<string, int> depths, IReadOnlyDictionary<string, double> weights)
    {
        var rank = 0;
        var depth = 0;
        var weight = 0.0;
        foreach (var slot in recipe.Slots)
        {
            var chosen = SlotChoice.Cheapest(slot, costs).ItemId;
            rank = Math.Max(rank, preferences.Rank(graph.Items.GetValueOrDefault(chosen)?.LeafClass));
            depth = Math.Max(depth, depths.GetValueOrDefault(chosen));
            if (graph.IsLeaf(chosen))
            {
                weight = Math.Max(weight, weights.GetValueOrDefault(chosen));
            }
        }
        return (rank, depth, weight);
    }

    /// <summary>Chain depth per item over chosen edges: leaves and unproduced items sit at 0,
    /// an expanded item one step past its deepest chosen input. The bestRecipe DAG is acyclic
    /// (§5), so a post-order walk settles every item once.</summary>
    private static Dictionary<string, int> Depths(
        SolverGraph graph, IReadOnlyDictionary<string, double> cost,
        Dictionary<string, SolverRecipe> best)
    {
        var depths = new Dictionary<string, int>();
        var started = new HashSet<string>();
        var stack = new List<(string Id, int Next)>();
        foreach (var root in best.Keys)
        {
            if (!started.Add(root))
            {
                continue;
            }
            stack.Add((root, 0));
            while (stack.Count > 0)
            {
                var (id, next) = stack[^1];
                if (graph.IsLeaf(id) || !best.TryGetValue(id, out var recipe))
                {
                    depths[id] = 0;
                    stack.RemoveAt(stack.Count - 1);
                    continue;
                }
                if (next < recipe.Slots.Count)
                {
                    stack[^1] = (id, next + 1);
                    var child = SlotChoice.Cheapest(recipe.Slots[next], cost).ItemId;
                    if (!depths.ContainsKey(child) && started.Add(child))
                    {
                        stack.Add((child, 0));
                    }
                }
                else
                {
                    var deepest = 0;
                    foreach (var slot in recipe.Slots)
                    {
                        deepest = Math.Max(
                            deepest, depths.GetValueOrDefault(SlotChoice.Cheapest(slot, cost).ItemId));
                    }
                    depths[id] = deepest + 1;
                    stack.RemoveAt(stack.Count - 1);
                }
            }
        }
        return depths;
    }

    /// <summary>Whether the item is reachable from the recipe's chosen inputs over the same
    /// chosen edges the BOM walks — leaves and unpriced items are terminal there too.</summary>
    private static bool Reaches(
        SolverGraph graph, IReadOnlyDictionary<string, double> cost,
        Dictionary<string, SolverRecipe> best, SolverRecipe from, string target)
    {
        var pending = new Stack<string>(
            from.Slots.Select(slot => SlotChoice.Cheapest(slot, cost).ItemId));
        var seen = new HashSet<string>();
        while (pending.TryPop(out var itemId))
        {
            if (itemId == target)
            {
                return true;
            }
            if (!seen.Add(itemId) || graph.IsLeaf(itemId) || !best.TryGetValue(itemId, out var recipe))
            {
                continue;
            }
            foreach (var slot in recipe.Slots)
            {
                pending.Push(SlotChoice.Cheapest(slot, cost).ItemId);
            }
        }
        return false;
    }

    private static void Index(
        Dictionary<string, List<SolverRecipe>> index, string itemId, SolverRecipe recipe)
    {
        if (!index.TryGetValue(itemId, out var list))
        {
            index[itemId] = list = [];
        }
        list.Add(recipe);
    }
}
