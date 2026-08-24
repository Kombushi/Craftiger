using Craftiger.Solver.Interfaces;
using Craftiger.Solver.Models;
using global::Highs;

namespace Craftiger.Solver.Highs;

/// <summary>HiGHS-backed lexicographic LP on one private solver instance per call: the native
/// library's thread safety is undocumented, so instances are never shared. Layers run as
/// sequential solves — each optimum becomes a constraint row and the next layer re-presolves
/// from scratch — because HiGHS's native multi-objective mode and hot-started bases both
/// measured minutes on real factory models against seconds for this sequence. The model is
/// equilibrated with exact power-of-two row and column scales before HiGHS sees it: real
/// factory matrices span from chanced yields to the artifact's giant sentinel amounts, and
/// that range broke presolve–postsolve equivalence. Settings are pinned single-threaded with
/// a fixed seed so identical programs return identical solutions on every replica.</summary>
public sealed class HighsLinearProgramSolver : ILinearProgramSolver
{
    private const int EquilibrationPasses = 3;

    /// <summary>Streams HiGHS's own log to stdout — a diagnostics aid, never set in production.</summary>
    public bool Verbose { get; init; }

    public LinearProgramResult Solve(LinearProgram program)
    {
        if (program.Objectives.Count == 0)
        {
            throw new ArgumentException("A linear program needs at least one objective.", nameof(program));
        }

        using var solver = new HighsLpSolver();
        Configure(solver);
        var deadline = program.TimeLimitSeconds > 0
            ? System.Diagnostics.Stopwatch.StartNew()
            : null;

        var infinity = solver.getInfinity();
        var (rowScales, columnScales) = Equilibrate(program);
        AddRows(solver, program.Rows, rowScales, infinity);
        AddColumns(solver, program.Columns, rowScales, columnScales, infinity);

        var columnCount = program.Columns.Count;
        var costs = new double[columnCount];
        var allColumns = new bool[columnCount];
        Array.Fill(allColumns, true);
        double[]? standing = null;
        foreach (var objective in program.Objectives)
        {
            // Costs live in the scaled column space and are normalized to unit geometric
            // mean: raw leaf weights span nine orders and trip the solver's cost tolerances.
            var costScale = CostScale(objective.Coefficients, columnScales);
            Array.Clear(costs);
            foreach (var entry in objective.Coefficients)
            {
                costs[entry.Index] = entry.Value * columnScales[entry.Index] / costScale;
            }
            solver.changeColsCostByMask(allColumns, costs);
            solver.changeObjectiveSense(objective.Maximize ? HighsObjectiveSense.kMaximize : HighsObjectiveSense.kMinimize);

            if (objective.SupportRestricted && standing is not null)
            {
                FixZeroColumns(solver, program.Columns, standing);
            }

            if (deadline is not null)
            {
                var remaining = program.TimeLimitSeconds - deadline.Elapsed.TotalSeconds;
                if (remaining <= 0)
                {
                    return new LinearProgramResult(LpSolveStatus.TimedOut, []);
                }
                solver.setDoubleOptionValue("time_limit", remaining);
            }

            // A cleared solver presolves the layer from scratch; a stale basis would skip
            // presolve entirely and dual simplex crawls on the full column space instead.
            solver.clearSolver();
            var status = RunToStatus(solver);
            if (status != LpSolveStatus.Optimal)
            {
                return new LinearProgramResult(status, []);
            }
            standing = solver.getSolution().colvalue;

            // The optimum becomes a constraint for the layers below, slack by the tolerance.
            var optimum = solver.getObjectiveValue();
            var slack = Math.Max(objective.AbsTolerance, objective.RelTolerance * Math.Abs(optimum));
            var indices = new int[objective.Coefficients.Count];
            var values = new double[objective.Coefficients.Count];
            for (var i = 0; i < objective.Coefficients.Count; i++)
            {
                indices[i] = objective.Coefficients[i].Index;
                values[i] = costs[objective.Coefficients[i].Index];
            }
            if (objective.Maximize)
            {
                solver.addRow(optimum - slack, infinity, indices, values);
            }
            else
            {
                solver.addRow(-infinity, optimum + slack, indices, values);
            }
        }

        var solution = new double[columnCount];
        for (var i = 0; i < columnCount; i++)
        {
            solution[i] = standing![i] * columnScales[i];
        }
        return new LinearProgramResult(LpSolveStatus.Optimal, solution);
    }

    /// <summary>Iterated geometric-mean equilibration in exact powers of two: row and column
    /// scales that pull every matrix entry toward unit magnitude. Column scaling changes the
    /// variables' units, so solutions are mapped back before they leave the adapter.</summary>
    private static (double[] Rows, double[] Columns) Equilibrate(LinearProgram program)
    {
        var rowLog = new double[program.Rows.Count];
        var columnLog = new double[program.Columns.Count];
        var sums = new double[program.Rows.Count];
        var counts = new int[program.Rows.Count];

        for (var pass = 0; pass < EquilibrationPasses; pass++)
        {
            Array.Clear(sums);
            Array.Clear(counts);
            for (var c = 0; c < program.Columns.Count; c++)
            {
                foreach (var entry in program.Columns[c].Entries)
                {
                    if (entry.Value != 0)
                    {
                        sums[entry.Index] += Math.Log2(Math.Abs(entry.Value)) + columnLog[c] - rowLog[entry.Index];
                        counts[entry.Index]++;
                    }
                }
            }
            for (var r = 0; r < rowLog.Length; r++)
            {
                if (counts[r] > 0)
                {
                    rowLog[r] += Math.Round(sums[r] / counts[r]);
                }
            }

            for (var c = 0; c < program.Columns.Count; c++)
            {
                var sum = 0.0;
                var count = 0;
                foreach (var entry in program.Columns[c].Entries)
                {
                    if (entry.Value != 0)
                    {
                        sum += Math.Log2(Math.Abs(entry.Value)) + columnLog[c] - rowLog[entry.Index];
                        count++;
                    }
                }
                if (count > 0)
                {
                    columnLog[c] -= Math.Round(sum / count);
                }
            }
        }

        var rowScales = new double[rowLog.Length];
        for (var r = 0; r < rowLog.Length; r++)
        {
            rowScales[r] = Math.Pow(2, rowLog[r]);
        }
        var columnScales = new double[columnLog.Length];
        for (var c = 0; c < columnLog.Length; c++)
        {
            columnScales[c] = Math.Pow(2, columnLog[c]);
        }
        return (rowScales, columnScales);
    }

    /// <summary>Fixes every column of the standing solution that sits at zero to zero, so a
    /// support-restricted layer only rearranges flow the earlier layers actually chose.</summary>
    private static void FixZeroColumns(HighsLpSolver solver, IReadOnlyList<LpColumn> columns, double[] standing)
    {
        var zero = new bool[standing.Length];
        var lower = new double[standing.Length];
        var upper = new double[standing.Length];

        for (var i = 0; i < standing.Length; i++)
        {
            if (Math.Abs(standing[i]) <= 1e-9 && columns[i].Lower <= 0)
            {
                zero[i] = true;
            }
        }

        solver.changeColsBoundsByMask(zero, lower, upper);
    }

    private static LpSolveStatus RunToStatus(HighsLpSolver solver)
    {
        if (solver.run() == HighsStatus.kError)
        {
            return LpSolveStatus.Error;
        }

        var modelStatus = solver.GetModelStatus();
        if (modelStatus == HighsModelStatus.kUnboundedOrInfeasible)
        {
            // Presolve could not tell the two apart; a re-run without it settles which one it is.
            solver.setStringOptionValue("presolve", "off");
            solver.run();
            modelStatus = solver.GetModelStatus();
            solver.setStringOptionValue("presolve", "choose");
        }

        return MapStatus(modelStatus);
    }

    private void Configure(HighsLpSolver solver)
    {
        solver.setStringOptionValue("output_flag", Verbose ? "true" : "false");
        solver.setIntOptionValue("threads", 1);
        solver.setIntOptionValue("random_seed", 0);
        solver.setStringOptionValue("solver", "simplex");
    }

    private static void AddRows(HighsLpSolver solver, IReadOnlyList<LpRow> rows, double[] scales, double infinity)
    {
        var lower = new double[rows.Count];
        var upper = new double[rows.Count];

        for (var i = 0; i < rows.Count; i++)
        {
            lower[i] = ClampBound(rows[i].Lower / scales[i], infinity);
            upper[i] = ClampBound(rows[i].Upper / scales[i], infinity);
        }

        solver.addRows(lower, upper, new int[rows.Count], [], []);
    }

    private static void AddColumns(
        HighsLpSolver solver,
        IReadOnlyList<LpColumn> columns,
        double[] rowScales,
        double[] columnScales,
        double infinity)
    {
        var costs = new double[columns.Count];
        var lower = new double[columns.Count];
        var upper = new double[columns.Count];
        var starts = new int[columns.Count];
        var entryCount = columns.Sum(c => c.Entries.Count);
        var indices = new int[entryCount];
        var values = new double[entryCount];
        var next = 0;

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            lower[i] = ClampBound(column.Lower / columnScales[i], infinity);
            upper[i] = ClampBound(column.Upper / columnScales[i], infinity);
            starts[i] = next;

            foreach (var entry in column.Entries)
            {
                indices[next] = entry.Index;
                values[next] = entry.Value * columnScales[i] / rowScales[entry.Index];
                next++;
            }
        }

        solver.addCols(costs, lower, upper, starts, indices, values);
    }

    /// <summary>The power of two nearest the geometric mean of the scaled costs' magnitudes.</summary>
    private static double CostScale(IReadOnlyList<LpEntry> coefficients, double[] columnScales)
    {
        var sum = 0.0;
        var count = 0;
        foreach (var entry in coefficients)
        {
            if (entry.Value != 0)
            {
                sum += Math.Log2(Math.Abs(entry.Value * columnScales[entry.Index]));
                count++;
            }
        }
        return count == 0 ? 1 : Math.Pow(2, Math.Round(sum / count));
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

    private static LpSolveStatus MapStatus(HighsModelStatus status)
    {
        return status switch
        {
            HighsModelStatus.kOptimal => LpSolveStatus.Optimal,
            HighsModelStatus.kInfeasible => LpSolveStatus.Infeasible,
            HighsModelStatus.kUnbounded => LpSolveStatus.Unbounded,
            HighsModelStatus.kUnboundedOrInfeasible => LpSolveStatus.Unbounded,
            HighsModelStatus.kTimeLimit => LpSolveStatus.TimedOut,
            _ => LpSolveStatus.Error,
        };
    }
}
