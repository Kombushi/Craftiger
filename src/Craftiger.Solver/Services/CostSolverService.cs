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
    /// the bestRecipe pointers stay acyclic. Recipes are evaluated first-in first-out in graph
    /// order, and that order decides which of two exactly tied recipes an item records — so
    /// the loop runs over the graph's integer index, where nothing depends on the layout, and
    /// translates back to ids only at the end. A price that is not known yet is NaN: every
    /// comparison against it is false, which is exactly "absent loses to any candidate".</summary>
    public CostTable Solve(SolverGraph graph, Garage garage, WeightSettings weights)
    {
        var index = graph.Index;
        var recipeCount = index.Recipes.Length;
        var legal = new bool[recipeCount];
        var legalCount = 0;
        for (var r = 0; r < recipeCount; r++)
        {
            legal[r] = legality.IsLegal(index.Recipes[r], garage);
            if (legal[r])
            {
                legalCount++;
            }
        }

        var seeds = leafWeights.Resolve(graph, weights);
        var cost = new double[index.ItemCount];
        Array.Fill(cost, double.NaN);
        foreach (var (id, weight) in seeds)
        {
            cost[index.ItemIndex[id]] = weight;
        }
        var best = new int[index.ItemCount];
        Array.Fill(best, -1);
        var chosen = new int[index.ItemCount][];
        // Items in the order they first won a recipe: the reroute pass visits them in that order
        // and its outcome on ties depends on it, so the order is kept explicitly.
        var won = new List<int>();

        var queue = new Queue<int>(legalCount);
        var queued = new bool[recipeCount];
        for (var r = 0; r < recipeCount; r++)
        {
            if (legal[r])
            {
                queue.Enqueue(r);
                queued[r] = true;
            }
        }
        var budget = (long)legalCount * MaxPassesPerRecipe;
        while (queue.TryDequeue(out var recipe))
        {
            queued[recipe] = false;
            if (budget-- <= 0)
            {
                return Materialize(index, cost, best, chosen, won, seeds, converged: false);
            }

            var total = SlotTotal(index, recipe, cost);
            if (double.IsPositiveInfinity(total))
            {
                continue;
            }

            int[]? picks = null;
            for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
            {
                var item = index.OutputItem[o];
                var candidate = total / index.OutputYield[o];
                if (candidate >= cost[item] - Epsilon)
                {
                    continue;
                }
                // The walk must follow the stacks this price was built from: an alternative that
                // only ties them later (the recipe's own output, once priced) could close a loop.
                picks ??= Picks(index, recipe, cost);
                if (best[item] < 0)
                {
                    won.Add(item);
                }
                cost[item] = candidate;
                best[item] = recipe;
                chosen[item] = picks;
                for (var c = index.ConsumerStart[item]; c < index.ConsumerStart[item + 1]; c++)
                {
                    var consumer = index.ConsumerRecipe[c];
                    if (legal[consumer] && !queued[consumer])
                    {
                        queued[consumer] = true;
                        queue.Enqueue(consumer);
                    }
                }
            }
        }

        PreferForms(index, legal, cost, best, chosen, won, seeds);
        return Materialize(index, cost, best, chosen, won, seeds, converged: true);
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

    private static double SlotTotal(SolverIndex index, int recipe, double[] cost)
    {
        var total = 0.0;
        for (var s = index.SlotStart[recipe]; s < index.SlotStart[recipe + 1]; s++)
        {
            var cheapest = double.PositiveInfinity;
            for (var a = index.AlternativeStart[s]; a < index.AlternativeStart[s + 1]; a++)
            {
                var stack = cost[index.AlternativeItem[a]] * index.AlternativeAmount[a];
                if (stack < cheapest)
                {
                    cheapest = stack;
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

    private static double Candidate(SolverIndex index, int recipe, int item, double[] cost)
    {
        var total = SlotTotal(index, recipe, cost);
        if (double.IsPositiveInfinity(total))
        {
            return double.PositiveInfinity;
        }

        var candidate = double.PositiveInfinity;
        for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
        {
            if (index.OutputItem[o] == item)
            {
                candidate = Math.Min(candidate, total / index.OutputYield[o]);
            }
        }
        return candidate;
    }

    /// <summary>The alternative each slot resolves to at the current prices — the same rule as
    /// <see cref="SlotChoice.Cheapest"/>: first strictly cheaper wins, the first on ties and
    /// when no alternative is priced.</summary>
    private static int[] Picks(SolverIndex index, int recipe, double[] cost)
    {
        var first = index.SlotStart[recipe];
        var picks = new int[index.SlotStart[recipe + 1] - first];
        for (var s = 0; s < picks.Length; s++)
        {
            var start = index.AlternativeStart[first + s];
            var bestCost = double.PositiveInfinity;
            picks[s] = 0;
            for (var a = start; a < index.AlternativeStart[first + s + 1]; a++)
            {
                var unit = cost[index.AlternativeItem[a]];
                var stack = double.IsNaN(unit) ? double.PositiveInfinity : unit * index.AlternativeAmount[a];
                if (a == start || stack < bestCost)
                {
                    picks[s] = a - start;
                    bestCost = stack;
                }
            }
        }
        return picks;
    }

    /// <summary>The item behind slot <paramref name="slot"/> of the recipe under the given picks.</summary>
    private static int PickedItem(SolverIndex index, int recipe, int[] picks, int slot) =>
        index.AlternativeItem[index.AlternativeStart[index.SlotStart[recipe] + slot] + picks[slot]];

    /// <summary>Reroutes ties toward better routes (§5): where another legal producer offers
    /// the same price, the pointer moves to the one with the better composite score — form
    /// rank first, then chain depth of the chosen inputs (a leaf beats a detour through
    /// intermediates), then the leaf weight of the chosen leaves (a lighter-era material
    /// beats a heavier one) — unless the new recipe's inputs can reach the item over chosen
    /// edges, leaves included, which would close a pointer loop. Costs never change here,
    /// only pointers.</summary>
    private void PreferForms(
        SolverIndex index, bool[] legal, double[] cost, int[] best, int[][] chosen, List<int> won,
        IReadOnlyDictionary<string, double> weights)
    {
        var depths = Depths(index, best, chosen, won);
        var rank = new int[index.ItemCount];
        var leafWeight = new double[index.ItemCount];
        for (var i = 0; i < index.ItemCount; i++)
        {
            rank[i] = preferences.Rank(index.LeafClass[i]);
            if (index.IsLeaf(i))
            {
                leafWeight[i] = weights.GetValueOrDefault(index.ItemIds[i]);
            }
        }

        var reach = new ReachWalk(index.ItemCount);
        var candidates = new List<(int Recipe, int[] Inputs, (int, int, double) Score)>();
        foreach (var item in won)
        {
            var current = best[item];
            var currentScore = Score(index, current, chosen[item], rank, depths, leafWeight);
            candidates.Clear();
            for (var p = index.ProducerStart[item]; p < index.ProducerStart[item + 1]; p++)
            {
                var producer = index.ProducerRecipe[p];
                if (producer == current || !legal[producer])
                {
                    continue;
                }
                var inputs = Picks(index, producer, cost);
                var score = Score(index, producer, inputs, rank, depths, leafWeight);
                if (score.CompareTo(currentScore) < 0)
                {
                    candidates.Add((producer, inputs, score));
                }
            }
            // Stable by score, so equally scored producers keep graph order.
            for (var i = 1; i < candidates.Count; i++)
            {
                var entry = candidates[i];
                var j = i - 1;
                while (j >= 0 && candidates[j].Score.CompareTo(entry.Score) > 0)
                {
                    candidates[j + 1] = candidates[j];
                    j--;
                }
                candidates[j + 1] = entry;
            }
            foreach (var (candidate, inputs, _) in candidates)
            {
                if (Candidate(index, candidate, item, cost) > cost[item] + Epsilon
                    || reach.Reaches(index, best, chosen, candidate, inputs, item))
                {
                    continue;
                }
                best[item] = candidate;
                chosen[item] = inputs;
                break;
            }
        }
    }

    /// <summary>The tie-break key of a recipe over its chosen inputs: worst form rank, deepest
    /// chain, heaviest chosen leaf. Lexicographically smaller is better.</summary>
    private static (int Rank, int Depth, double Weight) Score(
        SolverIndex index, int recipe, int[] picks, int[] rank, int[] depths, double[] leafWeight)
    {
        var worstRank = 0;
        var depth = 0;
        var weight = 0.0;
        for (var s = 0; s < picks.Length; s++)
        {
            var item = PickedItem(index, recipe, picks, s);
            worstRank = Math.Max(worstRank, rank[item]);
            depth = Math.Max(depth, depths[item]);
            if (index.IsLeaf(item))
            {
                weight = Math.Max(weight, leafWeight[item]);
            }
        }
        return (worstRank, depth, weight);
    }

    /// <summary>Chain depth per item over chosen edges: leaves and unproduced items sit at 0,
    /// an expanded item one step past its deepest chosen input. The bestRecipe DAG is acyclic
    /// (§5), so a post-order walk settles every item once.</summary>
    private static int[] Depths(SolverIndex index, int[] best, int[][] chosen, List<int> roots)
    {
        var depths = new int[index.ItemCount];
        var started = new bool[index.ItemCount];
        var done = new bool[index.ItemCount];
        var stack = new List<(int Item, int Next)>();
        foreach (var root in roots)
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
                if (index.IsLeaf(item) || best[item] < 0)
                {
                    depths[item] = 0;
                    done[item] = true;
                    stack.RemoveAt(stack.Count - 1);
                    continue;
                }
                var picks = chosen[item];
                if (next < picks.Length)
                {
                    stack[^1] = (item, next + 1);
                    var child = PickedItem(index, best[item], picks, next);
                    if (!done[child] && !started[child])
                    {
                        started[child] = true;
                        stack.Add((child, 0));
                    }
                }
                else
                {
                    var deepest = 0;
                    for (var s = 0; s < picks.Length; s++)
                    {
                        deepest = Math.Max(deepest, depths[PickedItem(index, best[item], picks, s)]);
                    }
                    depths[item] = deepest + 1;
                    done[item] = true;
                    stack.RemoveAt(stack.Count - 1);
                }
            }
        }
        return depths;
    }

    /// <summary>Reachability over chosen edges with its scratch arrays reused across calls — a
    /// stamp per walk instead of a fresh visited set each time. The walk runs through produced
    /// leaves — the BOM stops at them, but a pointer loop hiding behind one would explain two
    /// forms' prices with each other.</summary>
    private sealed class ReachWalk(int itemCount)
    {
        private readonly int[] _seenAt = new int[itemCount];
        private readonly Stack<int> _pending = new();
        private int _stamp;

        public bool Reaches(SolverIndex index, int[] best, int[][] chosen, int recipe, int[] picks, int target)
        {
            _stamp++;
            _pending.Clear();
            for (var s = 0; s < picks.Length; s++)
            {
                _pending.Push(PickedItem(index, recipe, picks, s));
            }
            while (_pending.TryPop(out var item))
            {
                if (item == target)
                {
                    return true;
                }
                if (_seenAt[item] == _stamp || best[item] < 0)
                {
                    continue;
                }
                _seenAt[item] = _stamp;
                var inputs = chosen[item];
                for (var s = 0; s < inputs.Length; s++)
                {
                    _pending.Push(PickedItem(index, best[item], inputs, s));
                }
            }
            return false;
        }
    }

    /// <summary>Back to the id-keyed table every reader consumes. Insertion order is kept as
    /// the dictionaries always had it: leaves in weight order, then items as they first won.</summary>
    private static CostTable Materialize(
        SolverIndex index, double[] cost, int[] best, int[][] chosen, List<int> won,
        IReadOnlyDictionary<string, double> seeds, bool converged)
    {
        var costs = new Dictionary<string, double>(seeds.Count + won.Count);
        foreach (var id in seeds.Keys)
        {
            costs[id] = cost[index.ItemIndex[id]];
        }
        var bestRecipes = new Dictionary<string, SolverRecipe>(won.Count);
        var chosenInputs = new Dictionary<string, IReadOnlyList<SolverStack>>(won.Count);
        foreach (var item in won)
        {
            var id = index.ItemIds[item];
            var recipe = index.Recipes[best[item]];
            var picks = chosen[item];
            var stacks = new SolverStack[picks.Length];
            for (var s = 0; s < picks.Length; s++)
            {
                stacks[s] = recipe.Slots[s].Alternatives[picks[s]];
            }
            costs[id] = cost[item];
            bestRecipes[id] = recipe;
            chosenInputs[id] = stacks;
        }
        return new CostTable(costs, bestRecipes, chosenInputs, converged);
    }
}
