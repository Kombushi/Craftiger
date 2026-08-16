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

        var cost = new Dictionary<string, double>(leafWeights.Resolve(graph, weights));
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

        PreferSolidForms(graph, producers, cost, best);
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

    /// <summary>Reroutes ties toward solid forms (§5): where an item's chosen recipe consumes
    /// a deprioritized leaf and another legal producer offers the same price without one, the
    /// pointer moves — unless the new recipe's inputs can reach the item over chosen edges,
    /// which would hand the BOM walk a cycle. Costs never change here, only pointers.</summary>
    private void PreferSolidForms(
        SolverGraph graph, Dictionary<string, List<SolverRecipe>> producers,
        Dictionary<string, double> cost, Dictionary<string, SolverRecipe> best)
    {
        foreach (var (itemId, current) in best.ToList())
        {
            if (!ConsumesDeprioritized(graph, current, cost))
            {
                continue;
            }
            foreach (var candidate in producers.GetValueOrDefault(itemId) ?? [])
            {
                if (ReferenceEquals(candidate, current)
                    || ConsumesDeprioritized(graph, candidate, cost)
                    || Candidate(candidate, itemId, cost) > cost[itemId] + Epsilon
                    || Reaches(graph, cost, best, candidate, itemId))
                {
                    continue;
                }
                best[itemId] = candidate;
                break;
            }
        }
    }

    private bool ConsumesDeprioritized(
        SolverGraph graph, SolverRecipe recipe, IReadOnlyDictionary<string, double> costs) =>
        recipe.Slots.Any(slot => preferences.Deprioritizes(
            graph.Items.GetValueOrDefault(SlotChoice.Cheapest(slot, costs).ItemId)?.LeafClass));

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
