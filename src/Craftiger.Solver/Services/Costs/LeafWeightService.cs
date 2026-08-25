using Craftiger.Solver.Interfaces.Costs;
using Craftiger.Solver.Models.Costs;
using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Services.Costs;

public sealed class LeafWeightService : ILeafWeightService
{
    /// <summary>Per leaf: user override, else shipped weight, else B × 4^tier, else parent ÷ divisor, else 1; parents resolve first, so every fraction settles in one pass.</summary>
    public IReadOnlyDictionary<string, double> Resolve(SolverGraph graph, WeightSettings weights)
    {
        var resolved = new Dictionary<string, double>();
        var fractions = new List<SolverItem>();
        foreach (var item in graph.Items.Values)
        {
            if (!item.IsLeaf)
            {
                continue;
            }
            if (item.IsFraction && !weights.ItemWeights.ContainsKey(item.Id))
            {
                fractions.Add(item);
                continue;
            }
            resolved[item.Id] = Base(item, weights);
        }

        foreach (var item in fractions)
        {
            // A fixture may break the artifact's priced-parent promise; the fraction then stays unpriced.
            if (resolved.TryGetValue(item.Parent!.ParentItemId, out var parent))
            {
                resolved[item.Id] = parent / item.Parent.Divisor;
            }
        }
        return resolved;
    }

    private static double Base(SolverItem item, WeightSettings weights)
    {
        if (weights.ItemWeights.TryGetValue(item.Id, out var over))
        {
            return over;
        }
        if (item.Weight is { } shipped)
        {
            return shipped;
        }
        return item.Tier is { } tier ? weights.PriceBase * Math.Pow(4, tier) : 1;
    }
}
