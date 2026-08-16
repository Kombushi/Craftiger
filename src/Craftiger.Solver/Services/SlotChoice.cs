using Craftiger.Solver.Models;

namespace Craftiger.Solver.Services;

/// <summary>The one definition of which alternative a slot resolves to — the cost engine's
/// reroute guard and the BOM walk must always agree on it.</summary>
public static class SlotChoice
{
    /// <summary>The slot's cheapest alternative, first on ties, first again when every
    /// alternative is unreachable — the walk then surfaces that child as unreachable.</summary>
    public static SolverStack Cheapest(SolverSlot slot, IReadOnlyDictionary<string, double> costs)
    {
        SolverStack? best = null;
        var bestCost = double.PositiveInfinity;
        foreach (var alternative in slot.Alternatives)
        {
            var total = costs.TryGetValue(alternative.ItemId, out var unit)
                ? unit * alternative.Amount
                : double.PositiveInfinity;
            if (best is null || total < bestCost)
            {
                best = alternative;
                bestCost = total;
            }
        }
        return best!;
    }
}
