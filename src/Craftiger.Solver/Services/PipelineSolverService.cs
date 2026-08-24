using Craftiger.Solver.Interfaces;
using Craftiger.Solver.Models;

namespace Craftiger.Solver.Services;

/// <summary>Builds the factory flow LP and reads the solution back. Variables are runs per
/// second per candidate recipe, one split variable per choice-slot alternative, and one
/// purchase variable per leaf; balance rows keep every item's net non-negative, with produce
/// targets raising the bound. The candidate walk continues through leaf-class items — unlike
/// the cost closure, which ends at leaves — because a factory may produce a leaf (every ingot
/// is one) and buying is just the competing route the purchase variable prices.</summary>
public sealed class PipelineSolverService(
    ILeafWeightService leafWeights,
    IGarageLegalityService legality,
    ILinearProgramSolver solver) : IPipelineSolverService
{
    /// <summary>Below this, a rate is solver noise, not flow.</summary>
    private const double RateEpsilon = 1e-8;

    private const double TicksPerSecond = 20.0;

    private enum ColumnKind
    {
        Run,
        Split,
        Buy,
    }

    /// <summary>What a column means when reading the solution back: the recipe for runs, the
    /// consumed item and per-run amount for splits, the item for purchases.</summary>
    private readonly record struct ColumnMeta(ColumnKind Kind, int Recipe, int Item, long Amount);

    public FactoryPlan Solve(
        SolverGraph graph,
        FactoryRecipeData recipes,
        Garage garage,
        WeightSettings weights,
        FactoryRequest request)
    {
        var index = graph.Index;
        var warnings = new List<FactoryWarning>();
        var targets = NormalizeTargets(index, request, warnings);
        if (targets is null)
        {
            return Empty(FactoryPlanStatus.Failed, warnings);
        }

        var candidates = CandidateRecipes(index, garage, targets.Keys);
        foreach (var target in targets.Keys.Order())
        {
            if (!index.IsLeaf(target) && !candidates.Any(r => Produces(index, r, target)))
            {
                warnings.Add(new FactoryWarning("unreachable_target", index.ItemIds[target]));
            }
        }
        if (warnings.Count > 0)
        {
            return Empty(FactoryPlanStatus.Infeasible, warnings);
        }

        var model = BuildModel(graph, recipes, weights, request, targets, candidates, warnings);
        var result = solver.Solve(model.Program);
        if (result.Status != LpSolveStatus.Optimal)
        {
            var (status, kind) = result.Status switch
            {
                LpSolveStatus.Infeasible => (FactoryPlanStatus.Infeasible, "infeasible"),
                LpSolveStatus.Unbounded => (FactoryPlanStatus.Unbounded, "free_lunch"),
                LpSolveStatus.TimedOut => (FactoryPlanStatus.TimedOut, "timeout"),
                _ => (FactoryPlanStatus.Failed, "solver_error"),
            };
            warnings.Add(new FactoryWarning(kind, ""));
            return Empty(status, warnings);
        }

        return Interpret(index, recipes, model, targets, result.ColumnValues, warnings);
    }

    private static FactoryPlan Empty(FactoryPlanStatus status, List<FactoryWarning> warnings)
    {
        return new FactoryPlan(status, [], [], [], warnings, 0, 0, 0);
    }

    /// <summary>Produce targets as item position → rate, duplicates summed; null when any
    /// target cannot enter the model at all.</summary>
    private static Dictionary<int, double>? NormalizeTargets(
        SolverIndex index, FactoryRequest request, List<FactoryWarning> warnings)
    {
        var targets = new Dictionary<int, double>();
        foreach (var target in request.Targets)
        {
            if (target.Kind != FactoryTargetKind.Produce)
            {
                warnings.Add(new FactoryWarning("target_unsupported", target.ItemId ?? ""));
                continue;
            }
            if (target.ItemId is null || !index.TryGetItem(target.ItemId, out var item))
            {
                warnings.Add(new FactoryWarning("target_unknown", target.ItemId ?? ""));
                continue;
            }
            if (target.Rate > 0)
            {
                targets[item] = targets.GetValueOrDefault(item) + target.Rate;
            }
        }
        return warnings.Count > 0 ? null : targets;
    }

    /// <summary>Garage-legal recipes upstream of the targets, walking producers through every
    /// slot alternative and through leaves, in recipe position order.</summary>
    private List<int> CandidateRecipes(SolverIndex index, Garage garage, IEnumerable<int> targets)
    {
        var candidates = new HashSet<int>();
        var seen = new HashSet<int>();
        var pending = new Stack<int>();
        foreach (var target in targets)
        {
            pending.Push(target);
        }
        while (pending.TryPop(out var item))
        {
            if (!seen.Add(item))
            {
                continue;
            }
            for (var p = index.ProducerStart[item]; p < index.ProducerStart[item + 1]; p++)
            {
                var recipe = index.ProducerRecipe[p];
                if (!legality.IsLegal(index, recipe, garage) || !candidates.Add(recipe))
                {
                    continue;
                }
                var start = index.AlternativeStart[index.SlotStart[recipe]];
                var end = index.AlternativeStart[index.SlotStart[recipe + 1]];
                for (var a = start; a < end; a++)
                {
                    pending.Push(index.AlternativeItem[a]);
                }
            }
        }
        return [.. candidates.Order()];
    }

    private static bool Produces(SolverIndex index, int recipe, int item)
    {
        for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
        {
            if (index.OutputItem[o] == item)
            {
                return true;
            }
        }
        return false;
    }

    private sealed record Model(
        LinearProgram Program,
        IReadOnlyList<ColumnMeta> Columns,
        IReadOnlyList<int> RowItems,
        IReadOnlyDictionary<string, double> Weights);

    private Model BuildModel(
        SolverGraph graph,
        FactoryRecipeData recipes,
        WeightSettings weights,
        FactoryRequest request,
        Dictionary<int, double> targets,
        List<int> candidates,
        List<FactoryWarning> warnings)
    {
        var index = graph.Index;
        var resolvedWeights = leafWeights.Resolve(graph, weights);
        var pinned = PinnedAway(index, request.Pins, candidates, warnings);

        var rows = new List<LpRow>();
        var rowItems = new List<int>();
        var rowOf = new Dictionary<int, int>();
        var columns = new List<LpColumn>();
        var metas = new List<ColumnMeta>();

        int RowOf(int item)
        {
            if (!rowOf.TryGetValue(item, out var row))
            {
                row = rows.Count;
                rowOf[item] = row;
                rows.Add(new LpRow(targets.GetValueOrDefault(item), double.PositiveInfinity));
                rowItems.Add(item);
            }
            return row;
        }

        foreach (var target in targets.Keys.Order())
        {
            RowOf(target);
        }

        foreach (var recipe in candidates)
        {
            var net = new Dictionary<int, double>();
            for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
            {
                var row = RowOf(index.OutputItem[o]);
                net[row] = net.GetValueOrDefault(row) + index.OutputYield[o];
            }

            var splits = new List<(int Slot, int LinkRow)>();
            for (var slot = 0; slot < index.SlotCount(recipe); slot++)
            {
                if (index.AlternativeCount(recipe, slot) == 1)
                {
                    var a = index.AlternativeAt(recipe, slot, 0);
                    var row = RowOf(index.AlternativeItem[a]);
                    net[row] = net.GetValueOrDefault(row) - index.AlternativeAmount[a];
                }
                else
                {
                    // One link row per choice slot: the split variables must sum to the runs.
                    var link = rows.Count;
                    rows.Add(new LpRow(0, 0));
                    rowItems.Add(-1);
                    net[link] = net.GetValueOrDefault(link) - 1;
                    splits.Add((slot, link));
                }
            }

            var upper = pinned.Contains(recipe) ? 0 : double.PositiveInfinity;
            columns.Add(new LpColumn(0, upper, Sorted(net)));
            metas.Add(new ColumnMeta(ColumnKind.Run, recipe, -1, 0));

            foreach (var (slot, link) in splits)
            {
                for (var alt = 0; alt < index.AlternativeCount(recipe, slot); alt++)
                {
                    var a = index.AlternativeAt(recipe, slot, alt);
                    var item = index.AlternativeItem[a];
                    var amount = index.AlternativeAmount[a];
                    var entries = new Dictionary<int, double>
                    {
                        [RowOf(item)] = -amount,
                        [link] = 1,
                    };
                    columns.Add(new LpColumn(0, double.PositiveInfinity, Sorted(entries)));
                    metas.Add(new ColumnMeta(ColumnKind.Split, recipe, item, amount));
                }
            }
        }

        // Purchase variables close every leaf's balance; consuming internal flow offsets them.
        foreach (var (item, row) in rowOf.OrderBy(pair => pair.Key))
        {
            if (index.IsLeaf(item))
            {
                columns.Add(new LpColumn(0, double.PositiveInfinity, [new LpEntry(row, 1)]));
                metas.Add(new ColumnMeta(ColumnKind.Buy, -1, item, 0));
            }
        }

        var program = new LinearProgram(
            columns,
            rows,
            BuildObjectives(index, recipes, request, metas, resolvedWeights),
            request.TimeLimitSeconds);
        return new Model(program, metas, rowItems, resolvedWeights);
    }

    /// <summary>Recipes a pin forces to zero: every other candidate producing the pinned item
    /// deterministically. Chanced byproduct rows stay free, and a pin whose item is outside
    /// the closure is simply inactive.</summary>
    private static HashSet<int> PinnedAway(
        SolverIndex index,
        IReadOnlyDictionary<string, string> pins,
        List<int> candidates,
        List<FactoryWarning> warnings)
    {
        var pinnedAway = new HashSet<int>();
        foreach (var (itemId, recipeId) in pins.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!index.TryGetItem(itemId, out var item))
            {
                continue;
            }
            var producers = candidates.Where(r => ProducesDeterministically(index, r, item)).ToList();
            if (producers.Count == 0)
            {
                continue;
            }
            if (!index.TryGetRecipe(recipeId, out var pin))
            {
                warnings.Add(new FactoryWarning("pin_unknown", itemId));
                continue;
            }
            if (!candidates.Contains(pin))
            {
                warnings.Add(new FactoryWarning("pin_illegal", itemId));
            }
            foreach (var producer in producers)
            {
                if (producer != pin)
                {
                    pinnedAway.Add(producer);
                }
            }
        }
        return pinnedAway;
    }

    private static bool ProducesDeterministically(SolverIndex index, int recipe, int item)
    {
        for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
        {
            if (index.OutputItem[o] == item && index.OutputChance[o] >= 1)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The user's layers in priority order, then the hidden canonicalization layer
    /// minimizing total runs and purchases — it pins every variable the earlier layers left
    /// free, so degenerate optima come back model-determined.</summary>
    private static List<LpObjective> BuildObjectives(
        SolverIndex index,
        FactoryRecipeData recipes,
        FactoryRequest request,
        List<ColumnMeta> metas,
        IReadOnlyDictionary<string, double> weights)
    {
        var priority = request.Priority.Count > 0
            ? request.Priority.Distinct().ToList()
            : [FactoryObjective.Resource, FactoryObjective.Energy, FactoryObjective.Machines];

        var objectives = new List<LpObjective>();
        foreach (var objective in priority)
        {
            var coefficients = new List<LpEntry>();
            for (var c = 0; c < metas.Count; c++)
            {
                var meta = metas[c];
                var coefficient = objective switch
                {
                    FactoryObjective.Resource when meta.Kind == ColumnKind.Buy =>
                        weights.GetValueOrDefault(index.ItemIds[meta.Item], 1),
                    FactoryObjective.Energy when meta.Kind == ColumnKind.Run =>
                        // kEU per run: duration ticks × EU/t × amps ÷ 1000.
                        recipes.DurationTicks[meta.Recipe] * (double)recipes.EuT[meta.Recipe] * recipes.Amps[meta.Recipe] / 1000,
                    FactoryObjective.Machines when meta.Kind == ColumnKind.Run =>
                        recipes.DurationTicks[meta.Recipe] / TicksPerSecond,
                    _ => 0.0,
                };
                if (coefficient != 0)
                {
                    coefficients.Add(new LpEntry(c, coefficient));
                }
            }
            objectives.Add(new LpObjective(Maximize: false, coefficients));
        }

        var canonical = new List<LpEntry>();
        for (var c = 0; c < metas.Count; c++)
        {
            if (metas[c].Kind != ColumnKind.Split)
            {
                canonical.Add(new LpEntry(c, 1));
            }
        }
        objectives.Add(new LpObjective(Maximize: false, canonical));
        return objectives;
    }

    private static List<LpEntry> Sorted(Dictionary<int, double> entries)
    {
        return [.. entries.OrderBy(entry => entry.Key).Select(entry => new LpEntry(entry.Key, entry.Value))];
    }

    private static FactoryPlan Interpret(
        SolverIndex index,
        FactoryRecipeData recipes,
        Model model,
        Dictionary<int, double> targets,
        IReadOnlyList<double> values,
        List<FactoryWarning> warnings)
    {
        var produced = new Dictionary<int, double>();
        var consumed = new Dictionary<int, double>();
        var bought = new Dictionary<int, double>();
        var lines = new List<FactoryLine>();
        var cost = 0.0;
        var drawEuT = 0.0;
        var busyMachines = 0.0;

        for (var c = 0; c < model.Columns.Count; c++)
        {
            var value = values[c];
            if (value <= RateEpsilon)
            {
                continue;
            }
            var meta = model.Columns[c];
            switch (meta.Kind)
            {
                case ColumnKind.Run:
                    var recipe = meta.Recipe;
                    for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
                    {
                        var item = index.OutputItem[o];
                        produced[item] = produced.GetValueOrDefault(item) + index.OutputYield[o] * value;
                    }
                    for (var slot = 0; slot < index.SlotCount(recipe); slot++)
                    {
                        if (index.AlternativeCount(recipe, slot) == 1)
                        {
                            var a = index.AlternativeAt(recipe, slot, 0);
                            var item = index.AlternativeItem[a];
                            consumed[item] = consumed.GetValueOrDefault(item) + index.AlternativeAmount[a] * value;
                        }
                    }
                    var durationSeconds = recipes.DurationTicks[recipe] / TicksPerSecond;
                    drawEuT += value * recipes.DurationTicks[recipe] * (double)recipes.EuT[recipe] * recipes.Amps[recipe] / TicksPerSecond;
                    busyMachines += value * durationSeconds;
                    lines.Add(new FactoryLine(
                        index.RecipeIds[recipe],
                        index.Machine[recipe],
                        value,
                        value * durationSeconds,
                        recipes.DurationTicks[recipe] == 0));
                    break;
                case ColumnKind.Split:
                    consumed[meta.Item] = consumed.GetValueOrDefault(meta.Item) + meta.Amount * value;
                    break;
                case ColumnKind.Buy:
                    bought[meta.Item] = bought.GetValueOrDefault(meta.Item) + value;
                    cost += value * model.Weights.GetValueOrDefault(index.ItemIds[meta.Item], 1);
                    break;
            }
        }

        var flows = new List<FactoryItemFlow>();
        var inflows = new List<FactoryInflow>();
        foreach (var item in produced.Keys.Union(consumed.Keys).Union(bought.Keys).Order())
        {
            var made = produced.GetValueOrDefault(item);
            var used = consumed.GetValueOrDefault(item);
            var buy = bought.GetValueOrDefault(item);
            var surplus = Math.Max(0, made + buy - used - targets.GetValueOrDefault(item));
            if (made > RateEpsilon || used > RateEpsilon)
            {
                flows.Add(new FactoryItemFlow(index.ItemIds[item], made, used, surplus));
            }
            if (buy > RateEpsilon)
            {
                inflows.Add(new FactoryInflow(
                    index.ItemIds[item], buy, model.Weights.GetValueOrDefault(index.ItemIds[item], 1)));
            }
        }

        return new FactoryPlan(
            FactoryPlanStatus.Solved, lines, flows, inflows, warnings, cost, drawEuT, busyMachines);
    }
}
