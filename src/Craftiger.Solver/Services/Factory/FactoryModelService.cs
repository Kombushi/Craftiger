using Craftiger.Solver.Interfaces.Costs;
using Craftiger.Solver.Interfaces.Factory;
using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;
using Craftiger.Solver.Models.Lp;
using Craftiger.Solver.Models.Options;
using Microsoft.Extensions.Options;

namespace Craftiger.Solver.Services.Factory;

public sealed class FactoryModelService(
    ILeafWeightService leafWeights,
    IRunVariantService runVariants,
    IOptions<FactorySolverOptions> options) : IFactoryModelService
{
    /// <summary>The intake lock is a hard commitment: one unit entry per target, numerically safe to hold tight.</summary>
    private const double SupplyLockTolerance = 1e-9;

    /// <summary>Energy coefficients are in kEU per run.</summary>
    private const double EuPerKiloEu = 1000.0;

    private readonly FactorySolverOptions _options = options.Value;

    /// <summary>Variables are runs per second per recipe variant, one split per choice-slot alternative, one purchase per leaf, one machine count per generator line, one bounded supply per consume target; balance rows keep every item's net non-negative with produce targets raising the floor.</summary>
    public FactoryModel Build(
        FactoryContext context,
        FactoryRequest request,
        FactoryTargets targets,
        CandidateSet candidates,
        IReadOnlyList<GeneratorLine> generators,
        IReadOnlySet<int> seedItems,
        ICollection<FactoryWarning> warnings)
    {
        var index = context.Index;
        var weights = leafWeights.Resolve(context.Graph, context.Weights);
        // A pipeline ignores pins: the steps are the pins, and a stale pin zeroing one of them would disable it silently.
        var (pinnedAway, pinItems) = request.IsPipeline
            ? (new HashSet<int>(), new List<string>())
            : PinnedAway(index, request.Pins, candidates, warnings);
        var stepOf = new Dictionary<int, FactoryStep>();
        foreach (var step in request.Steps ?? [])
        {
            if (index.TryGetRecipe(step.Id, out var stepRecipe))
            {
                stepOf[stepRecipe] = step;
            }
        }
        var assembly = new FactoryModelAssembly(targets);

        foreach (var target in targets.ProducedItems)
        {
            assembly.RowOf(target);
        }
        // A consume target's balance is an equality: dumped supply is not processing.
        foreach (var item in targets.ConsumedItems)
        {
            assembly.SetRow(assembly.RowOf(item), new LpRow(0, 0));
        }

        int? euRow = null;
        var bandRows = new List<(int Tier, int Row)>();
        if (targets.HasEnergy)
        {
            euRow = assembly.AddRow(new LpRow(targets.EnergyEuT, double.PositiveInfinity));
            foreach (var band in targets.Bands)
            {
                bandRows.Add((band.Tier, assembly.AddRow(new LpRow(band.Rate, double.PositiveInfinity))));
            }
        }

        foreach (var recipe in candidates.Candidates)
        {
            AddRecipe(
                context, assembly, recipe, euRow, pinnedAway.Contains(recipe),
                stepOf.GetValueOrDefault(recipe), warnings);
        }

        foreach (var line in generators)
        {
            var balance = new ItemBalance();
            balance.Add(assembly.RowOf(line.FuelItem), -line.UnitsPerSecond);
            balance.Add(euRow!.Value, line.NetEuT);
            if (line.CondensateItem is { } condensate)
            {
                balance.Add(assembly.RowOf(condensate), line.CondensatePerSecond);
            }
            foreach (var flow in line.Inputs)
            {
                balance.Add(assembly.RowOf(flow.Item), -flow.PerSecond);
            }
            foreach (var flow in line.Outputs)
            {
                balance.Add(assembly.RowOf(flow.Item), flow.PerSecond);
            }
            foreach (var (tier, row) in bandRows)
            {
                if (line.Tier >= tier)
                {
                    balance.Add(row, line.NetEuT);
                }
            }
            assembly.AddColumn(new LpColumn(0, double.PositiveInfinity, balance.Entries()), new GenerateColumn(line));
        }

        foreach (var (item, rate) in targets.Consume.OrderBy(pair => pair.Key))
        {
            assembly.TryGetRow(item, out var row);
            assembly.AddColumn(new LpColumn(0, rate, [new LpEntry(row, 1)]), new SupplyColumn(item));
        }

        // Purchase variables close every leaf's balance; consuming internal flow offsets them. A
        // pipeline instead supplies whatever no step makes — at its standing price — so a
        // half-built chain still solves and shows its open inputs, but never conjures a target.
        // Declared supplies buy free even where a step makes them; a produce target never buys.
        if (request.IsPipeline)
        {
            var supplied = new HashSet<int>();
            foreach (var id in (request.Supplies ?? []).Distinct().Order(StringComparer.Ordinal))
            {
                if (index.TryGetItem(id, out var supply))
                {
                    supplied.Add(supply);
                }
                else
                {
                    warnings.Add(FactoryWarning.SupplyUnknown(id));
                }
            }
            var made = new HashSet<int>();
            foreach (var recipe in candidates.Candidates)
            {
                for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
                {
                    made.Add(index.OutputItem[o]);
                }
            }
            foreach (var line in generators)
            {
                if (line.CondensateItem is { } condensate)
                {
                    made.Add(condensate);
                }
                foreach (var flow in line.Outputs)
                {
                    made.Add(flow.Item);
                }
            }
            foreach (var (item, row) in assembly.ItemRows)
            {
                if (!targets.Produce.ContainsKey(item) && (supplied.Contains(item) || !made.Contains(item)))
                {
                    assembly.AddColumn(new LpColumn(0, double.PositiveInfinity, [new LpEntry(row, 1)]), new BuyColumn(item));
                }
            }
            weights = StandingPrices(context, weights, assembly, supplied);
        }
        else
        {
            foreach (var (item, row) in assembly.ItemRows)
            {
                if (index.IsLeaf(item))
                {
                    assembly.AddColumn(new LpColumn(0, double.PositiveInfinity, [new LpEntry(row, 1)]), new BuyColumn(item));
                }
            }
        }

        var objectives = Objectives(context, request, targets, assembly.Meanings, weights, seedItems);
        return assembly.Freeze(
            objectives, request.TimeLimitSeconds, weights, seedItems, euRow,
            [.. bandRows.Select(band => band.Row)], pinItems);
    }

    /// <summary>One run column per variant sharing the recipe's balance — a step's pin narrows the variants — plus one link row and split columns per choice slot so the splits sum to the recipe's total runs.</summary>
    private void AddRecipe(
        FactoryContext context, FactoryModelAssembly assembly, int recipe, int? euRow, bool pinnedAway,
        FactoryStep? step, ICollection<FactoryWarning> warnings)
    {
        var index = context.Index;
        var balance = new ItemBalance();
        for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
        {
            balance.Add(assembly.RowOf(index.OutputItem[o]), index.OutputYield[o]);
        }

        var splits = new List<(int Slot, int LinkRow)>();
        for (var slot = 0; slot < index.SlotCount(recipe); slot++)
        {
            if (index.AlternativeCount(recipe, slot) == 1)
            {
                var a = index.AlternativeAt(recipe, slot, 0);
                balance.Add(assembly.RowOf(index.AlternativeItem[a]), -index.AlternativeAmount[a]);
            }
            else
            {
                var link = assembly.AddRow(new LpRow(0, 0));
                balance.Add(link, -1);
                splits.Add((slot, link));
            }
        }

        var upper = pinnedAway ? 0 : double.PositiveInfinity;
        var entries = balance.Entries();
        IReadOnlyList<RunVariant> variants = runVariants.Variants(context, recipe);
        if (step is { } pin && pin.PinsVariant)
        {
            var admitted = variants.Where(pin.Admits).ToList();
            if (admitted.Count > 0)
            {
                variants = admitted;
            }
            else
            {
                // A pin no buildable variant satisfies falls back to the free choice, visibly.
                warnings.Add(FactoryWarning.StepVariantUnknown(index.RecipeIds[recipe]));
            }
        }
        foreach (var variant in variants)
        {
            var variantEntries = entries;
            if ((euRow is not null && variant.DrawsEu) || variant.DrawsSteam || variant.ScalesOutputs)
            {
                var extras = new List<(int Row, double Delta)>();
                if (euRow is { } eu && variant.DrawsEu)
                {
                    extras.Add((eu, -variant.EuPerRun / Ticks.PerSecond));
                }
                if (variant.SteamItem is { } steamItem)
                {
                    // The recipe may already consume the same fluid as a real input.
                    extras.Add((assembly.RowOf(steamItem), -variant.SteamPerRun));
                }
                if (variant.ScalesOutputs)
                {
                    for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
                    {
                        extras.Add((assembly.RowOf(index.OutputItem[o]), index.OutputYield[o] * (variant.OutputFactor - 1)));
                    }
                }
                variantEntries = balance.Entries(extras);
            }
            assembly.AddColumn(new LpColumn(0, upper, variantEntries), new RunColumn(recipe, variant), pinnedAway);
        }

        foreach (var (slot, link) in splits)
        {
            for (var alt = 0; alt < index.AlternativeCount(recipe, slot); alt++)
            {
                var a = index.AlternativeAt(recipe, slot, alt);
                var item = index.AlternativeItem[a];
                var amount = index.AlternativeAmount[a];
                var split = new ItemBalance();
                split.Add(assembly.RowOf(item), -amount);
                split.Add(link, 1);
                assembly.AddColumn(new LpColumn(0, double.PositiveInfinity, split.Entries()), new SplitColumn(recipe, item, amount));
            }
        }
    }

    /// <summary>Every item a pipeline may buy, at the cost table's price — the leaf weight only where no garage-legal chain undercuts it, and the chain's own price where one does. Declared supplies charge nothing: the user's world provides them.</summary>
    private static IReadOnlyDictionary<string, double> StandingPrices(
        FactoryContext context,
        IReadOnlyDictionary<string, double> weights,
        FactoryModelAssembly assembly,
        IReadOnlySet<int> supplied)
    {
        var charges = new Dictionary<string, double>(weights);
        foreach (var (item, _) in assembly.ItemRows)
        {
            if (supplied.Contains(item))
            {
                charges[context.Index.ItemIds[item]] = 0;
            }
            else if (context.Costs.TryCost(item, out var cost))
            {
                charges[context.Index.ItemIds[item]] = cost;
            }
        }
        return charges;
    }

    /// <summary>Recipes a pin forces to zero — every other candidate deterministically producing the pinned item — with the pin items that removed at least one route; a pin outside the closure is inactive.</summary>
    private static (HashSet<int> Recipes, List<string> PinItems) PinnedAway(
        SolverIndex index, IReadOnlyDictionary<string, string> pins, CandidateSet candidates, ICollection<FactoryWarning> warnings)
    {
        var pinnedAway = new HashSet<int>();
        var pinItems = new List<string>();
        foreach (var (itemId, recipeId) in pins.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!index.TryGetItem(itemId, out var item))
            {
                continue;
            }
            var producers = candidates.Candidates.Where(recipe => index.ProducesDeterministically(recipe, item)).ToList();
            if (producers.Count == 0)
            {
                continue;
            }
            if (!index.TryGetRecipe(recipeId, out var pin))
            {
                warnings.Add(FactoryWarning.PinUnknown(itemId));
                continue;
            }
            if (!candidates.Contains(pin))
            {
                warnings.Add(FactoryWarning.PinIllegal(itemId));
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

    /// <summary>Maximize supply first for consume factories, then the user's layers, then the hidden canonicalization layer minimizing total runs and purchases so degenerate optima come back model-determined.</summary>
    private List<LpObjective> Objectives(
        FactoryContext context,
        FactoryRequest request,
        FactoryTargets targets,
        IReadOnlyList<FactoryColumn> columns,
        IReadOnlyDictionary<string, double> weights,
        IReadOnlySet<int> seedItems)
    {
        var index = context.Index;
        var objectives = new List<LpObjective>();
        if (targets.HasConsume)
        {
            var supply = new List<LpEntry>();
            for (var c = 0; c < columns.Count; c++)
            {
                if (columns[c] is SupplyColumn)
                {
                    supply.Add(new LpEntry(c, 1));
                }
            }
            objectives.Add(new LpObjective(Maximize: true, supply, AbsTolerance: SupplyLockTolerance, RelTolerance: 0));
        }

        foreach (var objective in request.Layers)
        {
            var coefficients = new List<LpEntry>();
            for (var c = 0; c < columns.Count; c++)
            {
                var coefficient = (objective, columns[c]) switch
                {
                    // Auto-infinite seeds buy at weight zero: the world refills them.
                    (FactoryObjective.Resource, BuyColumn buy) when !seedItems.Contains(buy.Item) =>
                        weights.GetValueOrDefault(index.ItemIds[buy.Item], 1),
                    // Generators are excluded: their cost is fuel, priced by the resource layer through the fuel chain.
                    (FactoryObjective.Energy, RunColumn run) => run.Variant.EnergyPerRun(context.Steam.EuPerLiter) / EuPerKiloEu,
                    (FactoryObjective.Machines, RunColumn run) => run.Variant.DurationSeconds / run.Variant.Parallels,
                    (FactoryObjective.Machines, GenerateColumn) => 1.0,
                    _ => 0.0,
                };
                if (coefficient != 0)
                {
                    coefficients.Add(new LpEntry(c, coefficient));
                }
            }
            objectives.Add(new LpObjective(Maximize: false, coefficients, RelTolerance: _options.LayerTolerance));
        }

        // Supplies stay out: canonicalization must not trade the maximized intake away.
        var canonical = new List<LpEntry>();
        for (var c = 0; c < columns.Count; c++)
        {
            if (columns[c] is not SplitColumn and not SupplyColumn)
            {
                canonical.Add(new LpEntry(c, 1));
            }
        }
        objectives.Add(new LpObjective(
            Maximize: false, canonical, RelTolerance: _options.LayerTolerance, SupportRestricted: true));
        return objectives;
    }
}
