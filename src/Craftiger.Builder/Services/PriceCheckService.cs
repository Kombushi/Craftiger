using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

/// <summary>A leaf weight is a ceiling routes may undercut; undercutting it by orders of magnitude means matter is created somewhere.</summary>
public sealed class PriceCheckService(IOptions<PricingConfiguration> options, ILogger<PriceCheckService> logger)
    : IPriceCheckService
{
    private const double Epsilon = 1e-9;

    /// <summary>A duplication loop never settles on its own, so the walk is bounded.</summary>
    private const int MaxPassesPerRecipe = 200;

    private readonly PricingConfiguration _config = options.Value;

    public PriceCheck Run(
        IReadOnlyList<PlannerRecipe> recipes,
        IReadOnlyDictionary<string, string> leafClasses,
        IReadOnlyDictionary<string, int> tiers,
        IReadOnlyDictionary<string, double> weights,
        UnifiedItems unified,
        Dump dump)
    {
        var leafWeights = LeafWeights(leafClasses, tiers, weights, unified, dump);
        var (cost, converged) = Solve(recipes, leafWeights);

        var undercut = leafWeights
            .Where(leaf => leaf.Value > 0
                && cost.TryGetValue(leaf.Key, out var solved)
                && solved < leaf.Value * _config.PriceLeakRatio)
            .Select(leaf => (Id: leaf.Key, Weight: leaf.Value, Solved: cost[leaf.Key]))
            .OrderBy(leak => leak.Solved / leak.Weight)
            .ToList();
        var free = cost
            .Where(item => item.Value <= 0 && leafWeights.GetValueOrDefault(item.Key, 1) > 0)
            .Select(item => item.Key)
            .ToList();

        if (!converged)
        {
            logger.LogWarning("price check gave up before settling; treat the counts below as a floor");
        }
        if (undercut.Count > 0)
        {
            logger.LogWarning(
                "{Count:N0} leaves price below {Ratio:G3} of their weight, worst first:",
                undercut.Count, _config.PriceLeakRatio);
            foreach (var (id, weight, solved) in undercut.Take(5))
            {
                logger.LogWarning(
                    "    {Name}: weight {Weight:N0}, priced {Solved:G3}", dump.NameOf(id), weight, solved);
            }
        }
        if (free.Count > 0)
        {
            logger.LogWarning("{Free:N0} items cost nothing at all, first:", free.Count);
            foreach (var id in free.Take(5))
            {
                logger.LogWarning("    {Name}", dump.NameOf(id));
            }
        }
        logger.LogInformation(
            "  {Priced:N0} of {Items:N0} items priced", cost.Count, Universe(recipes).Count);

        return new PriceCheck(undercut.Count, free.Count, converged);
    }

    /// <summary>Leaf prices at the defaults of the cost model; the app lets the user retune them.</summary>
    private Dictionary<string, double> LeafWeights(
        IReadOnlyDictionary<string, string> leafClasses, IReadOnlyDictionary<string, int> tiers,
        IReadOnlyDictionary<string, double> weights, UnifiedItems unified, Dump dump)
    {
        var leafWeights = new Dictionary<string, double>();
        foreach (var (id, leafClass) in leafClasses)
        {
            if (weights.TryGetValue(id, out var over))
            {
                leafWeights[id] = over;
            }
            else if (tiers.TryGetValue(id, out var tier))
            {
                leafWeights[id] = Tiered(tier);
            }
            else if (DerivedLeaf.ByClass.ContainsKey(leafClass))
            {
                var (parent, divisor) = DerivedLeaf.ParentsOf(id, leafClass, unified, dump.OrePrefixes)
                    .FirstOrDefault(p => tiers.ContainsKey(p.ParentId));
                if (parent is not null)
                {
                    leafWeights[id] = Tiered(tiers[parent]) / divisor;
                }
            }
            else
            {
                leafWeights[id] = 1;
            }
        }
        return leafWeights;
    }

    private double Tiered(int tier) => _config.PriceBase * Math.Pow(4, tier);

    /// <summary>The cost fixpoint of the solver's engine: a recipe only wins where it strictly beats what an output already costs, and one consuming nothing never prices.</summary>
    private static (Dictionary<string, double> Cost, bool Converged) Solve(
        IReadOnlyList<PlannerRecipe> recipes, Dictionary<string, double> leafWeights)
    {
        var consumers = new Dictionary<string, List<PlannerRecipe>>();
        foreach (var recipe in recipes)
        {
            foreach (var id in recipe.Ingredients.Select(part => part.ItemId).Distinct())
            {
                if (!consumers.TryGetValue(id, out var list))
                {
                    consumers[id] = list = [];
                }
                list.Add(recipe);
            }
        }

        var cost = new Dictionary<string, double>(leafWeights);
        var priceable = recipes.Where(recipe => !recipe.ConsumesNothing).ToList();
        var queue = new Queue<PlannerRecipe>(priceable);
        var queued = new HashSet<string>(priceable.Select(recipe => recipe.Id));
        var budget = priceable.Count * MaxPassesPerRecipe;
        while (queue.TryDequeue(out var recipe))
        {
            queued.Remove(recipe.Id);
            if (budget-- <= 0)
            {
                return (cost, false);
            }

            var total = 0.0;
            var known = true;
            foreach (var slot in recipe.Slots)
            {
                var cheapest = double.PositiveInfinity;
                foreach (var (itemId, amount) in slot)
                {
                    if (cost.TryGetValue(itemId, out var unit) && unit * amount < cheapest)
                    {
                        cheapest = unit * amount;
                    }
                }
                if (double.IsPositiveInfinity(cheapest))
                {
                    known = false;
                    break;
                }
                total += cheapest;
            }
            if (!known)
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
                foreach (var consumer in consumers.GetValueOrDefault(output.ItemId) ?? [])
                {
                    if (queued.Add(consumer.Id))
                    {
                        queue.Enqueue(consumer);
                    }
                }
            }
        }
        return (cost, true);
    }

    /// <summary>Every item the shipped recipes touch, which is what the artifacts hold.</summary>
    private static HashSet<string> Universe(IReadOnlyList<PlannerRecipe> recipes)
    {
        var ids = new HashSet<string>();
        foreach (var recipe in recipes)
        {
            ids.UnionWith(recipe.Ingredients.Select(part => part.ItemId));
            ids.UnionWith(recipe.Outputs.Select(output => output.ItemId));
        }
        return ids;
    }
}
