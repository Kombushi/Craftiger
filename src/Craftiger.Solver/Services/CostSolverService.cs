using Craftiger.Solver.Interfaces;
using Craftiger.Solver.Models;

namespace Craftiger.Solver.Services;

public sealed class CostSolverService(
    ILeafWeightService leafWeights, IGarageLegalityService legality) : ICostSolverService
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
        foreach (var recipe in legal)
        {
            foreach (var itemId in recipe.Slots
                .SelectMany(slot => slot.Alternatives)
                .Select(a => a.ItemId)
                .Distinct())
            {
                if (!consumers.TryGetValue(itemId, out var list))
                {
                    consumers[itemId] = list = [];
                }
                list.Add(recipe);
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
}
