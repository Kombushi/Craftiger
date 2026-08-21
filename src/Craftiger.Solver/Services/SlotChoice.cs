using Craftiger.Solver.Models;

namespace Craftiger.Solver.Services;

/// <summary>The one definition of which alternative a slot resolves to — the cost engine's
/// reroute guard, the BOM walk, and the item detail must always agree on it.</summary>
public static class SlotChoice
{
    /// <summary>The alternative a slot of the recipe resolves to when it produces the item: the
    /// one the solve priced it with where it is the item's best recipe — a later price drop
    /// that merely ties another alternative never reopens the choice, so the pointer DAG stays
    /// acyclic — and the cheapest alternative for any other recipe.</summary>
    public static int Pick(CostTable table, int item, int recipe, int slot) =>
        table.BestRecipe(item) == recipe ? table.Picks(item)[slot] : Cheapest(table, recipe, slot);

    /// <summary>Every slot's pick for the recipe producing the item, in slot order.</summary>
    public static int[] Picks(CostTable table, int item, int recipe)
    {
        var picks = new int[table.Index.SlotCount(recipe)];
        for (var s = 0; s < picks.Length; s++)
        {
            picks[s] = Pick(table, item, recipe, s);
        }
        return picks;
    }

    /// <summary>The slot's cheapest alternative, first on ties, first again when every
    /// alternative is unreachable — the walk then surfaces that child as unreachable.</summary>
    public static int Cheapest(CostTable table, int recipe, int slot)
    {
        var index = table.Index;
        var best = 0;
        var bestCost = double.PositiveInfinity;
        var count = index.AlternativeCount(recipe, slot);
        for (var a = 0; a < count; a++)
        {
            var at = index.AlternativeAt(recipe, slot, a);
            var unit = table.Cost(index.AlternativeItem[at]);
            var total = double.IsNaN(unit) ? double.PositiveInfinity : unit * index.AlternativeAmount[at];
            if (a == 0 || total < bestCost)
            {
                best = a;
                bestCost = total;
            }
        }
        return best;
    }

    /// <summary>The recipe's input stack per slot when it produces the item, by id — the item
    /// detail's view of the same choice.</summary>
    public static IReadOnlyList<SolverStack> Inputs(CostTable table, string itemId, SolverRecipe recipe)
    {
        var index = table.Index;
        if (!index.RecipeIndex.TryGetValue(recipe.Id, out var r))
        {
            throw new ArgumentException($"recipe '{recipe.Id}' does not belong to the solved graph", nameof(recipe));
        }
        // An id the index does not know cannot have a best recipe; every slot falls to cheapest.
        var item = index.TryGetItem(itemId, out var known) ? known : -1;
        var stacks = new SolverStack[recipe.Slots.Count];
        for (var s = 0; s < stacks.Length; s++)
        {
            var pick = item >= 0 ? Pick(table, item, r, s) : Cheapest(table, r, s);
            stacks[s] = recipe.Slots[s].Alternatives[pick];
        }
        return stacks;
    }
}
