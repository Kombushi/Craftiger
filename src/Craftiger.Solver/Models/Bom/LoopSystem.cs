using Craftiger.Solver.Models.Costs;

namespace Craftiger.Solver.Models.Bom;

/// <summary>A loop's recipes and the gain matrix between its members: how many units of member i one unit of member j consumes through j's recipe.</summary>
public sealed record LoopSystem(
    IReadOnlyDictionary<int, int> Row,
    IReadOnlyList<int> Recipes,
    IReadOnlyList<double> Yields,
    IReadOnlyList<int[]> Picks,
    double[,] Gain)
{
    public int Count => Recipes.Count;

    /// <summary>The system of the loop's members under the chosen recipes and picks.</summary>
    public static LoopSystem Analyze(CostTable costs, BomPins pins, IReadOnlyList<int> members)
    {
        var index = costs.Index;
        var row = new Dictionary<int, int>();
        for (var i = 0; i < members.Count; i++)
        {
            row[members[i]] = i;
        }
        var count = members.Count;
        var recipes = new int[count];
        var yields = new double[count];
        var picks = new int[count][];
        var gain = new double[count, count];
        for (var j = 0; j < count; j++)
        {
            recipes[j] = pins.Chosen(costs, members[j]);
            yields[j] = index.Yield(recipes[j], members[j]);
            picks[j] = costs.PicksFor(members[j], recipes[j]);
            for (var s = 0; s < picks[j].Length; s++)
            {
                var at = index.AlternativeAt(recipes[j], s, picks[j][s]);
                if (row.TryGetValue(index.AlternativeItem[at], out var i))
                {
                    gain[i, j] += index.AlternativeAmount[at] / yields[j];
                }
            }
        }
        return new LoopSystem(row, recipes, yields, picks, gain);
    }

    /// <summary>Whether the loop's gain stays below one, so its demands have a finite solution.</summary>
    public bool HasFinitePlan(double pivotEpsilon) => Eliminate(new double[Count], pivotEpsilon) is not null;

    /// <summary>The demands net of what the seed supplies: members the supply covers entirely drop out, and the rest is solved again until no member is asked for less than nothing.</summary>
    public double[] SolveSupplied(double[] demand, double[] supply, double pivotEpsilon)
    {
        var count = demand.Length;
        var active = Enumerable.Repeat(true, count).ToArray();
        while (true)
        {
            var reduced = new double[count, count];
            var rhs = new double[count];
            for (var i = 0; i < count; i++)
            {
                rhs[i] = active[i] ? demand[i] - supply[i] : 0;
                for (var j = 0; j < count; j++)
                {
                    reduced[i, j] = active[i] && active[j] ? Gain[i, j] : 0;
                }
            }
            var solution = Eliminate(reduced, rhs, pivotEpsilon)!;
            var negative = Array.FindIndex(solution, value => value < 0);
            if (negative < 0)
            {
                return solution;
            }
            active[negative] = false;
        }
    }

    private double[]? Eliminate(double[] demand, double pivotEpsilon) => Eliminate(Gain, demand, pivotEpsilon);

    /// <summary>Solves (I − gain) · x = demand without pivoting: the off-diagonals are non-positive, so a pivot at or below zero means the loop feeds itself for free, and null is returned.</summary>
    private static double[]? Eliminate(double[,] gain, double[] demand, double pivotEpsilon)
    {
        var count = demand.Length;
        var matrix = new double[count, count];
        var rhs = (double[])demand.Clone();
        for (var i = 0; i < count; i++)
        {
            for (var j = 0; j < count; j++)
            {
                matrix[i, j] = (i == j ? 1 : 0) - gain[i, j];
            }
        }
        for (var k = 0; k < count; k++)
        {
            var pivot = matrix[k, k];
            if (pivot <= pivotEpsilon)
            {
                return null;
            }
            for (var r = k + 1; r < count; r++)
            {
                var factor = matrix[r, k] / pivot;
                if (factor == 0)
                {
                    continue;
                }
                for (var c = k; c < count; c++)
                {
                    matrix[r, c] -= factor * matrix[k, c];
                }
                rhs[r] -= factor * rhs[k];
            }
        }
        var solution = new double[count];
        for (var i = count - 1; i >= 0; i--)
        {
            var sum = rhs[i];
            for (var c = i + 1; c < count; c++)
            {
                sum -= matrix[i, c] * solution[c];
            }
            solution[i] = sum / matrix[i, i];
        }
        return solution;
    }
}
