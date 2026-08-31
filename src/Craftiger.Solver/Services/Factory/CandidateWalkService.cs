using Craftiger.Solver.Interfaces.Costs;
using Craftiger.Solver.Interfaces.Factory;
using Craftiger.Solver.Models.Costs;
using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;
using Craftiger.Solver.Models.Options;
using Microsoft.Extensions.Options;

namespace Craftiger.Solver.Services.Factory;

public sealed class CandidateWalkService(IGarageLegalityService legality, IOptions<FactorySolverOptions> options) : ICandidateWalkService
{
    private readonly FactorySolverOptions _options = options.Value;

    /// <summary>The downstream cone of every consume target, then the garage-legal upstream closure of the targets, fuels and cone co-inputs through every slot alternative and through leaves; recipes outside the cost band are pruned before the walk recurses into them, pinned ones always survive.</summary>
    public CandidateSet Walk(
        FactoryContext context, IEnumerable<int> targets, IEnumerable<int> consumed, IReadOnlyDictionary<string, string> pins,
        bool mobFarms)
    {
        var index = context.Index;
        var pinned = new HashSet<int>();
        foreach (var recipeId in pins.Values)
        {
            if (index.TryGetRecipe(recipeId, out var pin))
            {
                pinned.Add(pin);
            }
        }

        var candidates = new HashSet<int>();
        var rejected = new HashSet<int>();

        bool Admit(int recipe, IReadOnlySet<int>? free = null)
        {
            if (candidates.Contains(recipe) || rejected.Contains(recipe)
                || !legality.IsLegal(index, recipe, context.Garage)
                || (index.ScopeOf(recipe) == RecipeScope.FactoryMob && !mobFarms)
                || !context.Environment.Admits(context.Recipes, recipe, context.Garage))
            {
                return false;
            }
            if (!pinned.Contains(recipe) && !WithinCostBand(context, recipe, free))
            {
                rejected.Add(recipe);
                return false;
            }
            candidates.Add(recipe);
            return true;
        }

        // Supplied items are free to their consumers: the cost engine cannot price them, but a consume target delivers them at no cost.
        var supplied = consumed.ToHashSet();
        var cone = new HashSet<int>();
        var pending = new Stack<int>();
        var downSeen = new HashSet<int>();
        var downPending = new Stack<int>(supplied);
        while (downPending.TryPop(out var item))
        {
            if (!downSeen.Add(item))
            {
                continue;
            }
            for (var c = index.ConsumerStart[item]; c < index.ConsumerStart[item + 1]; c++)
            {
                var recipe = index.ConsumerRecipe[c];
                if (!Admit(recipe, supplied))
                {
                    continue;
                }
                cone.Add(recipe);
                for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
                {
                    downPending.Push(index.OutputItem[o]);
                }
                for (var a = index.FirstAlternative(recipe); a < index.EndAlternative(recipe); a++)
                {
                    pending.Push(index.AlternativeItem[a]);
                }
            }
        }

        var seen = new HashSet<int>();
        foreach (var target in targets)
        {
            pending.Push(target);
        }
        while (pending.TryPop(out var item))
        {
            if (!seen.Add(item))
            {
                continue;
            }
            for (var p = index.ProducerStart[item]; p < index.ProducerStart[item + 1]; p++)
            {
                var recipe = index.ProducerRecipe[p];
                if (!Admit(recipe))
                {
                    continue;
                }
                for (var a = index.FirstAlternative(recipe); a < index.EndAlternative(recipe); a++)
                {
                    pending.Push(index.AlternativeItem[a]);
                }
            }
        }
        return new CandidateSet([.. candidates.Order()], cone, rejected.Count > 0);
    }

    /// <summary>Whether some output of the recipe prices within the band of its solved cost, the free items costing nothing.</summary>
    private bool WithinCostBand(FactoryContext context, int recipe, IReadOnlySet<int>? free)
    {
        var index = context.Index;
        var costs = context.Costs;
        for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
        {
            var item = index.OutputItem[o];
            if (!costs.TryCost(item, out var solved))
            {
                continue;
            }
            var candidate = free is null
                ? costs.Candidate(recipe, item)
                : FreeAwareCandidate(context, recipe, item, free);
            if (candidate <= _options.PruneFactor * solved + _options.PruneFloor)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The recipe's cost for the item with the free set priced at zero, over its expected yield.</summary>
    private static double FreeAwareCandidate(FactoryContext context, int recipe, int item, IReadOnlySet<int> free)
    {
        var total = CostArithmetic.SlotTotal(context.Index, recipe, context.Costs.Costs, free);
        if (double.IsPositiveInfinity(total))
        {
            return double.PositiveInfinity;
        }
        var yield = context.Index.Yield(recipe, item);
        return yield > 0 ? total / yield : double.PositiveInfinity;
    }
}
