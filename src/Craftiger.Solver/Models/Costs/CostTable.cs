using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Models.Costs;

/// <summary>A solved cost table over the positions of its index: cost per item (NaN = unreachable), the recipe that set it, and the alternative each of that recipe's slots was priced with.</summary>
public sealed record CostTable
{
    private readonly ImmutableArray<double> _cost;
    private readonly ImmutableArray<int> _best;
    private readonly ImmutableArray<int> _pickStart;
    private readonly ImmutableArray<ushort> _picks;

    public CostTable(SolverIndex index, double[] cost, int[] best, int[] pickStart, ushort[] picks, bool converged)
    {
        Index = index;
        _cost = ImmutableCollectionsMarshal.AsImmutableArray(cost);
        _best = ImmutableCollectionsMarshal.AsImmutableArray(best);
        _pickStart = ImmutableCollectionsMarshal.AsImmutableArray(pickStart);
        _picks = ImmutableCollectionsMarshal.AsImmutableArray(picks);
        Converged = converged;
        var priced = 0;
        foreach (var value in cost)
        {
            if (!double.IsNaN(value))
            {
                priced++;
            }
        }
        PricedCount = priced;
    }

    /// <summary>The index this table was solved on; readers must address it with the same one.</summary>
    public SolverIndex Index { get; }

    public bool Converged { get; }

    public int PricedCount { get; }

    /// <summary>The table's storage — cost, best recipe and pick offset per position, then the picks — for a store keeping tables outside the process.</summary>
    public ReadOnlySpan<double> Costs => _cost.AsSpan();

    public ReadOnlySpan<int> BestRecipes => _best.AsSpan();

    public ReadOnlySpan<int> PickStarts => _pickStart.AsSpan();

    public ReadOnlySpan<ushort> PickArray => _picks.AsSpan();

    public bool IsPriced(int item) => !double.IsNaN(_cost[item]);

    /// <summary>The cost at a position, NaN when unpriced.</summary>
    public double Cost(int item) => _cost[item];

    public bool TryCost(int item, out double cost)
    {
        cost = _cost[item];
        return !double.IsNaN(cost);
    }

    /// <summary>The recipe position that priced the item, or -1.</summary>
    public int BestRecipe(int item) => _best[item];

    /// <summary>The alternative chosen per slot of the item's best recipe; empty without one.</summary>
    public ReadOnlySpan<ushort> Picks(int item) =>
        _best[item] < 0 ? [] : _picks.AsSpan(_pickStart[item], Index.SlotCount(_best[item]));

    /// <summary>The item position the given slot of the item's best recipe was priced with.</summary>
    public int PickedItem(int item, int slot) =>
        Index.AlternativeItem[Index.AlternativeAt(_best[item], slot, _picks[_pickStart[item] + slot])];

    /// <summary>One recipe's candidate cost for one of its outputs, +∞ where an input is unreachable.</summary>
    public double Candidate(int recipe, int item) => CostArithmetic.Candidate(Index, recipe, item, Costs);

    /// <summary>The same by id; an id the index does not know is unreachable.</summary>
    public double Candidate(int recipe, string itemId) =>
        Index.TryGetItem(itemId, out var item) ? Candidate(recipe, item) : double.PositiveInfinity;

    /// <summary>The slot's cheapest alternative at the solved prices, first on ties.</summary>
    public int CheapestAlternative(int recipe, int slot) =>
        CostArithmetic.CheapestAlternative(Index, recipe, slot, Costs);

    /// <summary>The alternative a slot resolves to when the recipe produces the item: the recorded pick where it is the item's best recipe, so a later tie never reopens the choice, else the cheapest.</summary>
    public int Pick(int item, int recipe, int slot) =>
        _best[item] == recipe ? _picks[_pickStart[item] + slot] : CheapestAlternative(recipe, slot);

    /// <summary>Every slot's pick for the recipe producing the item, in slot order.</summary>
    public int[] PicksFor(int item, int recipe)
    {
        var picks = new int[Index.SlotCount(recipe)];
        for (var s = 0; s < picks.Length; s++)
        {
            picks[s] = Pick(item, recipe, s);
        }
        return picks;
    }

    /// <summary>The recipe's input stack per slot when it produces the item; an unknown id falls to the cheapest alternatives.</summary>
    public IReadOnlyList<SolverStack> InputsFor(string itemId, int recipe)
    {
        var item = Index.TryGetItem(itemId, out var known) ? known : -1;
        var stacks = new SolverStack[Index.SlotCount(recipe)];
        for (var s = 0; s < stacks.Length; s++)
        {
            var pick = item >= 0 ? Pick(item, recipe, s) : CheapestAlternative(recipe, s);
            var at = Index.AlternativeAt(recipe, s, pick);
            stacks[s] = new SolverStack(Index.ItemIds[Index.AlternativeItem[at]], Index.AlternativeAmount[at]);
        }
        return stacks;
    }

    public bool IsPriced(string itemId) => Index.TryGetItem(itemId, out var item) && IsPriced(item);

    public double? Cost(string itemId) =>
        Index.TryGetItem(itemId, out var item) && TryCost(item, out var cost) ? cost : null;

    /// <summary>The recipe position that priced the item, or -1 for an unpriced or unknown id.</summary>
    public int BestRecipe(string itemId) => Index.TryGetItem(itemId, out var item) ? _best[item] : -1;

    public string? BestRecipeId(string itemId) =>
        BestRecipe(itemId) is var recipe && recipe >= 0 ? Index.RecipeIds[recipe] : null;

    /// <summary>The input stack per slot the item's best recipe was priced with; empty when no recipe priced it.</summary>
    public IReadOnlyList<SolverStack> ChosenInputs(string itemId)
    {
        if (!Index.TryGetItem(itemId, out var item) || _best[item] < 0)
        {
            return [];
        }
        var recipe = _best[item];
        var picks = Picks(item);
        var stacks = new SolverStack[picks.Length];
        for (var s = 0; s < stacks.Length; s++)
        {
            var at = Index.AlternativeAt(recipe, s, picks[s]);
            stacks[s] = new SolverStack(Index.ItemIds[Index.AlternativeItem[at]], Index.AlternativeAmount[at]);
        }
        return stacks;
    }
}
