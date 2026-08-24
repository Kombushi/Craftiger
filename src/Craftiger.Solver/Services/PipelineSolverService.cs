using Craftiger.Solver.Interfaces;
using Craftiger.Solver.Models;

namespace Craftiger.Solver.Services;

/// <summary>Builds the factory flow LP and reads the solution back. Variables are runs per
/// second per candidate recipe, one split variable per choice-slot alternative, and one
/// purchase variable per leaf; balance rows keep every item's net non-negative, with produce
/// targets raising the bound. The candidate walk continues through leaf-class items — unlike
/// the cost closure, which ends at leaves — because a factory may produce a leaf (every ingot
/// is one) and buying is just the competing route the purchase variable prices. The walk
/// prunes recipes priced far above their outputs' solved costs: the unpruned closure reaches
/// two thirds of the artifact and its LP broke the solver numerics, so exotic byproduct
/// synergies beyond the band are traded for a model that solves — flagged on the plan.</summary>
public sealed class PipelineSolverService(
    ILeafWeightService leafWeights,
    IGarageLegalityService legality,
    ICostSolverService costSolver,
    ILinearProgramSolver solver) : IPipelineSolverService
{
    /// <summary>A candidate survives when some output prices within this factor of the item's
    /// solved cost; the floor keeps cheap recipes for items priced near zero.</summary>
    private const double PruneFactor = 4.0;

    private const double PruneFloor = 1.0;

    /// <summary>Below this, a rate is layer-tolerance noise, not flow: the lexicographic
    /// slack legitimately leaves slivers up to roughly the relative tolerance behind.</summary>
    private const double RateEpsilon = 1e-5;

    /// <summary>Each layer's optimum binds the next within a tenth of a percent. Tighter
    /// corridors are invisible in any displayed plan but broke the simplex numerics: postsolve
    /// solutions landed outside them and feasibility recovery on the full model never
    /// converged.</summary>
    private const double LayerTolerance = 1e-3;

    private const double TicksPerSecond = 20.0;

    private enum ColumnKind
    {
        Run,
        Split,
        Buy,
    }

    /// <summary>What a column means when reading the solution back: the recipe and variant
    /// for runs, the consumed item and per-run amount for splits, the item for purchases.</summary>
    private readonly record struct ColumnMeta(ColumnKind Kind, int Recipe, int Item, long Amount, int Variant = -1);

    /// <summary>One way to run a recipe: a machine block at an overclock step, with its
    /// effective duration, energy per run, and parallels resolved against garage state.</summary>
    private sealed record RunVariant(
        int Recipe,
        string? MachineItemId,
        int OcSteps,
        double Parallels,
        double DurationSeconds,
        double EuPerRun,
        bool Estimated);

    public FactoryPlan Solve(
        SolverGraph graph,
        FactoryRecipeData recipes,
        FactoryMachineData machines,
        CostTable costs,
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

        var candidates = CandidateRecipes(index, costs, garage, targets.Keys, request.Pins, warnings);
        var unreachable = false;
        foreach (var target in targets.Keys.Order())
        {
            if (!index.IsLeaf(target) && !candidates.Any(r => Produces(index, r, target)))
            {
                warnings.Add(new FactoryWarning("unreachable_target", index.ItemIds[target]));
                unreachable = true;
            }
        }
        if (unreachable)
        {
            return Empty(FactoryPlanStatus.Infeasible, warnings);
        }

        var model = BuildModel(graph, recipes, machines, garage, weights, request, targets, candidates, warnings);
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

        return Interpret(index, model, targets, result.ColumnValues, warnings);
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
    /// slot alternative and through leaves, in recipe position order. Recipes outside the
    /// cost band are pruned before the walk recurses into them; pinned recipes always
    /// survive.</summary>
    private List<int> CandidateRecipes(
        SolverIndex index,
        CostTable costs,
        Garage garage,
        IEnumerable<int> targets,
        IReadOnlyDictionary<string, string> pins,
        List<FactoryWarning> warnings)
    {
        var pinned = new HashSet<int>();
        foreach (var recipeId in pins.Values)
        {
            if (index.TryGetRecipe(recipeId, out var pin))
            {
                pinned.Add(pin);
            }
        }

        var candidates = new HashSet<int>();
        var rejected = new HashSet<int>();
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
                if (candidates.Contains(recipe) || rejected.Contains(recipe)
                    || !legality.IsLegal(index, recipe, garage))
                {
                    continue;
                }
                if (!pinned.Contains(recipe) && !WithinCostBand(index, costs, recipe))
                {
                    rejected.Add(recipe);
                    continue;
                }
                candidates.Add(recipe);
                var start = index.AlternativeStart[index.SlotStart[recipe]];
                var end = index.AlternativeStart[index.SlotStart[recipe + 1]];
                for (var a = start; a < end; a++)
                {
                    pending.Push(index.AlternativeItem[a]);
                }
            }
        }
        if (rejected.Count > 0)
        {
            warnings.Add(new FactoryWarning("routes_pruned", ""));
        }
        return [.. candidates.Order()];
    }

    /// <summary>Whether some output of the recipe prices within the band of its solved cost.</summary>
    private bool WithinCostBand(SolverIndex index, CostTable costs, int recipe)
    {
        for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
        {
            var item = index.OutputItem[o];
            if (costs.TryCost(item, out var solved)
                && costSolver.Candidate(costs, recipe, item) <= PruneFactor * solved + PruneFloor)
            {
                return true;
            }
        }
        return false;
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
        IReadOnlyList<RunVariant> Variants,
        IReadOnlyList<int> RowItems,
        IReadOnlyDictionary<string, double> Weights);

    /// <summary>Resolved machine modifiers: the factors are applied to base duration and
    /// per-tick energy, parallels divide busy machines.</summary>
    private readonly record struct BlockEffects(double DurationFactor, double EuFactor, double Parallels, bool Estimated);

    /// <summary>Every way the garage can run a recipe, in block id then overclock order:
    /// eligible blocks of the map at each voltage step up to their tier, or an anonymous
    /// flagged block at the map tier when the map ships no usable block data. Heat overclocks
    /// turn perfect and discount energy per the coil excess; durationless recipes get one
    /// free anonymous variant.</summary>
    private List<RunVariant> Variants(
        SolverIndex index,
        FactoryRecipeData data,
        FactoryMachineData machines,
        Garage garage,
        int recipe)
    {
        var map = index.Machine[recipe];
        var mapTier = legality.EffectiveTier(map, garage) ?? 0;
        var durationTicks = data.DurationTicks[recipe];
        if (durationTicks == 0)
        {
            return [new RunVariant(recipe, null, 0, 1, 0, 0, Estimated: false)];
        }

        var multiBuilt = index.MultiTier[recipe] >= 0 && garage.BuiltMultiblocks.Contains(map);
        var singleRequired = index.Tier[recipe];
        var multiRequired = index.MultiTier[recipe] >= 0 ? index.MultiTier[recipe] : index.Tier[recipe];

        var perfectSteps = 0;
        var heatEuFactor = 1.0;
        if (index.Heat[recipe] >= 0)
        {
            var excess = legality.HeatCapacity(map, garage) - index.Heat[recipe];
            if (excess > 0)
            {
                perfectSteps = excess / 1800;
                heatEuFactor = Math.Pow(0.95, excess / 900);
            }
        }

        var variants = new List<RunVariant>();
        var coilTier = machines.CoilTier(garage, map);
        var blocks = machines.BlocksByMap.GetValueOrDefault(map);
        if (blocks is not null && blocks.Count > 0)
        {
            var allMulti = blocks.All(b => b.Multiblock);
            foreach (var block in blocks.OrderBy(b => b.ItemId, StringComparer.Ordinal))
            {
                if (block.Steam || block.Era is not { } era || era > garage.DefaultTier)
                {
                    continue;
                }
                int voltageTier;
                int required;
                if (block.Multiblock)
                {
                    if (!allMulti && !multiBuilt)
                    {
                        continue;
                    }
                    voltageTier = mapTier;
                    required = multiRequired;
                }
                else
                {
                    if (block.Tier is not { } tier || tier < singleRequired || tier > mapTier)
                    {
                        continue;
                    }
                    voltageTier = tier;
                    required = singleRequired;
                }

                var effects = ResolveBonuses(block, coilTier, voltageTier);
                AddOcVariants(
                    variants, recipe, block.ItemId, durationTicks, data.EuT[recipe] * data.Amps[recipe],
                    voltageTier - required, perfectSteps, heatEuFactor, effects);
            }
        }

        if (variants.Count == 0)
        {
            // The map ships no usable block: an anonymous block at the map tier, flagged.
            var required = multiBuilt ? multiRequired : singleRequired;
            AddOcVariants(
                variants, recipe, null, durationTicks, data.EuT[recipe] * data.Amps[recipe],
                mapTier - required, perfectSteps, heatEuFactor,
                new BlockEffects(1, 1, 1, Estimated: true));
        }
        return variants;
    }

    private static void AddOcVariants(
        List<RunVariant> variants,
        int recipe,
        string? machineItemId,
        long durationTicks,
        long euPerTick,
        int maxSteps,
        int perfectSteps,
        double heatEuFactor,
        BlockEffects effects)
    {
        var baseSeconds = durationTicks / TicksPerSecond * effects.DurationFactor;
        var baseEu = durationTicks * (double)euPerTick * heatEuFactor * effects.EuFactor * effects.DurationFactor;
        for (var k = 0; k <= Math.Max(0, maxSteps); k++)
        {
            var perfect = Math.Min(k, perfectSteps);
            var standard = k - perfect;
            variants.Add(new RunVariant(
                recipe,
                machineItemId,
                k,
                effects.Parallels,
                baseSeconds / (Math.Pow(2, standard) * Math.Pow(4, perfect)),
                baseEu * Math.Pow(2, standard),
                effects.Estimated));
        }
    }

    /// <summary>Bonus semantics as the exporter's lang templates define them: SPEED is the
    /// percentage the machine runs at, EU_DISCOUNT the percentage it draws, per-tier kinds add
    /// per axis tier (negative for discounts), absolute speed multiplies by tier, and
    /// multiplicative parallels double per tier. Axes the garage cannot resolve contribute
    /// nothing and flag the line; a bonus-less multiblock runs flagged at one parallel.</summary>
    private static BlockEffects ResolveBonuses(FactoryMachineBlock block, int coilTier, int voltageTier)
    {
        var speedPercent = 100.0;
        double? absoluteSpeedPerTier = null;
        var absoluteSpeedTier = 0;
        var euPercent = 100.0;
        var parallelBase = (double)Math.Max(1, block.MaxParallel);
        var parallels = parallelBase;
        var estimated = block.Multiblock && block.MaxParallel <= 1 && block.Bonuses.Count == 0;

        foreach (var bonus in block.Bonuses)
        {
            var tier = bonus.TierAxis switch
            {
                null => 0,
                "COIL" => coilTier,
                "VOLTAGE" => voltageTier,
                _ => -1,
            };
            if (tier < 0)
            {
                // An axis without a garage picker yet: the conservative base, flagged.
                estimated = true;
                continue;
            }
            switch (bonus.Kind)
            {
                case "SPEED":
                    speedPercent = bonus.Bonus;
                    break;
                case "SPEED_BONUS_PER_TIER":
                    speedPercent += bonus.Bonus * tier;
                    break;
                case "SPEED_PER_TIER":
                    if (tier == 0)
                    {
                        // The machine needs the component to run at all; assume the first tier.
                        tier = 1;
                        estimated = true;
                    }
                    absoluteSpeedPerTier = bonus.Bonus;
                    absoluteSpeedTier = tier;
                    break;
                case "EU_DISCOUNT":
                    euPercent = bonus.Bonus;
                    break;
                case "EU_DISCOUNT_PER_TIER":
                    euPercent += bonus.Bonus * tier;
                    break;
                case "PARALLEL":
                    parallelBase = bonus.Bonus;
                    parallels = parallelBase;
                    break;
                case "PARALLEL_PER_TIER":
                    parallels = bonus.Multiplicative
                        ? parallelBase * Math.Pow(bonus.Bonus, tier)
                        : parallelBase + bonus.Bonus * tier;
                    break;
                default:
                    estimated = true;
                    break;
            }
        }

        var durationFactor = absoluteSpeedPerTier is { } perTier
            ? 100.0 / (perTier * absoluteSpeedTier)
            : 100.0 / Math.Max(speedPercent, 1);
        var euFactor = euPercent / 100.0;
        if (euFactor < 0.05)
        {
            euFactor = 0.05;
            estimated = true;
        }
        return new BlockEffects(durationFactor, euFactor, Math.Max(1, parallels), estimated);
    }

    private Model BuildModel(
        SolverGraph graph,
        FactoryRecipeData recipes,
        FactoryMachineData machines,
        Garage garage,
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
        var variants = new List<RunVariant>();

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
                    // One link row per choice slot: the splits must sum to the recipe's total
                    // runs across every machine and overclock variant.
                    var link = rows.Count;
                    rows.Add(new LpRow(0, 0));
                    rowItems.Add(-1);
                    net[link] = net.GetValueOrDefault(link) - 1;
                    splits.Add((slot, link));
                }
            }

            var upper = pinned.Contains(recipe) ? 0 : double.PositiveInfinity;
            var entries = Sorted(net);
            foreach (var variant in Variants(index, recipes, machines, garage, recipe))
            {
                columns.Add(new LpColumn(0, upper, entries));
                metas.Add(new ColumnMeta(ColumnKind.Run, recipe, -1, 0, variants.Count));
                variants.Add(variant);
            }

            foreach (var (slot, link) in splits)
            {
                for (var alt = 0; alt < index.AlternativeCount(recipe, slot); alt++)
                {
                    var a = index.AlternativeAt(recipe, slot, alt);
                    var item = index.AlternativeItem[a];
                    var amount = index.AlternativeAmount[a];
                    var splitEntries = new Dictionary<int, double>
                    {
                        [RowOf(item)] = -amount,
                        [link] = 1,
                    };
                    columns.Add(new LpColumn(0, double.PositiveInfinity, Sorted(splitEntries)));
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
            BuildObjectives(index, request, metas, variants, resolvedWeights),
            request.TimeLimitSeconds);
        return new Model(program, metas, variants, rowItems, resolvedWeights);
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
        FactoryRequest request,
        List<ColumnMeta> metas,
        List<RunVariant> variants,
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
                        variants[meta.Variant].EuPerRun / 1000,
                    FactoryObjective.Machines when meta.Kind == ColumnKind.Run =>
                        variants[meta.Variant].DurationSeconds / variants[meta.Variant].Parallels,
                    _ => 0.0,
                };
                if (coefficient != 0)
                {
                    coefficients.Add(new LpEntry(c, coefficient));
                }
            }
            objectives.Add(new LpObjective(Maximize: false, coefficients, RelTolerance: LayerTolerance));
        }

        var canonical = new List<LpEntry>();
        for (var c = 0; c < metas.Count; c++)
        {
            if (metas[c].Kind != ColumnKind.Split)
            {
                canonical.Add(new LpEntry(c, 1));
            }
        }
        objectives.Add(new LpObjective(
            Maximize: false, canonical, RelTolerance: LayerTolerance, SupportRestricted: true));
        return objectives;
    }

    private static List<LpEntry> Sorted(Dictionary<int, double> entries)
    {
        return [.. entries.OrderBy(entry => entry.Key).Select(entry => new LpEntry(entry.Key, entry.Value))];
    }

    private static FactoryPlan Interpret(
        SolverIndex index,
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
                    var variant = model.Variants[meta.Variant];
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
                    var busy = value * variant.DurationSeconds / variant.Parallels;
                    drawEuT += value * variant.EuPerRun / TicksPerSecond;
                    busyMachines += busy;
                    lines.Add(new FactoryLine(
                        index.RecipeIds[recipe],
                        index.Machine[recipe],
                        variant.MachineItemId,
                        value,
                        variant.OcSteps,
                        variant.Parallels,
                        busy,
                        variant.DurationSeconds == 0,
                        variant.Estimated));
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
