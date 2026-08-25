using Craftiger.Solver.Highs.Interfaces;
using Craftiger.Solver.Highs.Models;
using Craftiger.Solver.Models.Lp;
using Highs;
using Microsoft.Extensions.Options;

namespace Craftiger.Solver.Highs.Services;

public sealed class HighsModelLoader(IOptions<HighsOptions> options) : IHighsModelLoader
{
    private readonly HighsOptions _options = options.Value;

    /// <summary>Single-threaded simplex with a fixed seed, so identical programs return identical solutions on every replica.</summary>
    public void Configure(HighsLpSolver solver)
    {
        solver.setStringOptionValue("output_flag", _options.Verbose ? "true" : "false");
        solver.setIntOptionValue("threads", 1);
        solver.setIntOptionValue("random_seed", 0);
        solver.setStringOptionValue("solver", "simplex");
    }

    public void Load(HighsLpSolver solver, LinearProgram program, LpScaling scaling)
    {
        var infinity = solver.getInfinity();
        AddRows(solver, program.Rows, scaling, infinity);
        AddColumns(solver, program.Columns, scaling, infinity);
    }

    private static void AddRows(HighsLpSolver solver, IReadOnlyList<LpRow> rows, LpScaling scaling, double infinity)
    {
        var lower = new double[rows.Count];
        var upper = new double[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            lower[i] = ClampBound(rows[i].Lower / scaling.Rows[i], infinity);
            upper[i] = ClampBound(rows[i].Upper / scaling.Rows[i], infinity);
        }
        solver.addRows(lower, upper, new int[rows.Count], [], []);
    }

    private static void AddColumns(HighsLpSolver solver, IReadOnlyList<LpColumn> columns, LpScaling scaling, double infinity)
    {
        var costs = new double[columns.Count];
        var lower = new double[columns.Count];
        var upper = new double[columns.Count];
        var starts = new int[columns.Count];
        var entryCount = columns.Sum(column => column.Entries.Count);
        var indices = new int[entryCount];
        var values = new double[entryCount];
        var next = 0;

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            lower[i] = ClampBound(column.Lower / scaling.Columns[i], infinity);
            upper[i] = ClampBound(column.Upper / scaling.Columns[i], infinity);
            starts[i] = next;
            foreach (var entry in column.Entries)
            {
                indices[next] = entry.Index;
                values[next] = entry.Value * scaling.Columns[i] / scaling.Rows[entry.Index];
                next++;
            }
        }
        solver.addCols(costs, lower, upper, starts, indices, values);
    }

    private static double ClampBound(double bound, double infinity)
    {
        if (double.IsPositiveInfinity(bound))
        {
            return infinity;
        }
        if (double.IsNegativeInfinity(bound))
        {
            return -infinity;
        }
        return bound;
    }
}
