using Craftiger.Solver.Models.Graph;
using Craftiger.Solver.Models.Lp;

namespace Craftiger.Solver.Models.Factory;

/// <summary>A built factory LP with what its columns and rows mean: RowItems holds the item per balance row (-1 elsewhere), PinnedColumns the run columns pins forced to zero.</summary>
public sealed record FactoryModel(
    LinearProgram Program,
    IReadOnlyList<FactoryColumn> Columns,
    IReadOnlyList<int> RowItems,
    IReadOnlyDictionary<string, double> Weights,
    IReadOnlySet<int> SeedItems,
    int? EuRow,
    IReadOnlyList<int> BandRows,
    IReadOnlyList<int> PinnedColumns,
    IReadOnlyList<string> PinItems)
{
    public bool HasPins => PinnedColumns.Count > 0;

    /// <summary>The item behind a row, or null for a row belonging to no item.</summary>
    public int? ItemOfRow(int row) => RowItems[row] < 0 ? null : RowItems[row];

    /// <summary>Whether the row carries a demand the plan can fall short of: an item balance, the EU balance, or a band.</summary>
    public bool IsDemandRow(int row) => RowItems[row] >= 0 || row == EuRow || BandRows.Contains(row);

    /// <summary>The weight the resource layer charged a purchase — zero for auto-infinite seeds.</summary>
    public double ChargedWeight(SolverIndex index, int item) =>
        SeedItems.Contains(item) ? 0 : Weights.GetValueOrDefault(index.ItemIds[item], 1);

    /// <summary>The same program with every pinned-away column freed.</summary>
    public LinearProgram WithoutPins()
    {
        var columns = new List<LpColumn>(Program.Columns);
        foreach (var column in PinnedColumns)
        {
            columns[column] = columns[column] with { Upper = double.PositiveInfinity };
        }
        return new LinearProgram(columns, Program.Rows, [new LpObjective(Maximize: false, [])], Program.TimeLimitSeconds);
    }
}
