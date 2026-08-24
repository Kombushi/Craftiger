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

    /// <summary>The generator band's floor, in fuel weight per net EU/t — far below the item
    /// floor, because competitive fuel chains price near zero.</summary>
    private const double GeneratorPruneFloor = 1e-3;

    /// <summary>Below this, a rate is layer-tolerance noise, not flow: the lexicographic
    /// slack legitimately leaves slivers up to roughly the relative tolerance behind.</summary>
    private const double RateEpsilon = 1e-5;

    /// <summary>Each layer's optimum binds the next within a tenth of a percent. Tighter
    /// corridors are invisible in any displayed plan but broke the simplex numerics: postsolve
    /// solutions landed outside them and feasibility recovery on the full model never
    /// converged.</summary>
    private const double LayerTolerance = 1e-3;

    private const double TicksPerSecond = 20.0;

    /// <summary>Fuel maps whose multiblocks are rotor-driven large turbines, and the fuel
    /// class their rotor stat rows use.</summary>
    private static readonly IReadOnlyDictionary<string, string> TurbineFuelClasses =
        new Dictionary<string, string>
        {
            ["Gas Turbine Fuel"] = "GAS",
            ["Plasma Generator Fuels"] = "PLASMA",
            ["Large Steam Turbine"] = "STEAM",
        };

    /// <summary>Steam turbines return 1 L of distilled water per 160 L of steam swallowed.</summary>
    private const string DistilledWaterId = "f~IC2~ic2distilledwater";

    private const double CondensatePerSteam = 1.0 / 160;

    /// <summary>Every fluid that counts as steam; a steam machine drinks any of them.</summary>
    private static readonly IReadOnlyList<string> SteamFluidIds =
        ["f~IC2~ic2steam", "f~Railcraft~steam"];

    /// <summary>Steam machines burn 2 L per EU at the bronze rate over a doubled duration —
    /// four liters per EU of the electric recipe in total, fit or high pressure alike.</summary>
    private const double SteamPerEu = 4.0;

    /// <summary>Steam's energy content: the EU-efficiency layer is carrier-neutral (user
    /// decision), so a steam machine's draw counts at this rate instead of reading as free —
    /// free-steam seeds otherwise buy hundreds of machines to shave watts of electric draw.</summary>
    private const double EuPerSteamLiter = 0.5;

    private enum ColumnKind
    {
        Run,
        Split,
        Buy,
        Generate,
        Supply,
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
        bool Estimated,
        int SteamItem = -1,
        double SteamPerRun = 0);

    /// <summary>Normalized targets: produce rates by item position, consume rates by item
    /// position, the summed net EU/t export, and the per-tier quality bands energy targets
    /// demand.</summary>
    private sealed record Targets(
        Dictionary<int, double> Produce,
        Dictionary<int, double> Consume,
        double EnergyEuT,
        IReadOnlyList<(int Tier, double Rate)> Bands);

    public FactoryPlan Solve(
        SolverGraph graph,
        FactoryRecipeData recipes,
        FactoryMachineData machines,
        FactorySeedData seeds,
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

        var generators = targets.EnergyEuT > 0
            ? EligibleGenerators(index, machines, garage, costs, targets.Bands)
            : [];
        if (targets.EnergyEuT > 0 && generators.Count == 0)
        {
            warnings.Add(new FactoryWarning("no_generator", ""));
            return Empty(FactoryPlanStatus.Infeasible, warnings);
        }
        if (targets.Bands.Count > 0 && targets.Bands.Any(band => !generators.Any(g => g.Tier >= band.Tier)))
        {
            warnings.Add(new FactoryWarning("no_generator", ""));
            return Empty(FactoryPlanStatus.Infeasible, warnings);
        }

        // Steam is drawn by machine variants, not by recipe inputs, so the walk would never
        // reach its producers on its own; a garage with legal steam blocks seeds it in.
        var steamItems = machines.BlocksByMap.Values.Any(maps => maps.Any(b =>
                b.Steam && b.Era is { } steamEra && steamEra <= garage.DefaultTier))
            ? SteamFluidIds
                .Select(id => index.TryGetItem(id, out var item) ? item : -1)
                .Where(item => item >= 0)
            : [];
        var walkTargets = targets.Produce.Keys
            .Concat(generators.Select(g => g.FuelItem))
            .Concat(steamItems)
            .Distinct();
        var (candidates, cone) = CandidateRecipes(
            index, costs, garage, walkTargets, targets.Consume.Keys, request.Pins, warnings);
        var unreachable = false;
        foreach (var target in targets.Produce.Keys.Order())
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

        var seedItems = SeedItems(index, seeds, request.MobFarms);
        var model = BuildModel(
            graph, recipes, machines, garage, weights, request, targets, candidates, cone, generators,
            seedItems, warnings);
        var result = solver.Solve(model.Program);
        if (result.Status == LpSolveStatus.Infeasible)
        {
            Diagnose(index, model, warnings);
            return Empty(FactoryPlanStatus.Infeasible, warnings);
        }
        if (result.Status != LpSolveStatus.Optimal)
        {
            var (status, kind) = result.Status switch
            {
                LpSolveStatus.Unbounded => (FactoryPlanStatus.Unbounded, "free_lunch"),
                LpSolveStatus.TimedOut => (FactoryPlanStatus.TimedOut, "timeout"),
                _ => (FactoryPlanStatus.Failed, "solver_error"),
            };
            warnings.Add(new FactoryWarning(kind, ""));
            return Empty(status, warnings);
        }

        var infinite = AutoInfinite(index, garage, seedItems);
        return Interpret(index, model, targets, result.ColumnValues, warnings, infinite);
    }

    private static FactoryPlan Empty(FactoryPlanStatus status, List<FactoryWarning> warnings)
    {
        return new FactoryPlan(status, [], [], [], warnings, 0, 0, 0, 0);
    }

    /// <summary>Produce and energy targets normalized, duplicates summed; null when any
    /// target cannot enter the model at all.</summary>
    private static Targets? NormalizeTargets(
        SolverIndex index, FactoryRequest request, List<FactoryWarning> warnings)
    {
        var produce = new Dictionary<int, double>();
        var consume = new Dictionary<int, double>();
        var energy = 0.0;
        var bands = new Dictionary<int, double>();
        var failed = false;
        foreach (var target in request.Targets)
        {
            if (target.Kind == FactoryTargetKind.Energy)
            {
                if (target.Rate > 0)
                {
                    energy += target.Rate;
                    if (target.GeneratorTier is { } tier)
                    {
                        bands[tier] = bands.GetValueOrDefault(tier) + target.Rate;
                    }
                }
                continue;
            }
            if (target.ItemId is null || !index.TryGetItem(target.ItemId, out var item))
            {
                warnings.Add(new FactoryWarning("target_unknown", target.ItemId ?? ""));
                failed = true;
                continue;
            }
            if (target.Rate <= 0)
            {
                continue;
            }
            var rates = target.Kind == FactoryTargetKind.Consume ? consume : produce;
            rates[item] = rates.GetValueOrDefault(item) + target.Rate;
        }
        return failed
            ? null
            : new Targets(
                produce, consume, energy,
                [.. bands.OrderBy(b => b.Key).Select(b => (b.Key, b.Value))]);
    }

    /// <summary>One garage-legal way to burn a fuel: a generator block and the fuel it turns
    /// into EU, with per-machine consumption and net output after the Enet transfer loss.
    /// Turbine lines also carry the rotor they spin and its fit.</summary>
    private sealed record GeneratorVariant(
        string Map,
        string BlockItemId,
        int Tier,
        string FuelItemId,
        int FuelItem,
        double UnitsPerSecond,
        double NetEuT,
        string? RotorItemId = null,
        bool Loose = false,
        int CondensateItem = -1,
        double CondensatePerUnit = 0);

    /// <summary>Every (eligible generator block, fuel) pair, in map, block, fuel order.
    /// Standard fuels burn at the block's full output; timed fuels burn at their fixed EU/t
    /// over their lifetime; large turbines spin every Pareto-best craftable rotor at each
    /// fit's optimal flow, capped by the best garage-legal dynamo hatch. Output loses
    /// <c>2^max(0, tier−1)</c> EU per amp emitted. Pairs far above the cheapest fuel weight
    /// per net EU/t are pruned the way recipes are — each quality band keeps its own
    /// cheapest qualifying pairs, so a tier demand never starves.</summary>
    private List<GeneratorVariant> EligibleGenerators(
        SolverIndex index,
        FactoryMachineData machines,
        Garage garage,
        CostTable costs,
        IReadOnlyList<(int Tier, double Rate)> bands)
    {
        var variants = new List<GeneratorVariant>();
        var frontiers = new Dictionary<(string Fuel, bool Loose, double Cap), List<FactoryRotorStats>>();
        foreach (var fuel in machines.Fuels
            .OrderBy(f => f.Map, StringComparer.Ordinal)
            .ThenBy(f => f.ItemId, StringComparer.Ordinal))
        {
            if (!index.TryGetItem(fuel.ItemId, out var fuelItem)
                || !machines.BlocksByMap.TryGetValue(fuel.Map, out var blocks))
            {
                continue;
            }
            foreach (var block in blocks.OrderBy(b => b.ItemId, StringComparer.Ordinal))
            {
                if (block.Steam || block.Era is not { } era || era > garage.DefaultTier)
                {
                    continue;
                }
                if (block.Multiblock)
                {
                    if (block.RotorTurbine
                        && TurbineFuelClasses.TryGetValue(fuel.Map, out var fuelClass)
                        && fuel.EuPerUnit is { } perUnit && perUnit > 0
                        && costs.TryCost(fuelItem, out _))
                    {
                        AddTurbineVariants(
                            variants, index, machines, garage, costs, frontiers,
                            fuel, fuelItem, block, fuelClass, perUnit);
                    }
                    continue;
                }
                if (block.Tier is not { } tier || block.GeneratorEuT is not { } outEuT)
                {
                    continue;
                }
                var amps = block.GeneratorAmps ?? 1;
                var loss = Math.Pow(2, Math.Max(0, tier - 1)) * amps;
                double unitsPerSecond;
                double rawEuT;
                if (fuel.EuT is { } fixedEuT && fuel.DurationTicks is { } lifetime && lifetime > 0)
                {
                    unitsPerSecond = fuel.Amount * TicksPerSecond / lifetime;
                    rawEuT = fixedEuT;
                }
                else if (fuel.EuPerUnit is { } euPerUnit && euPerUnit > 0)
                {
                    var effective = euPerUnit * (block.GeneratorEfficiency ?? 100) / 100;
                    rawEuT = outEuT * (double)amps;
                    unitsPerSecond = rawEuT * TicksPerSecond / effective;
                }
                else
                {
                    continue;
                }
                var netEuT = rawEuT - loss;
                if (netEuT <= 0 || !costs.TryCost(fuelItem, out _))
                {
                    continue;
                }
                variants.Add(new GeneratorVariant(
                    fuel.Map, block.ItemId, tier, fuel.ItemId, fuelItem, unitsPerSecond, netEuT));
            }
        }
        return PruneGenerators(variants, costs, bands);
    }

    /// <summary>One turbine line per rotor on the fuel class's Pareto frontier at each fit,
    /// running at that rotor's optimal flow — off-optimal is strictly worse and excluded.
    /// Per-machine numbers fold the block's parallel factor in (the XL turbos run sixteen
    /// large turbines' throughput as one controller).</summary>
    private static void AddTurbineVariants(
        List<GeneratorVariant> variants,
        SolverIndex index,
        FactoryMachineData machines,
        Garage garage,
        CostTable costs,
        Dictionary<(string Fuel, bool Loose, double Cap), List<FactoryRotorStats>> frontiers,
        FactoryFuel fuel,
        int fuelItem,
        FactoryMachineBlock block,
        string fuelClass,
        double perUnit)
    {
        var parallels = (double)Math.Max(1, block.MaxParallel);
        var capPerRotor = HatchCapacity(machines, garage, multiAmp: parallels > 1) / parallels;
        if (capPerRotor <= 0)
        {
            return;
        }
        foreach (var loose in new[] { false, true })
        {
            if (!frontiers.TryGetValue((fuelClass, loose, capPerRotor), out var frontier))
            {
                frontier = RotorFrontier(index, machines, costs, fuelClass, loose, capPerRotor);
                frontiers[(fuelClass, loose, capPerRotor)] = frontier;
            }
            foreach (var stats in frontier)
            {
                var flow = loose ? stats.LooseOptimalFlow : stats.OptimalFlow;
                var output = loose ? stats.LooseOptimalEut : stats.OptimalEut;
                if (BestHatch(machines, garage, output * parallels, multiAmp: parallels > 1)
                    is not { } hatch || hatch.NetEuT <= 0)
                {
                    continue;
                }
                var condensate = fuelClass == "STEAM" && index.TryGetItem(DistilledWaterId, out var water)
                    ? (Item: water, PerUnit: CondensatePerSteam)
                    : (Item: -1, PerUnit: 0.0);
                variants.Add(new GeneratorVariant(
                    fuel.Map, block.ItemId, hatch.Tier, fuel.ItemId, fuelItem,
                    flow / perUnit * TicksPerSecond * parallels, hatch.NetEuT,
                    stats.ItemId, loose, condensate.Item, condensate.PerUnit));
            }
        }
    }

    /// <summary>Craftable rotors not dominated on (efficiency, output) for the fuel class at
    /// the fit, both measured against the hatch capacity: a capped rotor still burns its
    /// full optimal flow, so raw stats made monster rotors dominate everything while netting
    /// worst-in-pool fuel economy. A rotor beaten on both capped axes serves no objective.</summary>
    private static List<FactoryRotorStats> RotorFrontier(
        SolverIndex index,
        FactoryMachineData machines,
        CostTable costs,
        string fuelClass,
        bool loose,
        double cap)
    {
        var craftable = machines.Rotors
            .Where(r => r.Fuel == fuelClass
                && index.TryGetItem(r.ItemId, out var item) && costs.TryCost(item, out _))
            .OrderBy(r => r.ItemId, StringComparer.Ordinal)
            .ToList();

        double Out(FactoryRotorStats r) => Math.Min(loose ? r.LooseOptimalEut : r.OptimalEut, cap);
        double Eff(FactoryRotorStats r)
        {
            var raw = loose ? r.LooseOptimalEut : r.OptimalEut;
            return raw <= 0 ? 0 : (loose ? r.LooseEfficiency : r.Efficiency) * Out(r) / raw;
        }

        return
        [
            .. craftable.Where(rotor => !craftable.Any(other =>
                other != rotor
                && Eff(other) >= Eff(rotor) && Out(other) >= Out(rotor)
                && (Eff(other) > Eff(rotor) || Out(other) > Out(rotor)
                    || string.CompareOrdinal(other.ItemId, rotor.ItemId) < 0))),
        ];
    }

    /// <summary>The largest voltage-times-amps a garage-legal hatch offers the line.</summary>
    private static double HatchCapacity(FactoryMachineData machines, Garage garage, bool multiAmp)
    {
        var capacity = 0.0;
        foreach (var hatch in machines.Dynamos)
        {
            if (hatch.Era is not { } era || era > garage.DefaultTier
                || (!multiAmp && hatch.Amps > 4))
            {
                continue;
            }
            capacity = Math.Max(capacity, (double)hatch.EuT * hatch.Amps);
        }
        return capacity;
    }

    /// <summary>The dynamo hatch that nets the most: capacity caps the line (excess is
    /// voided) while the Enet loss per amp emitted rises with hatch tier. Large turbines
    /// accept one hatch of at most four amps; the XL turbos take multi-amp hatches.</summary>
    private static (double NetEuT, int Tier)? BestHatch(
        FactoryMachineData machines, Garage garage, double rawEuT, bool multiAmp)
    {
        (double NetEuT, int Tier)? best = null;
        foreach (var hatch in machines.Dynamos.OrderBy(d => d.ItemId, StringComparer.Ordinal))
        {
            if (hatch.Era is not { } era || era > garage.DefaultTier
                || (!multiAmp && hatch.Amps > 4))
            {
                continue;
            }
            var capped = Math.Min(rawEuT, (double)hatch.EuT * hatch.Amps);
            var tier = TierOfVoltage(hatch.EuT);
            var net = capped - Math.Pow(2, Math.Max(0, tier - 1)) * (capped / hatch.EuT);
            if (best is null || net > best.Value.NetEuT)
            {
                best = (net, tier);
            }
        }
        return best;
    }

    /// <summary>The GT ladder position of a voltage: <c>V = 8·4^tier</c>.</summary>
    private static int TierOfVoltage(long voltage)
    {
        var tier = 0;
        while (voltage > 8L << (2 * tier) && tier < 14)
        {
            tier++;
        }
        return tier;
    }

    /// <summary>Keeps pairs within the cost band of the cheapest fuel weight per net EU/t —
    /// overall, and per quality band among the pairs whose tier satisfies it.</summary>
    private static List<GeneratorVariant> PruneGenerators(
        List<GeneratorVariant> variants,
        CostTable costs,
        IReadOnlyList<(int Tier, double Rate)> bands)
    {
        if (variants.Count == 0)
        {
            return variants;
        }

        double WeightPerEu(GeneratorVariant variant) =>
            costs.Cost(variant.FuelItem) * variant.UnitsPerSecond / variant.NetEuT;

        var cheapest = variants.Min(WeightPerEu);
        var cheapestPerBand = bands
            .Select(band => (band.Tier, Best: variants
                .Where(v => v.Tier >= band.Tier)
                .Select(WeightPerEu)
                .DefaultIfEmpty(double.PositiveInfinity)
                .Min()))
            .ToList();

        return
        [
            .. variants.Where(variant =>
            {
                var weight = WeightPerEu(variant);
                return weight <= PruneFactor * cheapest + GeneratorPruneFloor
                    || cheapestPerBand.Any(band =>
                        variant.Tier >= band.Tier && weight <= PruneFactor * band.Best + GeneratorPruneFloor);
            }),
        ];
    }

    /// <summary>The candidate set: the downstream cone of every consume target — consumers,
    /// recursively through their outputs — then the garage-legal upstream closure of the
    /// produce targets, the fuels, and the cone recipes' co-inputs, walking producers through
    /// every slot alternative and through leaves, in recipe position order. Recipes outside
    /// the cost band are pruned before the walk recurses into them; pinned recipes always
    /// survive.</summary>
    private (List<int> Candidates, HashSet<int> Cone) CandidateRecipes(
        SolverIndex index,
        CostTable costs,
        Garage garage,
        IEnumerable<int> targets,
        IEnumerable<int> consumed,
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

        bool Admit(int recipe, HashSet<int>? free = null)
        {
            if (candidates.Contains(recipe) || rejected.Contains(recipe)
                || !legality.IsLegal(index, recipe, garage))
            {
                return false;
            }
            if (!pinned.Contains(recipe) && !WithinCostBand(index, costs, recipe, free))
            {
                rejected.Add(recipe);
                return false;
            }
            candidates.Add(recipe);
            return true;
        }

        // The supplied items are free to their consumers — the cost engine cannot price them,
        // but a consume target delivers them at no cost.
        var supplied = consumed.ToHashSet();
        var cone = new HashSet<int>();
        var pending = new Stack<int>();
        var downSeen = new HashSet<int>();
        var downPending = new Stack<int>(supplied);
        while (downPending.TryPop(out var item))
        {
            if (!downSeen.Add(item))
            {
                continue;
            }
            for (var c = index.ConsumerStart[item]; c < index.ConsumerStart[item + 1]; c++)
            {
                var recipe = index.ConsumerRecipe[c];
                if (!Admit(recipe, supplied))
                {
                    continue;
                }
                cone.Add(recipe);
                for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
                {
                    downPending.Push(index.OutputItem[o]);
                }
                var start = index.AlternativeStart[index.SlotStart[recipe]];
                var end = index.AlternativeStart[index.SlotStart[recipe + 1]];
                for (var a = start; a < end; a++)
                {
                    pending.Push(index.AlternativeItem[a]);
                }
            }
        }

        var seen = new HashSet<int>();
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
                if (!Admit(recipe))
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
        if (rejected.Count > 0)
        {
            warnings.Add(new FactoryWarning("routes_pruned", ""));
        }
        return ([.. candidates.Order()], cone);
    }

    /// <summary>Whether some output of the recipe prices within the band of its solved cost.
    /// Items in <paramref name="free"/> cost nothing here — supplied consume targets.</summary>
    private bool WithinCostBand(SolverIndex index, CostTable costs, int recipe, HashSet<int>? free = null)
    {
        for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
        {
            var item = index.OutputItem[o];
            if (!costs.TryCost(item, out var solved))
            {
                continue;
            }
            var candidate = free is null
                ? costSolver.Candidate(costs, recipe, item)
                : FreeAwareCandidate(index, costs, recipe, item, free);
            if (candidate <= PruneFactor * solved + PruneFloor)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The cost engine's candidate arithmetic with the free set priced at zero:
    /// every slot at its cheapest alternative over the recipe's expected yield of the item.</summary>
    private static double FreeAwareCandidate(
        SolverIndex index, CostTable costs, int recipe, int item, HashSet<int> free)
    {
        var total = 0.0;
        for (var slot = 0; slot < index.SlotCount(recipe); slot++)
        {
            var cheapest = double.PositiveInfinity;
            for (var alt = 0; alt < index.AlternativeCount(recipe, slot); alt++)
            {
                var a = index.AlternativeAt(recipe, slot, alt);
                var input = index.AlternativeItem[a];
                var stack = free.Contains(input)
                    ? 0
                    : costs.Cost(input) * index.AlternativeAmount[a];
                if (stack < cheapest)
                {
                    cheapest = stack;
                }
            }
            if (double.IsPositiveInfinity(cheapest) || double.IsNaN(cheapest))
            {
                return double.PositiveInfinity;
            }
            total += cheapest;
        }
        var yield = index.Yield(recipe, item);
        return yield > 0 ? total / yield : double.PositiveInfinity;
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
        IReadOnlyList<GeneratorVariant> Generators,
        IReadOnlyList<int> RowItems,
        IReadOnlyDictionary<string, double> Weights,
        HashSet<int> SeedItems,
        int EuRow,
        IReadOnlyList<int> BandRows,
        IReadOnlyList<int> PinnedColumns,
        IReadOnlyList<string> PinItems);

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

        // Steam blocks run LV-and-below recipes on steam instead of EU: two liters per EU at
        // the bronze rate over a doubled duration, high pressure doubling rate and speed
        // alike, so a run swallows the same four liters per recipe EU either way. Bonuses
        // shape steam use and speed exactly as they shape EU and duration.
        if (blocks is not null && index.Tier[recipe] <= 1 && index.Heat[recipe] < 0
            && data.EuT[recipe] > 0)
        {
            foreach (var block in blocks.OrderBy(b => b.ItemId, StringComparer.Ordinal))
            {
                if (!block.Steam || block.Era is not { } era || era > garage.DefaultTier)
                {
                    continue;
                }
                var effects = ResolveBonuses(block, coilTier, voltageTier: 0);
                var steamSeconds = durationTicks / TicksPerSecond
                    * (block.Tier == 2 ? 1.0 : 2.0) * effects.DurationFactor;
                var steamPerRun = SteamPerEu * data.EuT[recipe] * data.Amps[recipe] * durationTicks
                    * effects.EuFactor;
                foreach (var steamId in SteamFluidIds)
                {
                    if (!index.TryGetItem(steamId, out var steamItem))
                    {
                        continue;
                    }
                    variants.Add(new RunVariant(
                        recipe, block.ItemId, 0, effects.Parallels, steamSeconds, 0,
                        effects.Estimated, steamItem, steamPerRun));
                }
            }
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
        // Overclocking trades quadrupled power for halved time; a recipe drawing nothing
        // has no power to trade and runs at base speed only.
        if (euPerTick <= 0)
        {
            maxSteps = 0;
        }
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
        Targets targets,
        List<int> candidates,
        HashSet<int> cone,
        List<GeneratorVariant> generators,
        HashSet<int> seedItems,
        List<FactoryWarning> warnings)
    {
        var index = graph.Index;
        var resolvedWeights = leafWeights.Resolve(graph, weights);
        var (pinned, pinItems) = PinnedAway(index, request.Pins, candidates, warnings);
        var pinnedColumns = new List<int>();

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
                rows.Add(new LpRow(targets.Produce.GetValueOrDefault(item), double.PositiveInfinity));
                rowItems.Add(item);
            }
            return row;
        }

        foreach (var target in targets.Produce.Keys.Order())
        {
            RowOf(target);
        }

        // A consume target's balance is an equality: what the outside supplies, the plan must
        // actually absorb — dumped supply is not processing.
        foreach (var item in targets.Consume.Keys.Order())
        {
            var row = RowOf(item);
            rows[row] = new LpRow(0, 0);
        }

        // The EU balance: generators feed it, every machine's duty-cycled draw taxes it, and
        // the bound is the demanded net export. Band rows repeat the demand over generators
        // of sufficient voltage tier.
        var euRow = -1;
        var bandRows = new List<(int Tier, int Row)>();
        if (targets.EnergyEuT > 0)
        {
            euRow = rows.Count;
            rows.Add(new LpRow(targets.EnergyEuT, double.PositiveInfinity));
            rowItems.Add(-1);
            foreach (var (tier, rate) in targets.Bands)
            {
                bandRows.Add((tier, rows.Count));
                rows.Add(new LpRow(rate, double.PositiveInfinity));
                rowItems.Add(-1);
            }
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
                var variantEntries = entries;
                if ((euRow >= 0 && variant.EuPerRun > 0) || variant.SteamItem >= 0)
                {
                    var setEu = euRow >= 0 && variant.EuPerRun > 0;
                    if (setEu)
                    {
                        net[euRow] = -variant.EuPerRun / TicksPerSecond;
                    }
                    var steamRow = -1;
                    var priorSteam = 0.0;
                    var hadSteam = false;
                    if (variant.SteamItem >= 0)
                    {
                        // The recipe may already consume the same fluid as a real input.
                        steamRow = RowOf(variant.SteamItem);
                        hadSteam = net.TryGetValue(steamRow, out priorSteam);
                        net[steamRow] = (hadSteam ? priorSteam : 0) - variant.SteamPerRun;
                    }
                    variantEntries = Sorted(net);
                    if (setEu)
                    {
                        net.Remove(euRow);
                    }
                    if (steamRow >= 0)
                    {
                        if (hadSteam)
                        {
                            net[steamRow] = priorSteam;
                        }
                        else
                        {
                            net.Remove(steamRow);
                        }
                    }
                }
                if (upper == 0)
                {
                    pinnedColumns.Add(columns.Count);
                }
                columns.Add(new LpColumn(0, upper, variantEntries));
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

        // One column per generator line: the variable is machines running, feeding the EU
        // rows and drawing fuel from its item's balance.
        for (var g = 0; g < generators.Count; g++)
        {
            var generator = generators[g];
            var entries = new Dictionary<int, double>
            {
                [RowOf(generator.FuelItem)] = -generator.UnitsPerSecond,
                [euRow] = generator.NetEuT,
            };
            if (generator.CondensateItem >= 0)
            {
                var row = RowOf(generator.CondensateItem);
                entries[row] = entries.GetValueOrDefault(row)
                    + generator.UnitsPerSecond * generator.CondensatePerUnit;
            }
            foreach (var (tier, row) in bandRows)
            {
                if (generator.Tier >= tier)
                {
                    entries[row] = generator.NetEuT;
                }
            }
            columns.Add(new LpColumn(0, double.PositiveInfinity, Sorted(entries)));
            metas.Add(new ColumnMeta(ColumnKind.Generate, -1, -1, 0, g));
        }

        // One bounded supply variable per consume target; the pre-layer maximizes them.
        foreach (var (item, rate) in targets.Consume.OrderBy(pair => pair.Key))
        {
            columns.Add(new LpColumn(0, rate, [new LpEntry(rowOf[item], 1)]));
            metas.Add(new ColumnMeta(ColumnKind.Supply, -1, item, 0));
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
            BuildObjectives(
                index, request, metas, variants, columns, rowItems, resolvedWeights, cone, seedItems,
                targets.Consume.Count > 0),
            request.TimeLimitSeconds);
        return new Model(
            program, metas, variants, generators, rowItems, resolvedWeights, seedItems,
            euRow, [.. bandRows.Select(band => band.Row)], pinnedColumns, pinItems);
    }

    /// <summary>Recipes a pin forces to zero — every other candidate producing the pinned
    /// item deterministically — with the pinned item ids that removed at least one route.
    /// Chanced byproduct rows stay free, and a pin whose item is outside the closure is
    /// simply inactive.</summary>
    private static (HashSet<int> Recipes, List<string> PinItems) PinnedAway(
        SolverIndex index,
        IReadOnlyDictionary<string, string> pins,
        List<int> candidates,
        List<FactoryWarning> warnings)
    {
        var pinnedAway = new HashSet<int>();
        var pinItems = new List<string>();
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
            var before = pinnedAway.Count;
            foreach (var producer in producers)
            {
                if (producer != pin)
                {
                    pinnedAway.Add(producer);
                }
            }
            if (pinnedAway.Count > before)
            {
                pinItems.Add(itemId);
            }
        }
        return (pinnedAway, pinItems);
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

    /// <summary>The layer sequence: maximize supply first when consume targets exist, then
    /// the user's layers in priority order, then the hidden canonicalization layer minimizing
    /// total runs and purchases, which pins every variable the earlier layers left free so
    /// degenerate optima come back model-determined.</summary>
    private static List<LpObjective> BuildObjectives(
        SolverIndex index,
        FactoryRequest request,
        List<ColumnMeta> metas,
        List<RunVariant> variants,
        List<LpColumn> columns,
        List<int> rowItems,
        IReadOnlyDictionary<string, double> weights,
        HashSet<int> cone,
        HashSet<int> seedItems,
        bool hasConsume)
    {
        var priority = request.Priority.Count > 0
            ? request.Priority.Distinct().ToList()
            : [FactoryObjective.Resource, FactoryObjective.Energy, FactoryObjective.Machines];

        var objectives = new List<LpObjective>();
        if (hasConsume)
        {
            var supply = new List<LpEntry>();
            for (var c = 0; c < metas.Count; c++)
            {
                if (metas[c].Kind == ColumnKind.Supply)
                {
                    supply.Add(new LpEntry(c, 1));
                }
            }
            // The intake is a hard commitment, not a preference: its lock row is one unit
            // entry per target, numerically safe to hold tight.
            objectives.Add(new LpObjective(Maximize: true, supply, AbsTolerance: 1e-9, RelTolerance: 0));
        }

        foreach (var objective in priority)
        {
            var coefficients = new List<LpEntry>();
            for (var c = 0; c < metas.Count; c++)
            {
                var meta = metas[c];
                var coefficient = objective switch
                {
                    // Auto-infinite seeds buy at weight zero: the world refills them.
                    FactoryObjective.Resource when meta.Kind == ColumnKind.Buy
                        && !seedItems.Contains(meta.Item) =>
                        weights.GetValueOrDefault(index.ItemIds[meta.Item], 1),
                    FactoryObjective.Energy when meta.Kind == ColumnKind.Run =>
                        // Generators are excluded: their cost is fuel, priced by the resource
                        // layer through the fuel chain. Steam draw counts at its EU content.
                        (variants[meta.Variant].EuPerRun
                            + variants[meta.Variant].SteamPerRun * EuPerSteamLiter) / 1000,
                    FactoryObjective.Machines when meta.Kind == ColumnKind.Run =>
                        variants[meta.Variant].DurationSeconds / variants[meta.Variant].Parallels,
                    FactoryObjective.Machines when meta.Kind == ColumnKind.Generate => 1.0,
                    _ => 0.0,
                };
                if (coefficient != 0)
                {
                    coefficients.Add(new LpEntry(c, coefficient));
                }
            }
            objectives.Add(new LpObjective(Maximize: false, coefficients, RelTolerance: LayerTolerance));

            // A maximize-recovered-value layer was designed to follow the resource lock for
            // consume factories, and three formulations of it measured unbounded on the real
            // artifact: the leaf weights are not arbitrage-free and free world-origin chains
            // exist by design, so any open value-maximize finds a mint. Byproducts still flow
            // and surface as surplus; an actively value-seeking route choice awaits a weight
            // model that can support it.
        }

        var canonical = new List<LpEntry>();
        for (var c = 0; c < metas.Count; c++)
        {
            // Supplies stay out: canonicalization must not trade the maximized intake away.
            if (metas[c].Kind is not ColumnKind.Split and not ColumnKind.Supply)
            {
                canonical.Add(new LpEntry(c, 1));
            }
        }
        objectives.Add(new LpObjective(
            Maximize: false, canonical, RelTolerance: LayerTolerance, SupportRestricted: true));
        return objectives;
    }

    /// <summary>Seed ids resolved to positions; MOB seeds only when the toggle admits them.</summary>
    private static HashSet<int> SeedItems(SolverIndex index, FactorySeedData seeds, bool mobFarms)
    {
        var items = new HashSet<int>();
        foreach (var (itemId, kind) in seeds.Kinds)
        {
            if ((mobFarms || kind != FactorySeedData.MobKind) && index.TryGetItem(itemId, out var item))
            {
                items.Add(item);
            }
        }
        return items;
    }

    /// <summary>The monotone fixpoint over the garage-legal recipes: an item is auto-infinite
    /// when it is a seed or some legal recipe covers every slot with an auto-infinite
    /// alternative. Catalysts and EU count as free — the index carries neither as a slot, so
    /// a zero-slot recipe qualifies outright.</summary>
    private bool[] AutoInfinite(SolverIndex index, Garage garage, HashSet<int> seedItems)
    {
        var infinite = new bool[index.ItemCount];
        var remaining = new int[index.RecipeCount];
        var satisfied = new bool[index.SlotStart[index.RecipeCount]];
        var queue = new Queue<int>();

        void Reach(int item)
        {
            if (!infinite[item])
            {
                infinite[item] = true;
                queue.Enqueue(item);
            }
        }

        void Qualify(int recipe)
        {
            for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
            {
                Reach(index.OutputItem[o]);
            }
        }

        for (var recipe = 0; recipe < index.RecipeCount; recipe++)
        {
            if (!legality.IsLegal(index, recipe, garage))
            {
                remaining[recipe] = -1;
                continue;
            }
            remaining[recipe] = index.SlotCount(recipe);
            if (remaining[recipe] == 0)
            {
                Qualify(recipe);
            }
        }
        foreach (var seed in seedItems)
        {
            Reach(seed);
        }

        while (queue.TryDequeue(out var item))
        {
            for (var c = index.ConsumerStart[item]; c < index.ConsumerStart[item + 1]; c++)
            {
                var recipe = index.ConsumerRecipe[c];
                if (remaining[recipe] <= 0)
                {
                    continue;
                }
                for (var slot = 0; slot < index.SlotCount(recipe) && remaining[recipe] > 0; slot++)
                {
                    var position = index.SlotStart[recipe] + slot;
                    if (satisfied[position] || !SlotHolds(index, recipe, slot, item))
                    {
                        continue;
                    }
                    satisfied[position] = true;
                    if (--remaining[recipe] == 0)
                    {
                        Qualify(recipe);
                    }
                }
            }
        }
        return infinite;
    }

    private static bool SlotHolds(SolverIndex index, int recipe, int slot, int item)
    {
        for (var alt = 0; alt < index.AlternativeCount(recipe, slot); alt++)
        {
            if (index.AlternativeItem[index.AlternativeAt(recipe, slot, alt)] == item)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The weight the resource layer actually charged the purchase — zero for
    /// auto-infinite seeds.</summary>
    private static double ChargedWeight(SolverIndex index, Model model, int item)
    {
        return model.SeedItems.Contains(item)
            ? 0
            : model.Weights.GetValueOrDefault(index.ItemIds[item], 1);
    }

    /// <summary>Never a bare infeasibility: first asks whether lifting the pins alone makes
    /// the model feasible, then re-solves with slack on every demand row — the rows whose
    /// slack the minimum keeps are what the garage cannot deliver.</summary>
    private void Diagnose(SolverIndex index, Model model, List<FactoryWarning> warnings)
    {
        if (model.PinnedColumns.Count > 0 && SolvesWithoutPins(model))
        {
            foreach (var itemId in model.PinItems)
            {
                warnings.Add(new FactoryWarning("pin_conflict", itemId));
            }
            return;
        }
        Elastic(index, model, warnings);
    }

    /// <summary>Whether the model turns feasible once every pinned-away column is freed.</summary>
    private bool SolvesWithoutPins(Model model)
    {
        var columns = new List<LpColumn>(model.Program.Columns);
        foreach (var column in model.PinnedColumns)
        {
            columns[column] = columns[column] with { Upper = double.PositiveInfinity };
        }
        var probe = new LinearProgram(
            columns, model.Program.Rows, [new LpObjective(Maximize: false, [])],
            model.Program.TimeLimitSeconds);
        return solver.Solve(probe).Status == LpSolveStatus.Optimal;
    }

    /// <summary>The elastic re-solve: every demand row gets a shortfall slack — and an excess
    /// slack where the row is bounded above — and minimizing total slack keeps nonzero only
    /// the rows the model cannot satisfy, each named in a warning.</summary>
    private void Elastic(SolverIndex index, Model model, List<FactoryWarning> warnings)
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

        var bandRows = model.BandRows.ToHashSet();
        for (var row = 0; row < program.Rows.Count; row++)
        {
            if (model.RowItems[row] < 0 && row != model.EuRow && !bandRows.Contains(row))
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
            columns, program.Rows, [new LpObjective(Maximize: false, coefficients)],
            program.TimeLimitSeconds);
        var result = solver.Solve(elastic);
        if (result.Status != LpSolveStatus.Optimal)
        {
            warnings.Add(new FactoryWarning("infeasible", ""));
            return;
        }

        var named = new HashSet<string>();
        for (var s = 0; s < slackRows.Count; s++)
        {
            if (result.ColumnValues[program.Columns.Count + s] <= RateEpsilon)
            {
                continue;
            }
            var item = model.RowItems[slackRows[s]];
            var warning = item >= 0
                ? new FactoryWarning("infeasible_item", index.ItemIds[item])
                : new FactoryWarning("infeasible_energy", "");
            if (named.Add(warning.Kind + " " + warning.ItemId))
            {
                warnings.Add(warning);
            }
        }
        if (named.Count == 0)
        {
            warnings.Add(new FactoryWarning("infeasible", ""));
        }
    }

    private static List<LpEntry> Sorted(Dictionary<int, double> entries)
    {
        return [.. entries.OrderBy(entry => entry.Key).Select(entry => new LpEntry(entry.Key, entry.Value))];
    }

    private static FactoryPlan Interpret(
        SolverIndex index,
        Model model,
        Targets targets,
        IReadOnlyList<double> values,
        List<FactoryWarning> warnings,
        bool[] infinite)
    {
        var produced = new Dictionary<int, double>();
        var consumed = new Dictionary<int, double>();
        var bought = new Dictionary<int, double>();
        var supplied = new Dictionary<int, double>();
        var lines = new List<FactoryLine>();
        var cost = 0.0;
        var drawEuT = 0.0;
        var exportEuT = 0.0;
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
                    if (variant.SteamItem >= 0)
                    {
                        consumed[variant.SteamItem] =
                            consumed.GetValueOrDefault(variant.SteamItem) + variant.SteamPerRun * value;
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
                case ColumnKind.Generate:
                    var generator = model.Generators[meta.Variant];
                    consumed[generator.FuelItem] =
                        consumed.GetValueOrDefault(generator.FuelItem) + generator.UnitsPerSecond * value;
                    if (generator.CondensateItem >= 0)
                    {
                        produced[generator.CondensateItem] = produced.GetValueOrDefault(generator.CondensateItem)
                            + generator.UnitsPerSecond * generator.CondensatePerUnit * value;
                    }
                    exportEuT += generator.NetEuT * value;
                    busyMachines += value;
                    // Item ids contain '~', so the synthetic id uses '|' as its separator.
                    var lineId = generator.RotorItemId is { } rotor
                        ? $"generator|{generator.BlockItemId}|{generator.FuelItemId}|{rotor}|{(generator.Loose ? "loose" : "tight")}"
                        : $"generator|{generator.BlockItemId}|{generator.FuelItemId}";
                    lines.Add(new FactoryLine(
                        lineId,
                        generator.Map,
                        generator.BlockItemId,
                        value,
                        0,
                        1,
                        value,
                        Durationless: false,
                        Estimated: false));
                    break;
                case ColumnKind.Supply:
                    supplied[meta.Item] = supplied.GetValueOrDefault(meta.Item) + value;
                    break;
                case ColumnKind.Buy:
                    bought[meta.Item] = bought.GetValueOrDefault(meta.Item) + value;
                    cost += value * ChargedWeight(index, model, meta.Item);
                    break;
            }
        }

        foreach (var (item, rate) in targets.Consume.OrderBy(pair => pair.Key))
        {
            var achieved = supplied.GetValueOrDefault(item);
            if (achieved < rate - Math.Max(RateEpsilon, LayerTolerance * rate))
            {
                warnings.Add(new FactoryWarning("consume_shortfall", index.ItemIds[item]));
            }
        }

        var flows = new List<FactoryItemFlow>();
        var inflows = new List<FactoryInflow>();
        foreach (var item in produced.Keys.Union(consumed.Keys).Union(bought.Keys).Union(supplied.Keys).Order())
        {
            var made = produced.GetValueOrDefault(item);
            var used = consumed.GetValueOrDefault(item);
            var buy = bought.GetValueOrDefault(item);
            var surplus = Math.Max(0, made + buy - used - targets.Produce.GetValueOrDefault(item));
            var supply = supplied.GetValueOrDefault(item);
            if (made > RateEpsilon || used > RateEpsilon || supply > RateEpsilon)
            {
                flows.Add(new FactoryItemFlow(
                    index.ItemIds[item], made, used, surplus, supply, infinite[item]));
            }
            if (buy > RateEpsilon)
            {
                inflows.Add(new FactoryInflow(
                    index.ItemIds[item], buy, ChargedWeight(index, model, item), infinite[item]));
            }
        }

        return new FactoryPlan(
            FactoryPlanStatus.Solved, lines, flows, inflows, warnings, cost, drawEuT, exportEuT, busyMachines);
    }
}
