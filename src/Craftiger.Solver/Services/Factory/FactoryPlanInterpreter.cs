using Craftiger.Solver.Interfaces.Factory;
using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Options;
using Microsoft.Extensions.Options;

namespace Craftiger.Solver.Services.Factory;

public sealed class FactoryPlanInterpreter(IOptions<FactorySolverOptions> options) : IFactoryPlanInterpreter
{
    private readonly FactorySolverOptions _options = options.Value;

    public FactoryPlan Interpret(
        FactoryContext context,
        FactoryModel model,
        FactoryTargets targets,
        IReadOnlyList<double> values,
        IReadOnlyList<FactoryWarning> warnings,
        AutoInfiniteItems infinite)
    {
        var index = context.Index;
        var produced = new Dictionary<int, double>();
        var consumed = new Dictionary<int, double>();
        var bought = new Dictionary<int, double>();
        var supplied = new Dictionary<int, double>();
        var lines = new List<FactoryLine>();
        var cost = 0.0;
        var drawEuT = 0.0;
        var exportEuT = 0.0;
        var busyMachines = 0.0;
        var cleanroomHosts = false;

        void Accumulate(Dictionary<int, double> rates, int item, double amount) =>
            rates[item] = rates.GetValueOrDefault(item) + amount;

        for (var c = 0; c < model.Columns.Count; c++)
        {
            var value = values[c];
            if (value <= _options.RateEpsilon)
            {
                continue;
            }
            switch (model.Columns[c])
            {
                case RunColumn run:
                    var recipe = run.Recipe;
                    var variant = run.Variant;
                    for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
                    {
                        Accumulate(produced, index.OutputItem[o], index.OutputYield[o] * variant.OutputFactor * value);
                    }
                    for (var slot = 0; slot < index.SlotCount(recipe); slot++)
                    {
                        if (index.AlternativeCount(recipe, slot) == 1)
                        {
                            var a = index.AlternativeAt(recipe, slot, 0);
                            Accumulate(consumed, index.AlternativeItem[a], index.AlternativeAmount[a] * value);
                        }
                    }
                    if (variant.SteamItem is { } steamItem)
                    {
                        Accumulate(consumed, steamItem, variant.SteamPerRun * value);
                    }
                    cleanroomHosts |= context.Recipes.NeedsCleanroom(recipe);
                    var busy = variant.BusyMachines(value);
                    drawEuT += variant.DrawEuT(value);
                    busyMachines += busy;
                    lines.Add(new FactoryLine(
                        index.RecipeIds[recipe],
                        index.Machine[recipe],
                        variant.MachineItemId,
                        value,
                        variant.OcSteps,
                        variant.Parallels,
                        busy,
                        variant.IsDurationless,
                        variant.Estimated,
                        variant.DurationSeconds,
                        variant.IsDurationless
                            ? 0
                            : variant.EuPerRun * variant.Parallels / (variant.DurationSeconds * Ticks.PerSecond)));
                    break;
                case SplitColumn split:
                    Accumulate(consumed, split.Item, split.Amount * value);
                    break;
                case GenerateColumn generate:
                    var line = generate.Line;
                    Accumulate(consumed, line.FuelItem, line.UnitsPerSecond * value);
                    if (line.CondensateItem is { } condensate)
                    {
                        Accumulate(produced, condensate, line.CondensatePerSecond * value);
                    }
                    foreach (var flow in line.Inputs)
                    {
                        Accumulate(consumed, flow.Item, flow.PerSecond * value);
                    }
                    foreach (var flow in line.Outputs)
                    {
                        Accumulate(produced, flow.Item, flow.PerSecond * value);
                    }
                    exportEuT += line.NetEuT * value;
                    busyMachines += value;
                    lines.Add(new FactoryLine(
                        line.LineId(index),
                        line.Map,
                        line.BlockItemId,
                        value,
                        0,
                        1,
                        value,
                        Durationless: false,
                        Estimated: false,
                        EuTPerMachine: -line.NetEuT));
                    break;
                case SupplyColumn supply:
                    Accumulate(supplied, supply.Item, value);
                    break;
                case BuyColumn buy:
                    Accumulate(bought, buy.Item, value);
                    cost += value * model.ChargedWeight(index, buy.Item);
                    break;
            }
        }

        // The hosting cleanroom stays out of the LP: one warm room and its draw are a post-solve overhead.
        if (cleanroomHosts)
        {
            var environment = context.Environment;
            var hostingDraw = environment.CleanroomDrawEuT(context.Garage.DefaultTier);
            drawEuT += hostingDraw;
            busyMachines += 1;
            lines.Add(new FactoryLine(
                FactoryEnvironment.CleanroomLineId, "Cleanroom", environment.CleanroomItemId, 1, 0, 1, 1,
                Durationless: false, Estimated: false, EuTPerMachine: hostingDraw));
        }

        var allWarnings = new List<FactoryWarning>(warnings);
        foreach (var (item, rate) in targets.Consume.OrderBy(pair => pair.Key))
        {
            var achieved = supplied.GetValueOrDefault(item);
            if (achieved < rate - Math.Max(_options.RateEpsilon, _options.LayerTolerance * rate))
            {
                allWarnings.Add(FactoryWarning.ConsumeShortfall(index.ItemIds[item]));
            }
        }

        var flows = new List<FactoryItemFlow>();
        var inflows = new List<FactoryInflow>();
        foreach (var item in produced.Keys.Union(consumed.Keys).Union(bought.Keys).Union(supplied.Keys).Order())
        {
            var made = produced.GetValueOrDefault(item);
            var used = consumed.GetValueOrDefault(item);
            var buy = bought.GetValueOrDefault(item);
            var surplus = Math.Max(0, made + buy - used - targets.ProduceRate(item));
            var supply = supplied.GetValueOrDefault(item);
            if (made > _options.RateEpsilon || used > _options.RateEpsilon || supply > _options.RateEpsilon)
            {
                flows.Add(new FactoryItemFlow(
                    index.ItemIds[item], made, used, surplus, supply, infinite.Contains(item)));
            }
            if (buy > _options.RateEpsilon)
            {
                inflows.Add(new FactoryInflow(
                    index.ItemIds[item], buy, model.ChargedWeight(index, item), infinite.Contains(item)));
            }
        }

        return new FactoryPlan(
            FactoryPlanStatus.Solved, lines, flows, inflows, allWarnings, cost, drawEuT, exportEuT, busyMachines);
    }
}
