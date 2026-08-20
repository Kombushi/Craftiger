using Craftiger.Solver.Models;

namespace Craftiger.Solver.Services;

/// <summary>The one definition of which alternative a slot resolves to — the cost engine's
/// reroute guard, the BOM walk, and the item detail must always agree on it.</summary>
public static class SlotChoice
{
    /// <summary>The recipe's input stack per slot when it produces the item: the stacks the solve
    /// priced it with where it is the item's best recipe — a later price drop that merely ties
    /// another alternative never reopens the choice, so the pointer DAG stays acyclic — and the
    /// cheapest alternative for any other recipe.</summary>
    public static IReadOnlyList<SolverStack> Inputs(CostTable table, string itemId, SolverRecipe recipe) =>
        table.BestRecipes.TryGetValue(itemId, out var best) && best.Id == recipe.Id
        && table.ChosenInputs.TryGetValue(itemId, out var chosen)
            ? chosen
            : recipe.Slots.Select(slot => Cheapest(slot, table.Costs)).ToList();

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
