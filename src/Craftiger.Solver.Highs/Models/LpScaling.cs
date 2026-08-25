using System.Collections.Immutable;
using Craftiger.Solver.Models.Lp;

namespace Craftiger.Solver.Highs.Models;

/// <summary>Exact power-of-two row and column scales pulling every matrix entry toward unit magnitude; column scaling changes the variables' units, so solutions are mapped back before they leave the adapter.</summary>
public sealed record LpScaling(ImmutableArray<double> Rows, ImmutableArray<double> Columns)
{
    /// <summary>Iterated geometric-mean equilibration: real factory matrices span chanced yields to giant sentinel amounts, and that range broke presolve–postsolve equivalence.</summary>
    public static LpScaling Equilibrate(LinearProgram program, int passes)
    {
        var rowLog = new double[program.Rows.Count];
        var columnLog = new double[program.Columns.Count];
        var sums = new double[program.Rows.Count];
        var counts = new int[program.Rows.Count];

        for (var pass = 0; pass < passes; pass++)
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

        return new LpScaling(
            [.. rowLog.Select(log => Math.Pow(2, log))],
            [.. columnLog.Select(log => Math.Pow(2, log))]);
    }

    /// <summary>A layer's cost vector in the scaled column space, normalized to unit geometric mean: raw leaf weights span nine orders and trip the solver's cost tolerances.</summary>
    public double[] ScaledCosts(LpObjective objective)
    {
        var scale = CostScale(objective.Coefficients);
        var costs = new double[Columns.Length];
        foreach (var entry in objective.Coefficients)
        {
            costs[entry.Index] = entry.Value * Columns[entry.Index] / scale;
        }
        return costs;
    }

    /// <summary>A scaled solution mapped back to the program's units.</summary>
    public double[] Unscale(double[] scaled)
    {
        var solution = new double[scaled.Length];
        for (var i = 0; i < solution.Length; i++)
        {
            solution[i] = scaled[i] * Columns[i];
        }
        return solution;
    }

    /// <summary>The power of two nearest the geometric mean of the scaled costs' magnitudes.</summary>
    private double CostScale(IReadOnlyList<LpEntry> coefficients)
    {
        var sum = 0.0;
        var count = 0;
        foreach (var entry in coefficients)
        {
            if (entry.Value != 0)
            {
                sum += Math.Log2(Math.Abs(entry.Value * Columns[entry.Index]));
                count++;
            }
        }
        return count == 0 ? 1 : Math.Pow(2, Math.Round(sum / count));
    }
}
