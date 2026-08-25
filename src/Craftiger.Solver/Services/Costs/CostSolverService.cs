using Craftiger.Solver.Interfaces.Costs;
using Craftiger.Solver.Models.Costs;
using Craftiger.Solver.Models.Graph;
using Craftiger.Solver.Models.Options;
using Microsoft.Extensions.Options;

namespace Craftiger.Solver.Services.Costs;

public sealed class CostSolverService(
    ILeafWeightService leafWeights,
    IGarageLegalityService legality,
    IRoutePreferenceService routePreference,
    IOptions<CostSolverOptions> options) : ICostSolverService
{
    private readonly CostSolverOptions _options = options.Value;

    /// <summary>The strict-improvement fixpoint in graph order: a recipe only wins where it beats what an output already costs, so cycles starve and the pointers stay acyclic; a recipe consuming nothing has no material cost to offer and never prices.</summary>
    public CostTable Solve(SolverGraph graph, Garage garage, WeightSettings weights)
    {
        var index = graph.Index;
        var priceable = new bool[index.RecipeCount];
        var queue = new Queue<int>();
        var queued = new bool[index.RecipeCount];
        for (var r = 0; r < index.RecipeCount; r++)
        {
            priceable[r] = !index.ConsumesNothing(r) && legality.IsLegal(index, r, garage);
            if (priceable[r])
            {
                queue.Enqueue(r);
                queued[r] = true;
            }
        }

        var seeds = leafWeights.Resolve(graph, weights);
        var table = new CostTableBuilder(index, seeds);
        var budget = (long)queue.Count * _options.MaxPassesPerRecipe;
        while (queue.TryDequeue(out var recipe))
        {
            queued[recipe] = false;
            if (budget-- <= 0)
            {
                return table.Build(converged: false);
            }

            var total = table.SlotTotal(recipe);
            if (double.IsPositiveInfinity(total))
            {
                continue;
            }

            // The picks are taken once, at the prices this win was built from: an alternative that only ties them later could close a loop.
            ReadOnlySpan<ushort> picks = default;
            var picked = false;
            for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
            {
                var item = index.OutputItem[o];
                var candidate = total / index.OutputYield[o];
                if (candidate >= table.Cost(item) - _options.Epsilon)
                {
                    continue;
                }
                if (!picked)
                {
                    picks = table.ScratchPicks(recipe);
                    picked = true;
                }
                table.Win(item, recipe, candidate, picks);
                for (var c = index.ConsumerStart[item]; c < index.ConsumerStart[item + 1]; c++)
                {
                    var consumer = index.ConsumerRecipe[c];
                    if (priceable[consumer] && !queued[consumer])
                    {
                        queued[consumer] = true;
                        queue.Enqueue(consumer);
                    }
                }
            }
        }

        routePreference.Apply(table, priceable, seeds);
        return table.Build(converged: true);
    }
}
