using System.Diagnostics;
using Craftiger.Solver.Highs.Interfaces;
using Craftiger.Solver.Highs.Models;
using Craftiger.Solver.Models.Lp;
using Highs;
using Microsoft.Extensions.Options;

namespace Craftiger.Solver.Highs.Services;

public sealed class LexicographicLayerRunner(IOptions<HighsOptions> options) : ILexicographicLayerRunner
{
    private readonly HighsOptions _options = options.Value;

    /// <summary>Each layer re-presolves from scratch: HiGHS's native multi-objective mode and hot-started bases both measured minutes against seconds for this sequence.</summary>
    public LayerOutcome Run(HighsLpSolver solver, LinearProgram program, LpScaling scaling, Stopwatch? deadline)
    {
        var infinity = solver.getInfinity();
        var allColumns = new bool[program.Columns.Count];
        Array.Fill(allColumns, true);
        var locks = new List<LockRow>();
        double[]? standing = null;
        for (var layer = 0; layer < program.Objectives.Count; layer++)
        {
            var objective = program.Objectives[layer];
            var costs = scaling.ScaledCosts(objective);
            solver.changeColsCostByMask(allColumns, costs);
            solver.changeObjectiveSense(objective.Maximize ? HighsObjectiveSense.kMaximize : HighsObjectiveSense.kMinimize);

            if (objective.SupportRestricted && standing is not null)
            {
                CapToStanding(solver, program.Columns, standing);
                RelaxLocksToStanding(solver, program.Rows.Count, locks, standing);
            }

            if (deadline is not null)
            {
                var remaining = program.TimeLimitSeconds - deadline.Elapsed.TotalSeconds;
                if (remaining <= 0)
                {
                    return new LayerOutcome(LpSolveStatus.TimedOut, null);
                }
                solver.setDoubleOptionValue("time_limit", remaining);
            }

            solver.clearSolver();
            var status = RunToStatus(solver);
            if (status == LpSolveStatus.Infeasible && objective.SupportRestricted)
            {
                // Presolve treats the restricted box's dust-sized bounds as zero and can prove the model infeasible; without it the box has fixed nearly every column, so the rerun is cheap.
                solver.setStringOptionValue("presolve", "off");
                solver.clearSolver();
                status = RunToStatus(solver);
                solver.setStringOptionValue("presolve", "choose");
            }
            if (status != LpSolveStatus.Optimal)
            {
                if (objective.SupportRestricted && standing is not null)
                {
                    // Canonicalization is a tie-break, not a constraint: a simplex vertex already carries no free-spinning churn.
                    break;
                }
                return new LayerOutcome(status, null);
            }
            standing = solver.getSolution().colvalue;

            if (layer < program.Objectives.Count - 1)
            {
                locks.Add(AddLockRow(
                    solver, objective, costs, solver.getObjectiveValue(), infinity, program.Rows.Count + locks.Count));
            }
        }
        return new LayerOutcome(LpSolveStatus.Optimal, standing);
    }

    /// <summary>Bounds a layer's expression at its optimum plus the tolerance slack — the constraint every later layer honors.</summary>
    private static LockRow AddLockRow(
        HighsLpSolver solver, LpObjective objective, double[] costs, double optimum, double infinity, int row)
    {
        var slack = objective.Slack(optimum);
        var indices = new int[objective.Coefficients.Count];
        var values = new double[objective.Coefficients.Count];
        for (var i = 0; i < objective.Coefficients.Count; i++)
        {
            indices[i] = objective.Coefficients[i].Index;
            values[i] = costs[objective.Coefficients[i].Index];
        }
        var lower = objective.Maximize ? optimum - slack : -infinity;
        var upper = objective.Maximize ? infinity : optimum + slack;
        solver.addRow(lower, upper, indices, values);
        return new LockRow(row, indices, values, lower, upper);
    }

    /// <summary>Re-bounds every lock row to contain the standing point: a postsolved solution honors its lock only to solver tolerance, and the restricted box cannot retreat from that dust.</summary>
    private void RelaxLocksToStanding(HighsLpSolver solver, int rowCount, List<LockRow> locks, double[] standing)
    {
        if (locks.Count == 0)
        {
            return;
        }
        var total = rowCount + locks.Count;
        var mask = new bool[total];
        var lower = new double[total];
        var upper = new double[total];
        foreach (var lockRow in locks)
        {
            var activity = lockRow.Activity(standing);
            var pad = _options.LockPad * Math.Max(1, Math.Abs(activity));
            mask[lockRow.Row] = true;
            lower[lockRow.Row] = Math.Min(lockRow.Lower, activity - pad);
            upper[lockRow.Row] = Math.Max(lockRow.Upper, activity + pad);
        }
        solver.changeRowsBoundsByMask(mask, lower, upper);
    }

    /// <summary>Boxes every column between zero and its standing value, dust negatives included and every nonzero side floored, so churn can only shrink while exact zeros stay fixed for presolve.</summary>
    private void CapToStanding(HighsLpSolver solver, IReadOnlyList<LpColumn> columns, double[] standing)
    {
        var mask = new bool[standing.Length];
        var lower = new double[standing.Length];
        var upper = new double[standing.Length];
        for (var i = 0; i < standing.Length; i++)
        {
            if (columns[i].Lower <= 0)
            {
                mask[i] = true;
                var top = Math.Max(0, standing[i]);
                var bottom = Math.Min(0, standing[i]);
                upper[i] = top > 0 ? Math.Max(top, _options.DustFloor) : 0;
                lower[i] = bottom < 0 ? Math.Min(bottom, -_options.DustFloor) : 0;
            }
        }
        solver.changeColsBoundsByMask(mask, lower, upper);
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
        return modelStatus switch
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
