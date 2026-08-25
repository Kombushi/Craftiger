using Craftiger.Solver.Interfaces.Factory;
using Craftiger.Solver.Interfaces.Lp;
using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Lp;
using Craftiger.Solver.Models.Options;
using Microsoft.Extensions.Options;

namespace Craftiger.Solver.Services.Factory;

public sealed class FactoryDiagnosisService(ILinearProgramSolver solver, IOptions<FactorySolverOptions> options) : IFactoryDiagnosisService
{
    private readonly FactorySolverOptions _options = options.Value;

    /// <summary>First asks whether lifting the pins alone makes the model feasible, then re-solves with slack on every demand row — the rows whose slack the minimum keeps are what the garage cannot deliver.</summary>
    public void Diagnose(FactoryContext context, FactoryModel model, ICollection<FactoryWarning> warnings)
    {
        if (model.HasPins && solver.Solve(model.WithoutPins()).Status == LpSolveStatus.Optimal)
        {
            foreach (var itemId in model.PinItems)
            {
                warnings.Add(FactoryWarning.PinConflict(itemId));
            }
            return;
        }
        Elastic(context, model, warnings);
    }

    /// <summary>Every demand row gets a shortfall slack — and an excess slack where it is bounded above — and minimizing total slack keeps nonzero only the rows the model cannot satisfy.</summary>
    private void Elastic(FactoryContext context, FactoryModel model, ICollection<FactoryWarning> warnings)
    {
        var program = model.Program;
        var columns = new List<LpColumn>(program.Columns);
        var slackRows = new List<int>();
        var coefficients = new List<LpEntry>();

        void AddSlack(int row, double sign)
        {
            coefficients.Add(new LpEntry(columns.Count, 1));
            slackRows.Add(row);
            columns.Add(new LpColumn(0, double.PositiveInfinity, [new LpEntry(row, sign)]));
        }

        for (var row = 0; row < program.Rows.Count; row++)
        {
            if (!model.IsDemandRow(row))
            {
                continue;
            }
            AddSlack(row, 1);
            if (!double.IsPositiveInfinity(program.Rows[row].Upper))
            {
                AddSlack(row, -1);
            }
        }

        var elastic = new LinearProgram(
            columns, program.Rows, [new LpObjective(Maximize: false, coefficients)], program.TimeLimitSeconds);
        var result = solver.Solve(elastic);
        if (result.Status != LpSolveStatus.Optimal)
        {
            warnings.Add(FactoryWarning.Infeasible());
            return;
        }

        var named = new HashSet<FactoryWarning>();
        for (var s = 0; s < slackRows.Count; s++)
        {
            if (result.ColumnValues[program.Columns.Count + s] <= _options.RateEpsilon)
            {
                continue;
            }
            var warning = model.ItemOfRow(slackRows[s]) is { } item
                ? FactoryWarning.InfeasibleItem(context.Index.ItemIds[item])
                : FactoryWarning.InfeasibleEnergy();
            if (named.Add(warning))
            {
                warnings.Add(warning);
            }
        }
        if (named.Count == 0)
        {
            warnings.Add(FactoryWarning.Infeasible());
        }
    }
}
