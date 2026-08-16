using Craftiger.Solver.Interfaces;
using Craftiger.Solver.Models;

namespace Craftiger.Solver.Services;

public sealed class LeafWeightService : ILeafWeightService
{
    /// <summary>Per leaf: user override, else shipped weight, else B × 4^tier, else parent ÷
    /// divisor, else the flat class default of 1. Parents are plain tiered leaves, never
    /// fractions themselves, so resolving them first settles every fraction in one pass —
    /// and a fraction follows its parent's override, since the two are the same material.</summary>
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
            if (item.Parent is not null
                && !weights.ItemWeights.ContainsKey(item.Id) && item.Weight is null)
            {
                fractions.Add(item);
                continue;
            }
            resolved[item.Id] = Base(item, weights);
        }

        foreach (var item in fractions)
        {
            // The artifact guarantees a priced parent; a fixture that breaks that promise
            // simply leaves the fraction unpriced, mirroring the builder's pruning.
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
