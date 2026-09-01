using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Models.Costs;

/// <summary>The working state of a cost solve: prices, pointers and per-item picks, mutable until frozen into a CostTable.</summary>
public sealed class CostTableBuilder
{
    private readonly double[] _cost;
    private readonly int[] _best;
    private readonly ushort[]?[] _chosen;
    private readonly List<int> _won = [];
    private readonly ushort[] _scratch;

    /// <summary>Prices start at NaN — every comparison against it fails, which is exactly "absent loses to any candidate".</summary>
    public CostTableBuilder(SolverIndex index, IReadOnlyDictionary<string, double> leafWeights)
    {
        Index = index;
        _cost = new double[index.ItemCount];
        Array.Fill(_cost, double.NaN);
        foreach (var (id, weight) in leafWeights)
        {
            _cost[index.ItemIndex[id]] = weight;
        }
        _best = new int[index.ItemCount];
        Array.Fill(_best, -1);
        _chosen = new ushort[index.ItemCount][];
        _scratch = new ushort[index.MaxSlotCount];
    }

    public SolverIndex Index { get; }

    /// <summary>Items in the order they first won a recipe; the reroute pass visits them in that order and its outcome on ties depends on it.</summary>
    public IReadOnlyList<int> Won => _won;

    public double Cost(int item) => _cost[item];

    public int BestRecipe(int item) => _best[item];

    public ReadOnlySpan<ushort> Picks(int item) => _chosen[item];

    public double SlotTotal(int recipe) => CostArithmetic.SlotTotal(Index, recipe, _cost);

    public double Candidate(int recipe, int item) => CostArithmetic.Candidate(Index, recipe, item, _cost);

    /// <summary>The alternative each slot resolves to at the current prices, in a buffer reused across calls.</summary>
    public ReadOnlySpan<ushort> ScratchPicks(int recipe)
    {
        var picks = _scratch.AsSpan(0, Index.SlotCount(recipe));
        CostArithmetic.Picks(Index, recipe, _cost, picks);
        return picks;
    }

    /// <summary>The alternative each slot resolves to at the current prices, in a fresh buffer.</summary>
    public ushort[] CurrentPicks(int recipe)
    {
        var picks = new ushort[Index.SlotCount(recipe)];
        CostArithmetic.Picks(Index, recipe, _cost, picks);
        return picks;
    }

    public int PickedItem(int recipe, ReadOnlySpan<ushort> picks, int slot) =>
        CostArithmetic.PickedItem(Index, recipe, picks, slot);

    /// <summary>Records a strict improvement: the price, the winning recipe and the picks it was priced with.</summary>
    public void Win(int item, int recipe, double cost, ReadOnlySpan<ushort> picks)
    {
        if (_best[item] < 0)
        {
            _won.Add(item);
        }
        _cost[item] = cost;
        _best[item] = recipe;
        Record(item, picks);
    }

    /// <summary>Moves the pointer without touching the price.</summary>
    public void Reroute(int item, int recipe, ReadOnlySpan<ushort> picks)
    {
        _best[item] = recipe;
        Record(item, picks);
    }

    /// <summary>Keeps the item's picks in its own buffer, grown only when a wider recipe wins.</summary>
    private void Record(int item, ReadOnlySpan<ushort> picks)
    {
        var buffer = _chosen[item];
        if (buffer is null || buffer.Length < picks.Length)
        {
            _chosen[item] = buffer = new ushort[picks.Length];
        }
        picks.CopyTo(buffer);
    }

    /// <summary>Packs the per-item pick buffers into one compressed row array; cost and pointer arrays are handed over as they are.</summary>
    public CostTable Build(bool converged)
    {
        var pickStart = new int[Index.ItemCount];
        var total = 0;
        foreach (var item in _won)
        {
            pickStart[item] = total;
            total += Index.SlotCount(_best[item]);
        }
        var picks = new ushort[total];
        foreach (var item in _won)
        {
            Array.Copy(_chosen[item]!, 0, picks, pickStart[item], Index.SlotCount(_best[item]));
        }
        return new CostTable(Index, _cost, _best, pickStart, picks, converged);
    }
}
