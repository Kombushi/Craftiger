using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Models.Costs;

/// <summary>The working state of a cost solve: prices, pointers and per-item picks, mutable until frozen into a CostTable.</summary>
public sealed class CostTableBuilder
{
    private readonly SolverIndex _index;
    private readonly double[] _cost;
    private readonly int[] _best;
    private readonly ushort[][] _chosen;
    private readonly List<int> _won = [];
    private readonly ushort[] _scratch;

    /// <summary>Prices start at NaN — every comparison against it fails, which is exactly "absent loses to any candidate".</summary>
    public CostTableBuilder(SolverIndex index, IReadOnlyDictionary<string, double> leafWeights)
    {
        _index = index;
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

    public SolverIndex Index => _index;

    public ReadOnlySpan<double> Costs => _cost;

    /// <summary>Items in the order they first won a recipe; the reroute pass visits them in that order and its outcome on ties depends on it.</summary>
    public IReadOnlyList<int> Won => _won;

    public double Cost(int item) => _cost[item];

    public int BestRecipe(int item) => _best[item];

    public ReadOnlySpan<ushort> Picks(int item) => _chosen[item];

    public double SlotTotal(int recipe) => CostArithmetic.SlotTotal(_index, recipe, _cost);

    public double Candidate(int recipe, int item) => CostArithmetic.Candidate(_index, recipe, item, _cost);

    /// <summary>The alternative each slot resolves to at the current prices, in a buffer reused across calls.</summary>
    public ReadOnlySpan<ushort> ScratchPicks(int recipe)
    {
        var picks = _scratch.AsSpan(0, _index.SlotCount(recipe));
        CostArithmetic.Picks(_index, recipe, _cost, picks);
        return picks;
    }

    /// <summary>The alternative each slot resolves to at the current prices, in a fresh buffer.</summary>
    public ushort[] CurrentPicks(int recipe)
    {
        var picks = new ushort[_index.SlotCount(recipe)];
        CostArithmetic.Picks(_index, recipe, _cost, picks);
        return picks;
    }

    public int PickedItem(int recipe, ReadOnlySpan<ushort> picks, int slot) =>
        CostArithmetic.PickedItem(_index, recipe, picks, slot);

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
        if (_chosen[item] is null || _chosen[item].Length < picks.Length)
        {
            _chosen[item] = new ushort[picks.Length];
        }
        picks.CopyTo(_chosen[item]);
    }

    /// <summary>Packs the per-item pick buffers into one compressed row array; cost and pointer arrays are handed over as they are.</summary>
    public CostTable Build(bool converged)
    {
        var pickStart = new int[_index.ItemCount];
        var total = 0;
        foreach (var item in _won)
        {
            pickStart[item] = total;
            total += _index.SlotCount(_best[item]);
        }
        var picks = new ushort[total];
        foreach (var item in _won)
        {
            Array.Copy(_chosen[item], 0, picks, pickStart[item], _index.SlotCount(_best[item]));
        }
        return new CostTable(_index, _cost, _best, pickStart, picks, converged);
    }
}
