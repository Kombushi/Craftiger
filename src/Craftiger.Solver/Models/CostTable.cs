namespace Craftiger.Solver.Models;

/// <summary>A solved cost table over the positions of a <see cref="SolverIndex"/>: cheapest
/// known cost per item (NaN means unreachable), the recipe that set it (leaves may carry one
/// yet never expand), the alternative each of that recipe's slots was priced with, and whether
/// the fixpoint settled inside its budget. Readers ask by position on hot paths and by id at
/// the API edge; an id the index does not know is simply unpriced.</summary>
public sealed class CostTable
{
    private readonly double[] _cost;
    private readonly int[] _best;
    private readonly int[] _pickStart;
    private readonly ushort[] _picks;

    public CostTable(
        SolverIndex index, double[] cost, int[] best, int[] pickStart, ushort[] picks, bool converged)
    {
        Index = index;
        _cost = cost;
        _best = best;
        _pickStart = pickStart;
        _picks = picks;
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

    public bool IsPriced(int item) => !double.IsNaN(_cost[item]);

    /// <summary>The cost at a position, NaN when unpriced — for loops that check first.</summary>
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

    public bool IsPriced(string itemId) => Index.TryGetItem(itemId, out var item) && IsPriced(item);

    public double? Cost(string itemId) =>
        Index.TryGetItem(itemId, out var item) && TryCost(item, out var cost) ? cost : null;

    /// <summary>The recipe position that priced the item, or -1 for an unpriced or unknown id.</summary>
    public int BestRecipe(string itemId) => Index.TryGetItem(itemId, out var item) ? _best[item] : -1;

    public string? BestRecipeId(string itemId) =>
        BestRecipe(itemId) is var recipe && recipe >= 0 ? Index.RecipeIds[recipe] : null;

    /// <summary>The input stack per slot the item's best recipe was priced with; empty when no
    /// recipe priced it.</summary>
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

    /// <summary>The raw cost array for the solver's own arithmetic.</summary>
    internal double[] CostArray => _cost;

    /// <summary>The table's storage, for a store that keeps solved tables outside the process:
    /// cost per position, best recipe per position, each item's offset into the picks, and the
    /// picks. The constructor takes the same four arrays back.</summary>
    public ReadOnlySpan<double> Costs => _cost;

    public ReadOnlySpan<int> BestRecipes => _best;

    public ReadOnlySpan<int> PickStarts => _pickStart;

    public ReadOnlySpan<ushort> PickArray => _picks;
}
