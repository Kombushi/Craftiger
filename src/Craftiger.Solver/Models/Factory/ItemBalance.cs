using Craftiger.Solver.Models.Lp;

namespace Craftiger.Solver.Models.Factory;

/// <summary>A column's net contribution per row, accumulated as a recipe's outputs and inputs are added.</summary>
public sealed class ItemBalance
{
    private readonly Dictionary<int, double> _net = new();

    public void Add(int row, double delta) => _net[row] = _net.GetValueOrDefault(row) + delta;

    /// <summary>The entries in row order.</summary>
    public List<LpEntry> Entries() => Sorted(_net);

    /// <summary>The entries in row order with extra contributions added on top, the balance itself untouched.</summary>
    public List<LpEntry> Entries(IEnumerable<(int Row, double Delta)> extras)
    {
        var net = new Dictionary<int, double>(_net);
        foreach (var (row, delta) in extras)
        {
            net[row] = net.GetValueOrDefault(row) + delta;
        }
        return Sorted(net);
    }

    private static List<LpEntry> Sorted(Dictionary<int, double> net) =>
        [.. net.OrderBy(entry => entry.Key).Select(entry => new LpEntry(entry.Key, entry.Value))];
}
