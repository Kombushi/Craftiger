namespace Craftiger.Builder.Models.Planner;

/// <summary>Arithmetic over a recipe's output rows: merging twins and netting returned containers.</summary>
public static class PlannerOutputs
{
    /// <summary>Rows of the same item at the same chance collapse into one.</summary>
    public static IReadOnlyList<PlannerOutput> Merge(IEnumerable<PlannerOutput> outputs) =>
        outputs
            .GroupBy(o => (o.ItemId, o.Chance))
            .Select(g => new PlannerOutput(g.Key.ItemId, g.Sum(o => o.Amount), g.Key.Chance))
            .ToList();

    /// <summary>Nets items appearing on both sides, e.g. returned empty containers; chanced rows never net.</summary>
    public static (IReadOnlyDictionary<string, long> Inputs, IReadOnlyList<PlannerOutput> Outputs) Net(
        IReadOnlyDictionary<string, long> inputs, IReadOnlyList<PlannerOutput> outputs)
    {
        var netInputs = new Dictionary<string, long>(inputs);
        var netOutputs = new List<PlannerOutput>(outputs);
        for (var i = netOutputs.Count - 1; i >= 0; i--)
        {
            var o = netOutputs[i];
            if (o.Chance < 1.0 || !netInputs.TryGetValue(o.ItemId, out var inAmount))
            {
                continue;
            }

            var netted = Math.Min(inAmount, o.Amount);
            if (inAmount == netted)
            {
                netInputs.Remove(o.ItemId);
            }
            else
            {
                netInputs[o.ItemId] = inAmount - netted;
            }

            if (o.Amount == netted)
            {
                netOutputs.RemoveAt(i);
            }
            else
            {
                netOutputs[i] = o with { Amount = o.Amount - netted };
            }
        }
        return (netInputs, netOutputs);
    }
}
