using Craftiger.Solver.Interfaces;
using Craftiger.Solver.Models;
using global::Highs;

namespace Craftiger.Solver.Highs;

/// <summary>HiGHS-backed lexicographic LP solve on one private solver instance per call:
/// the native library's thread safety is undocumented, so instances are never shared.
/// Settings are pinned single-threaded with a fixed seed for cross-replica determinism.</summary>
public sealed class HighsLinearProgramSolver : ILinearProgramSolver
{
    public LinearProgramResult Solve(LinearProgram program)
    {
        if (program.Objectives.Count == 0)
        {
            throw new ArgumentException("A linear program needs at least one objective.", nameof(program));
        }

        using var solver = new HighsLpSolver();
        Configure(solver, program.TimeLimitSeconds);

        var infinity = solver.getInfinity();
        AddRows(solver, program.Rows, infinity);
        AddColumns(solver, program.Columns, infinity);
        AddObjectives(solver, program.Objectives, program.Columns.Count);

        if (solver.run() == HighsStatus.kError)
        {
            return new LinearProgramResult(LpSolveStatus.Error, []);
        }

        var modelStatus = solver.GetModelStatus();
        if (modelStatus == HighsModelStatus.kUnboundedOrInfeasible)
        {
            // Presolve could not tell the two apart; a re-run without it settles which one it is.
            solver.setStringOptionValue("presolve", "off");
            solver.run();
            modelStatus = solver.GetModelStatus();
        }

        var status = MapStatus(modelStatus);
        if (status != LpSolveStatus.Optimal)
        {
            return new LinearProgramResult(status, []);
        }

        return new LinearProgramResult(LpSolveStatus.Optimal, solver.getSolution().colvalue);
    }

    private static void Configure(HighsLpSolver solver, double timeLimitSeconds)
    {
        solver.setStringOptionValue("output_flag", "false");
        solver.setIntOptionValue("threads", 1);
        solver.setIntOptionValue("random_seed", 0);
        solver.setStringOptionValue("solver", "simplex");
        solver.setBoolOptionValue("blend_multi_objectives", 0);

        if (timeLimitSeconds > 0)
        {
            solver.setDoubleOptionValue("time_limit", timeLimitSeconds);
        }
    }

    private static void AddRows(HighsLpSolver solver, IReadOnlyList<LpRow> rows, double infinity)
    {
        var lower = new double[rows.Count];
        var upper = new double[rows.Count];

        for (var i = 0; i < rows.Count; i++)
        {
            lower[i] = ClampBound(rows[i].Lower, infinity);
            upper[i] = ClampBound(rows[i].Upper, infinity);
        }

        solver.addRows(lower, upper, new int[rows.Count], [], []);
    }

    private static void AddColumns(HighsLpSolver solver, IReadOnlyList<LpColumn> columns, double infinity)
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
            lower[i] = ClampBound(column.Lower, infinity);
            upper[i] = ClampBound(column.Upper, infinity);
            starts[i] = next;

            foreach (var entry in column.Entries)
            {
                indices[next] = entry.Index;
                values[next] = entry.Value;
                next++;
            }
        }

        solver.addCols(costs, lower, upper, starts, indices, values);
    }

    private static void AddObjectives(HighsLpSolver solver, IReadOnlyList<LpObjective> objectives, int columnCount)
    {
        for (var i = 0; i < objectives.Count; i++)
        {
            var objective = objectives[i];
            var coefficients = new double[columnCount];

            foreach (var entry in objective.Coefficients)
            {
                coefficients[entry.Index] = entry.Value;
            }

            var weight = objective.Maximize ? -1.0 : 1.0;
            var priority = objectives.Count - 1 - i;
            solver.addLinearObjective(weight, 0.0, coefficients, objective.AbsTolerance, objective.RelTolerance, priority);
        }
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
