using Craftiger.Solver.Models.Lp;

namespace Craftiger.Solver.Models.Factory;

/// <summary>The LP under construction: balance rows keyed by item, extra rows, and columns with their meaning; rows and columns take positions in the order they are added.</summary>
public sealed class FactoryModelAssembly(FactoryTargets targets)
{
    private readonly List<LpRow> _rows = [];
    private readonly List<int> _rowItems = [];
    private readonly Dictionary<int, int> _rowOf = new();
    private readonly List<LpColumn> _columns = [];
    private readonly List<FactoryColumn> _meanings = [];
    private readonly List<int> _pinnedColumns = [];

    public int RowCount => _rows.Count;

    public int ColumnCount => _columns.Count;

    public IReadOnlyList<LpColumn> Columns => _columns;

    public IReadOnlyList<FactoryColumn> Meanings => _meanings;

    /// <summary>The balance row of an item, opened on first use with the produce target as its floor.</summary>
    public int RowOf(int item)
    {
        if (!_rowOf.TryGetValue(item, out var row))
        {
            row = _rows.Count;
            _rowOf[item] = row;
            _rows.Add(new LpRow(targets.ProduceRate(item), double.PositiveInfinity));
            _rowItems.Add(item);
        }
        return row;
    }

    public bool TryGetRow(int item, out int row) => _rowOf.TryGetValue(item, out row);

    /// <summary>The items with balance rows, in position order.</summary>
    public IEnumerable<(int Item, int Row)> ItemRows =>
        _rowOf.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value));

    public void SetRow(int row, LpRow bounds) => _rows[row] = bounds;

    /// <summary>A row belonging to no item: the EU balance, a band, a choice-slot link.</summary>
    public int AddRow(LpRow bounds)
    {
        _rows.Add(bounds);
        _rowItems.Add(-1);
        return _rows.Count - 1;
    }

    public int AddColumn(LpColumn column, FactoryColumn meaning, bool pinnedAway = false)
    {
        if (pinnedAway)
        {
            _pinnedColumns.Add(_columns.Count);
        }
        _columns.Add(column);
        _meanings.Add(meaning);
        return _columns.Count - 1;
    }

    public FactoryModel Freeze(
        IReadOnlyList<LpObjective> objectives,
        double timeLimitSeconds,
        IReadOnlyDictionary<string, double> weights,
        IReadOnlySet<int> seedItems,
        int? euRow,
        IReadOnlyList<int> bandRows,
        IReadOnlyList<string> pinItems) =>
        new(
            new LinearProgram(_columns, _rows, objectives, timeLimitSeconds),
            _meanings, _rowItems, weights, seedItems, euRow, bandRows, _pinnedColumns, pinItems);
}
