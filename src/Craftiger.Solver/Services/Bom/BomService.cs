using Craftiger.Solver.Interfaces.Bom;
using Craftiger.Solver.Interfaces.Costs;
using Craftiger.Solver.Models.Bom;
using Craftiger.Solver.Models.Costs;
using Craftiger.Solver.Models.Graph;
using Craftiger.Solver.Models.Options;
using Microsoft.Extensions.Options;

namespace Craftiger.Solver.Services.Bom;

public sealed class BomService(
    IGarageLegalityService legality,
    IChosenEdgeGraphService graph,
    ILoopSeedService loopSeeds,
    IOptions<BomOptions> options) : IBomService
{
    private readonly BomOptions _options = options.Value;

    /// <summary>Walks the chosen-edge graph consumers before producers in two accountings — expected values and whole runs — solving each loop as one system with one seeded unit.</summary>
    public BomResult Compute(
        SolverGraph solverGraph, CostTable costs, Garage garage,
        IReadOnlyList<BomTarget> targets, IReadOnlyDictionary<string, string> pins)
    {
        if (!ReferenceEquals(solverGraph.Index, costs.Index))
        {
            throw new ArgumentException("the cost table was solved on a different graph", nameof(costs));
        }
        var index = costs.Index;
        var items = new BomItems(index);
        var warnings = new List<BomWarning>();
        var activePins = ValidatePins(index, garage, pins, warnings);
        var roots = targets.Select(target => target.ItemId).Distinct().Select(items.PositionOf).ToList();

        // A seed hangs its route off the loop it starts, so the walk reruns until every loop has been offered one.
        var seeds = new Dictionary<int, LoopSeed?>();
        IReadOnlyList<BomComponent> components;
        while (true)
        {
            var (walked, cyclePin) = graph.Walk(items, costs, activePins, roots, SeedsByItem(seeds));
            if (cyclePin is { } pinned)
            {
                activePins = activePins.Without(pinned);
                warnings.Add(BomWarning.PinCycle(index.ItemIds[pinned]));
                continue;
            }
            var seeded = false;
            foreach (var component in walked.Where(component => component.Loop))
            {
                if (seeds.ContainsKey(component.Key))
                {
                    continue;
                }
                var seed = loopSeeds.Seed(items, costs, garage, activePins, SeedsByItem(seeds), component.Items);
                seeds[component.Key] = seed;
                seeded |= seed is not null;
            }
            if (!seeded)
            {
                components = walked;
                break;
            }
        }

        var demand = new Dictionary<int, double>();
        var wholeDemand = new Dictionary<int, long>();
        foreach (var target in targets)
        {
            var item = items.PositionOf(target.ItemId);
            demand[item] = demand.GetValueOrDefault(item) + target.Count;
            wholeDemand[item] = wholeDemand.GetValueOrDefault(item) + target.Count;
        }

        var walk = new BomWalk(items, costs, activePins, roots.ToHashSet(), demand, wholeDemand, warnings);
        foreach (var component in components)
        {
            if (component.Loop)
            {
                ExpandLoop(walk, component, seeds[component.Key]);
            }
            else
            {
                ExpandItem(walk, component.Items[0]);
            }
        }

        return new BomResult(
            targets.Select(target => TargetResult(costs, activePins, target)).ToList(),
            walk.Leaves.Select(leaf => new BomLeaf(walk.IdOf(leaf.Key), leaf.Value.Amount, leaf.Value.Whole)).ToList(),
            warnings,
            walk.Nodes);
    }

    private static Dictionary<int, LoopSeed> SeedsByItem(Dictionary<int, LoopSeed?> seeds) =>
        seeds.Values
            .Where(seed => seed is not null)
            .ToDictionary(seed => seed!.Item, seed => seed!);

    private static void ExpandItem(BomWalk walk, int item)
    {
        var demanded = walk.Demanded(item);
        if (demanded <= 0)
        {
            return;
        }
        var wholeDemanded = walk.WholeDemanded(item);
        var index = walk.Items.Index;
        if (walk.Items.IsLeaf(item))
        {
            walk.AddLeaf(item, demanded, wholeDemanded);
            return;
        }

        var recipe = walk.Pins.Chosen(walk.Costs, item);
        if (recipe < 0)
        {
            walk.Warn(walk.Roots.Contains(item)
                ? BomWarning.UnreachableTarget(walk.IdOf(item))
                : BomWarning.UnreachableInput(walk.IdOf(item)));
            return;
        }

        var yield = index.Yield(recipe, item);
        var runs = demanded / yield;
        var wholeRuns = WholeRuns(wholeDemanded, yield);
        var picks = walk.Costs.PicksFor(item, recipe);
        walk.AddNode(new BomNode(
            walk.IdOf(item), demanded, runs, wholeDemanded, wholeRuns, index.RecipeIds[recipe],
            walk.Stacks(recipe, picks), Loop: null, Seed: false));
        for (var s = 0; s < picks.Length; s++)
        {
            var at = index.AlternativeAt(recipe, s, picks[s]);
            walk.Add(index.AlternativeItem[at], runs * index.AlternativeAmount[at], wholeRuns * index.AlternativeAmount[at]);
        }
    }

    /// <summary>A loop's demands as one system — fractional by elimination, whole runs by iterating the same equations on integers — with the seed's one unit already supplied.</summary>
    private void ExpandLoop(BomWalk walk, BomComponent component, LoopSeed? seed)
    {
        var index = walk.Items.Index;
        var system = LoopSystem.Analyze(walk.Costs, walk.Pins, component.Items);
        var members = component.Items;
        var count = members.Count;
        var external = members.Select(walk.Demanded).ToArray();
        if (external.All(amount => amount <= 0))
        {
            return;
        }
        var externalWhole = members.Select(walk.WholeDemanded).ToArray();
        var supply = new double[count];
        if (seed is not null)
        {
            supply[system.Row[seed.Item]] = 1;
        }

        var totals = system.SolveSupplied(external, supply, _options.PivotEpsilon);
        var runs = new double[count];
        for (var i = 0; i < count; i++)
        {
            runs[i] = totals[i] / system.Yields[i];
        }

        var wholeTotals = new long[count];
        for (var i = 0; i < count; i++)
        {
            wholeTotals[i] = Math.Max(0, externalWhole[i] - (long)supply[i]);
        }
        var wholeRuns = new long[count];
        for (var round = 0; round < _options.MaxWholeRounds; round++)
        {
            for (var i = 0; i < count; i++)
            {
                wholeRuns[i] = WholeRuns(wholeTotals[i], system.Yields[i]);
            }
            var next = new long[count];
            for (var i = 0; i < count; i++)
            {
                next[i] = externalWhole[i] - (long)supply[i];
            }
            for (var j = 0; j < count; j++)
            {
                var picks = system.Picks[j];
                for (var s = 0; s < picks.Length; s++)
                {
                    var at = index.AlternativeAt(system.Recipes[j], s, picks[s]);
                    if (system.Row.TryGetValue(index.AlternativeItem[at], out var i))
                    {
                        next[i] += wholeRuns[j] * index.AlternativeAmount[at];
                    }
                }
            }
            for (var i = 0; i < count; i++)
            {
                next[i] = Math.Max(0, next[i]);
            }
            if (next.SequenceEqual(wholeTotals))
            {
                break;
            }
            wholeTotals = next;
        }

        var loop = walk.NextLoop();
        for (var i = 0; i < count; i++)
        {
            if (runs[i] <= 0 && wholeRuns[i] <= 0)
            {
                continue;
            }
            var recipe = system.Recipes[i];
            var picks = system.Picks[i];
            walk.AddNode(new BomNode(
                walk.IdOf(members[i]), totals[i], runs[i], wholeTotals[i], wholeRuns[i], index.RecipeIds[recipe],
                walk.Stacks(recipe, picks), Loop: loop, Seed: false));
            for (var s = 0; s < picks.Length; s++)
            {
                var at = index.AlternativeAt(recipe, s, picks[s]);
                var input = index.AlternativeItem[at];
                if (!system.Row.ContainsKey(input))
                {
                    walk.Add(input, runs[i] * index.AlternativeAmount[at], wholeRuns[i] * index.AlternativeAmount[at]);
                }
            }
        }

        if (seed is null)
        {
            walk.Warn(BomWarning.LoopUnseeded(walk.IdOf(members[0])));
            return;
        }
        var seedYield = index.Yield(seed.Recipe, seed.Item);
        var seedRuns = 1 / seedYield;
        var seedWholeRuns = WholeRuns(1, seedYield);
        walk.AddNode(new BomNode(
            walk.IdOf(seed.Item), 1, seedRuns, 1, seedWholeRuns, index.RecipeIds[seed.Recipe],
            walk.Stacks(seed.Recipe, seed.Picks), Loop: loop, Seed: true));
        for (var s = 0; s < seed.Picks.Length; s++)
        {
            var at = index.AlternativeAt(seed.Recipe, s, seed.Picks[s]);
            walk.Add(index.AlternativeItem[at], seedRuns * index.AlternativeAmount[at], seedWholeRuns * index.AlternativeAmount[at]);
        }
    }

    private BomPins ValidatePins(
        SolverIndex index, Garage garage, IReadOnlyDictionary<string, string> pins, List<BomWarning> warnings)
    {
        var active = new Dictionary<int, int>();
        foreach (var (itemId, recipeId) in pins)
        {
            if (!index.TryGetRecipe(recipeId, out var recipe)
                || !index.TryGetItem(itemId, out var item)
                || index.Yield(recipe, item) <= 0)
            {
                warnings.Add(BomWarning.PinUnknown(itemId));
                continue;
            }
            if (!legality.IsLegal(index, recipe, garage))
            {
                warnings.Add(BomWarning.PinIllegal(itemId));
                continue;
            }
            active[item] = recipe;
        }
        return new BomPins(active);
    }

    /// <summary>The fewest whole runs whose expected yield covers the demand; the tolerance keeps an exactly-divisible demand from rounding one run too high.</summary>
    private static long WholeRuns(long demanded, double yield) =>
        (long)Math.Ceiling(demanded / yield - 1e-9);

    private static BomTargetResult TargetResult(CostTable costs, BomPins pins, BomTarget target)
    {
        var index = costs.Index;
        if (!index.TryGetItem(target.ItemId, out var item) || index.IsLeaf(item))
        {
            return new BomTargetResult(target.ItemId, target.Count, null, []);
        }
        var recipe = pins.Chosen(costs, item);
        if (recipe < 0)
        {
            return new BomTargetResult(target.ItemId, target.Count, null, []);
        }

        var runs = target.Count / index.Yield(recipe, item);
        var inputs = new Dictionary<string, double>();
        var picks = costs.PicksFor(item, recipe);
        for (var s = 0; s < picks.Length; s++)
        {
            var at = index.AlternativeAt(recipe, s, picks[s]);
            var id = index.ItemIds[index.AlternativeItem[at]];
            inputs[id] = inputs.GetValueOrDefault(id) + runs * index.AlternativeAmount[at];
        }
        return new BomTargetResult(
            target.ItemId, target.Count, index.RecipeIds[recipe],
            inputs.Select(input => new BomStack(input.Key, input.Value)).ToList());
    }
}
