using Craftiger.Solver.Interfaces.Bom;
using Craftiger.Solver.Interfaces.Costs;
using Craftiger.Solver.Models.Bom;
using Craftiger.Solver.Models.Costs;

namespace Craftiger.Solver.Services.Bom;

public sealed class LoopSeedService(IGarageLegalityService legality, IChosenEdgeGraphService graph) : ILoopSeedService
{
    /// <summary>The route a player takes once to get the first unit: the cheapest legal producer whose inputs stay outside the loop.</summary>
    public LoopSeed? Seed(
        BomItems items, CostTable costs, Garage garage, BomPins pins,
        IReadOnlyDictionary<int, LoopSeed> seeds, IReadOnlyList<int> members)
    {
        var index = costs.Index;
        var memberSet = members.ToHashSet();
        LoopSeed? best = null;
        var bestCost = double.PositiveInfinity;
        foreach (var item in members)
        {
            var chosen = pins.Chosen(costs, item);
            for (var p = index.ProducerStart[item]; p < index.ProducerStart[item + 1]; p++)
            {
                var producer = index.ProducerRecipe[p];
                if (producer == chosen || !legality.IsLegal(index, producer, garage))
                {
                    continue;
                }
                var picks = costs.PicksFor(item, producer);
                var total = 0.0;
                var inputs = new List<int>(picks.Length);
                for (var s = 0; s < picks.Length; s++)
                {
                    var at = index.AlternativeAt(producer, s, picks[s]);
                    inputs.Add(index.AlternativeItem[at]);
                    total += costs.TryCost(index.AlternativeItem[at], out var unit)
                        ? unit * index.AlternativeAmount[at]
                        : double.PositiveInfinity;
                }
                var cost = total / index.Yield(producer, item);
                if (!(cost < bestCost) || graph.Reaches(items, costs, pins, seeds, inputs, memberSet))
                {
                    continue;
                }
                best = new LoopSeed(item, producer, picks);
                bestCost = cost;
            }
        }
        return best;
    }
}
