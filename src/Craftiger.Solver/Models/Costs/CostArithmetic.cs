using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Models.Costs;

/// <summary>The one definition of recipe pricing over a cost array; the fixpoint, the solved table and every reader must agree on it.</summary>
internal static class CostArithmetic
{
    /// <summary>Every slot at its cheapest alternative, or +∞ when one has no known price.</summary>
    public static double SlotTotal(SolverIndex index, int recipe, ReadOnlySpan<double> cost)
    {
        var total = 0.0;
        for (var s = index.SlotStart[recipe]; s < index.SlotStart[recipe + 1]; s++)
        {
            var cheapest = double.PositiveInfinity;
            for (var a = index.AlternativeStart[s]; a < index.AlternativeStart[s + 1]; a++)
            {
                var stack = cost[index.AlternativeItem[a]] * index.AlternativeAmount[a];
                if (stack < cheapest)
                {
                    cheapest = stack;
                }
            }
            if (double.IsPositiveInfinity(cheapest))
            {
                return double.PositiveInfinity;
            }
            total += cheapest;
        }
        return total;
    }

    /// <summary>The same total with the free items priced at zero.</summary>
    public static double SlotTotal(SolverIndex index, int recipe, ReadOnlySpan<double> cost, IReadOnlySet<int> free)
    {
        var total = 0.0;
        for (var s = index.SlotStart[recipe]; s < index.SlotStart[recipe + 1]; s++)
        {
            var cheapest = double.PositiveInfinity;
            for (var a = index.AlternativeStart[s]; a < index.AlternativeStart[s + 1]; a++)
            {
                var input = index.AlternativeItem[a];
                var stack = free.Contains(input) ? 0 : cost[input] * index.AlternativeAmount[a];
                if (stack < cheapest)
                {
                    cheapest = stack;
                }
            }
            if (double.IsPositiveInfinity(cheapest))
            {
                return double.PositiveInfinity;
            }
            total += cheapest;
        }
        return total;
    }

    /// <summary>The recipe's cost for one of its outputs: slot total over the best row's yield, +∞ where an input is unreachable.</summary>
    public static double Candidate(SolverIndex index, int recipe, int item, ReadOnlySpan<double> cost)
    {
        var total = SlotTotal(index, recipe, cost);
        if (double.IsPositiveInfinity(total))
        {
            return double.PositiveInfinity;
        }

        var candidate = double.PositiveInfinity;
        for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
        {
            if (index.OutputItem[o] == item)
            {
                candidate = Math.Min(candidate, total / index.OutputYield[o]);
            }
        }
        return candidate;
    }

    /// <summary>The slot's cheapest alternative: first strictly cheaper wins, the first on ties and when nothing is priced.</summary>
    public static int CheapestAlternative(SolverIndex index, int recipe, int slot, ReadOnlySpan<double> cost)
    {
        var best = 0;
        var bestCost = double.PositiveInfinity;
        var count = index.AlternativeCount(recipe, slot);
        for (var a = 0; a < count; a++)
        {
            var at = index.AlternativeAt(recipe, slot, a);
            var unit = cost[index.AlternativeItem[at]];
            var stack = double.IsNaN(unit) ? double.PositiveInfinity : unit * index.AlternativeAmount[at];
            if (a == 0 || stack < bestCost)
            {
                best = a;
                bestCost = stack;
            }
        }
        return best;
    }

    /// <summary>Every slot's cheapest alternative at the given prices, written into picks.</summary>
    public static void Picks(SolverIndex index, int recipe, ReadOnlySpan<double> cost, Span<ushort> picks)
    {
        var slots = index.SlotCount(recipe);
        for (var s = 0; s < slots; s++)
        {
            picks[s] = (ushort)CheapestAlternative(index, recipe, s, cost);
        }
    }

    /// <summary>The item behind a slot of the recipe under the given picks.</summary>
    public static int PickedItem(SolverIndex index, int recipe, ReadOnlySpan<ushort> picks, int slot) =>
        index.AlternativeItem[index.AlternativeAt(recipe, slot, picks[slot])];
}
