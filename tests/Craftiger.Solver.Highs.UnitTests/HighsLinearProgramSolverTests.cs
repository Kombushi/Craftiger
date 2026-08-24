using Craftiger.Solver.Highs;
using Craftiger.Solver.Models;

namespace Craftiger.Solver.Highs.UnitTests;

public class HighsLinearProgramSolverTests
{
    private const double Inf = double.PositiveInfinity;

    private static LpObjective Minimize(params LpEntry[] coefficients)
    {
        return new LpObjective(Maximize: false, coefficients);
    }

    private static LpObjective Maximize(params LpEntry[] coefficients)
    {
        return new LpObjective(Maximize: true, coefficients);
    }

    [Fact]
    public void SolvesSingleObjectiveLp()
    {
        // min x + 2y subject to x + y >= 10, 0 <= x <= 6: the optimum is x = 6, y = 4.
        var program = new LinearProgram(
            Columns:
            [
                new LpColumn(0, 6, [new LpEntry(0, 1)]),
                new LpColumn(0, Inf, [new LpEntry(0, 1)]),
            ],
            Rows: [new LpRow(10, Inf)],
            Objectives: [Minimize(new LpEntry(0, 1), new LpEntry(1, 2))]);

        var result = new HighsLinearProgramSolver().Solve(program);

        Assert.Equal(LpSolveStatus.Optimal, result.Status);
        Assert.Equal(6, result.ColumnValues[0], 6);
        Assert.Equal(4, result.ColumnValues[1], 6);
    }

    [Fact]
    public void HonorsLexicographicPriority()
    {
        // x + y >= 10 with two zero-cost variables: the first layer decides which one carries it.
        static LinearProgram Build(bool xFirst)
        {
            var minX = Minimize(new LpEntry(0, 1));
            var minY = Minimize(new LpEntry(1, 1));

            return new LinearProgram(
                Columns:
                [
                    new LpColumn(0, Inf, [new LpEntry(0, 1)]),
                    new LpColumn(0, Inf, [new LpEntry(0, 1)]),
                ],
                Rows: [new LpRow(10, Inf)],
                Objectives: xFirst ? [minX, minY] : [minY, minX]);
        }

        var solver = new HighsLinearProgramSolver();
        var xFirst = solver.Solve(Build(xFirst: true));
        var yFirst = solver.Solve(Build(xFirst: false));

        // Values sit within the layer tolerance of the lexicographic ideal, never exactly on it.
        Assert.Equal(LpSolveStatus.Optimal, xFirst.Status);
        Assert.Equal(0, xFirst.ColumnValues[0], 4);
        Assert.Equal(10, xFirst.ColumnValues[1], 4);
        Assert.Equal(LpSolveStatus.Optimal, yFirst.Status);
        Assert.Equal(10, yFirst.ColumnValues[0], 4);
        Assert.Equal(0, yFirst.ColumnValues[1], 4);
    }

    [Fact]
    public void MaximizeLayerRunsBeforeMinimizeLayer()
    {
        // Maximize s (capped at 8) even though the next layer pays x = 2s for it.
        var program = new LinearProgram(
            Columns:
            [
                new LpColumn(0, 8, [new LpEntry(0, -2)]),
                new LpColumn(0, Inf, [new LpEntry(0, 1)]),
            ],
            Rows: [new LpRow(0, Inf)],
            Objectives:
            [
                Maximize(new LpEntry(0, 1)),
                Minimize(new LpEntry(1, 1)),
            ]);

        var result = new HighsLinearProgramSolver().Solve(program);

        Assert.Equal(LpSolveStatus.Optimal, result.Status);
        Assert.Equal(8, result.ColumnValues[0], 4);
        Assert.Equal(16, result.ColumnValues[1], 4);
    }

    [Fact]
    public void ReportsInfeasible()
    {
        var program = new LinearProgram(
            Columns: [new LpColumn(0, 3, [new LpEntry(0, 1)])],
            Rows: [new LpRow(5, Inf)],
            Objectives: [Minimize(new LpEntry(0, 1))]);

        var result = new HighsLinearProgramSolver().Solve(program);

        Assert.Equal(LpSolveStatus.Infeasible, result.Status);
        Assert.Empty(result.ColumnValues);
    }

    [Fact]
    public void ReportsUnbounded()
    {
        var program = new LinearProgram(
            Columns: [new LpColumn(0, Inf, [new LpEntry(0, 1)])],
            Rows: [new LpRow(0, Inf)],
            Objectives: [Maximize(new LpEntry(0, 1))]);

        var result = new HighsLinearProgramSolver().Solve(program);

        Assert.Equal(LpSolveStatus.Unbounded, result.Status);
        Assert.Empty(result.ColumnValues);
    }

    [Fact]
    public void IsDeterministicAcrossRepeatedSolves()
    {
        // A degenerate facet (many optimal vertices) must come back identical on every solve.
        var columns = new List<LpColumn>();

        for (var i = 0; i < 20; i++)
        {
            columns.Add(new LpColumn(0, Inf, [new LpEntry(0, 1), new LpEntry(1, i % 3 == 0 ? 1 : 0)]));
        }

        var program = new LinearProgram(
            Columns: columns,
            Rows: [new LpRow(100, Inf), new LpRow(10, Inf)],
            Objectives:
            [
                Minimize([.. Enumerable.Range(0, 20).Select(i => new LpEntry(i, 1.0))]),
                Minimize([.. Enumerable.Range(0, 20).Select(i => new LpEntry(i, i % 2 == 0 ? 1.0 : 0.5))]),
            ]);

        var solver = new HighsLinearProgramSolver();
        var first = solver.Solve(program);
        var second = solver.Solve(program);

        Assert.Equal(LpSolveStatus.Optimal, first.Status);
        Assert.Equal(first.ColumnValues, second.ColumnValues);
    }

    [Fact]
    public void RejectsProgramWithoutObjectives()
    {
        var program = new LinearProgram(
            Columns: [new LpColumn(0, 1, [])],
            Rows: [],
            Objectives: []);

        Assert.Throws<ArgumentException>(() => new HighsLinearProgramSolver().Solve(program));
    }
}
