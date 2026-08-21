using Craftiger.Solver.Interfaces;
using Craftiger.Solver.Models;

namespace Craftiger.Solver.Services;

public sealed class BomService(IGarageLegalityService legality) : IBomService
{
    /// <summary>A loop whose remaining gain is this close to one feeds itself for free and has
    /// no finite plan.</summary>
    private const double PivotEpsilon = 1e-12;

    /// <summary>The whole-run fixpoint of a loop is monotone and bounded, so this only guards
    /// against a broken system.</summary>
    private const int MaxWholeRounds = 100_000;

    /// <summary>Walks the chosen-edge graph with pins overlaid, consumers before producers, so
    /// shared intermediates accumulate their full demand before they are expanded. Each step
    /// carries two accountings: fractional expected values for pricing, and whole runs — the
    /// accumulated demand rounded up once per item — for a plan a machine can execute. Recipes
    /// that feed each other while consuming something from outside form a loop (§6): its demands
    /// solve as one linear system, and one unit of a loop item is added through the cheapest
    /// producer outside the loop to start it. Leaves never expand, whatever their bestRecipe
    /// says. A pin that closes a loop with no finite plan is ignored with a warning (§9). The
    /// walk runs on index positions; ids only cross the boundary in and out.</summary>
    public BomResult Compute(
        SolverGraph graph, CostTable costs, Garage garage,
        IReadOnlyList<BomTarget> targets, IReadOnlyDictionary<string, string> pins)
    {
        if (!ReferenceEquals(graph.Index, costs.Index))
        {
            throw new ArgumentException("the cost table was solved on a different graph", nameof(costs));
        }
        var index = costs.Index;
        var warnings = new List<BomWarning>();
        var activePins = ValidatePins(index, garage, pins, warnings);

        // A target the index never saw has no recipe and is no leaf; it gets a position past
        // the index so it walks — and warns — exactly like any other unproducible item.
        var extraIds = new List<string>();
        var roots = new List<int>();
        foreach (var id in targets.Select(target => target.ItemId).Distinct())
        {
            roots.Add(PositionOf(index, extraIds, id));
        }

        // A seed hangs its route off the loop it starts, so the walk is rerun until every loop
        // has been offered one and the seeds' own subtrees are ordered after their loops.
        var seeds = new Dictionary<int, LoopSeed?>();
        List<BomComponent> components;
        while (true)
        {
            var (walked, cyclePin) = Walk(index, costs, activePins, roots, SeedsByItem(seeds), extraIds);
            if (cyclePin is { } pinned)
            {
                activePins.Remove(pinned);
                warnings.Add(new BomWarning("pin_cycle", index.ItemIds[pinned]));
                continue;
            }
            var seeded = false;
            foreach (var component in walked.Where(component => component.Loop))
            {
                var key = LoopKey(component.Items);
                if (seeds.ContainsKey(key))
                {
                    continue;
                }
                var seed = Seed(index, costs, garage, activePins, SeedsByItem(seeds), component.Items);
                seeds[key] = seed;
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
            var item = PositionOf(index, extraIds, target.ItemId);
            demand[item] = demand.GetValueOrDefault(item) + target.Count;
            wholeDemand[item] = wholeDemand.GetValueOrDefault(item) + target.Count;
        }

        var walk = new BomWalkState(
            index, costs, garage, activePins, roots.ToHashSet(), demand, wholeDemand, warnings, extraIds);
        foreach (var component in components)
        {
            if (component.Loop)
            {
                ExpandLoop(walk, component, seeds[LoopKey(component.Items)]);
            }
            else
            {
                ExpandItem(walk, component.Items[0]);
            }
        }

        return new BomResult(
            targets.Select(target => TargetResult(index, costs, activePins, target)).ToList(),
            walk.Leaves.Select(leaf => new BomLeaf(walk.IdOf(leaf.Key), leaf.Value.Amount, leaf.Value.Whole)).ToList(),
            warnings,
            walk.Nodes);
    }

    /// <summary>The index position of an id, or a fresh position past the index for an id it
    /// does not know — assigned once per id, so the same unknown target keeps one position.</summary>
    private static int PositionOf(SolverIndex index, List<string> extraIds, string itemId)
    {
        if (index.TryGetItem(itemId, out var item))
        {
            return item;
        }
        var extra = extraIds.IndexOf(itemId);
        if (extra < 0)
        {
            extra = extraIds.Count;
            extraIds.Add(itemId);
        }
        return index.ItemCount + extra;
    }

    /// <summary>Loops keep their identity across walks by their smallest member position.</summary>
    private static int LoopKey(IEnumerable<int> members) => members.Min();

    private static Dictionary<int, LoopSeed> SeedsByItem(Dictionary<int, LoopSeed?> seeds) =>
        seeds.Values
            .Where(seed => seed is not null)
            .ToDictionary(seed => seed!.Item, seed => seed!);

    private static void ExpandItem(BomWalkState walk, int item)
    {
        var demanded = walk.Demand.GetValueOrDefault(item);
        if (demanded <= 0)
        {
            return;
        }
        var wholeDemanded = walk.WholeDemand.GetValueOrDefault(item);
        var index = walk.Index;
        if (item < index.ItemCount && index.IsLeaf(item))
        {
            var (amount, whole) = walk.Leaves.GetValueOrDefault(item);
            walk.Leaves[item] = (amount + demanded, whole + wholeDemanded);
            return;
        }

        var recipe = Chosen(index, walk.Pins, walk.Costs, item);
        if (recipe < 0)
        {
            walk.Warnings.Add(new BomWarning(
                walk.Roots.Contains(item) ? "unreachable_target" : "unreachable_input", walk.IdOf(item)));
            return;
        }

        var yield = index.Yield(recipe, item);
        var runs = demanded / yield;
        var wholeRuns = WholeRuns(wholeDemanded, yield);
        var picks = SlotChoice.Picks(walk.Costs, item, recipe);
        walk.Nodes.Add(new BomNode(
            walk.IdOf(item), demanded, runs, wholeDemanded, wholeRuns, index.RecipeIds[recipe],
            Stacks(walk, recipe, picks), Loop: null, Seed: false));
        for (var s = 0; s < picks.Length; s++)
        {
            var at = index.AlternativeAt(recipe, s, picks[s]);
            walk.Add(index.AlternativeItem[at], runs * index.AlternativeAmount[at], wholeRuns * index.AlternativeAmount[at]);
        }
    }

    /// <summary>Solves a loop's demands as one system — fractional by elimination, whole runs
    /// by iterating the same equations on integers until they settle — with the seed's one
    /// unit already supplied: the loop only makes what the outside demand and its own feeding
    /// need beyond that, so a single unit is just the seed route.</summary>
    private static void ExpandLoop(BomWalkState walk, BomComponent component, LoopSeed? seed)
    {
        var index = walk.Index;
        var system = AnalyzeLoop(index, walk.Costs, walk.Pins, component.Items);
        var members = component.Items;
        var count = members.Count;
        var external = members.Select(item => walk.Demand.GetValueOrDefault(item)).ToArray();
        if (external.All(amount => amount <= 0))
        {
            return;
        }
        var externalWhole = members.Select(item => walk.WholeDemand.GetValueOrDefault(item)).ToArray();
        var seedIndex = seed is null ? -1 : system.Row[seed.Item];
        var supply = new double[count];
        if (seedIndex >= 0)
        {
            supply[seedIndex] = 1;
        }

        var totals = SolveSupplied(system.Gain, external, supply);
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
        for (var round = 0; round < MaxWholeRounds; round++)
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

        var loop = walk.Loops++;
        for (var i = 0; i < count; i++)
        {
            if (runs[i] <= 0 && wholeRuns[i] <= 0)
            {
                continue;
            }
            var recipe = system.Recipes[i];
            var picks = system.Picks[i];
            walk.Nodes.Add(new BomNode(
                walk.IdOf(members[i]), totals[i], runs[i], wholeTotals[i], wholeRuns[i], index.RecipeIds[recipe],
                Stacks(walk, recipe, picks), Loop: loop, Seed: false));
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
            walk.Warnings.Add(new BomWarning("loop_unseeded", walk.IdOf(members[0])));
            return;
        }
        var seedYield = index.Yield(seed.Recipe, seed.Item);
        var seedRuns = 1 / seedYield;
        var seedWholeRuns = WholeRuns(1, seedYield);
        walk.Nodes.Add(new BomNode(
            walk.IdOf(seed.Item), 1, seedRuns, 1, seedWholeRuns, index.RecipeIds[seed.Recipe],
            Stacks(walk, seed.Recipe, seed.Picks), Loop: loop, Seed: true));
        for (var s = 0; s < seed.Picks.Length; s++)
        {
            var at = index.AlternativeAt(seed.Recipe, s, seed.Picks[s]);
            walk.Add(index.AlternativeItem[at], seedRuns * index.AlternativeAmount[at], seedWholeRuns * index.AlternativeAmount[at]);
        }
    }

    /// <summary>The recipe's input stacks under the given picks, by id, for a chain node.</summary>
    private static List<BomStack> Stacks(BomWalkState walk, int recipe, int[] picks)
    {
        var index = walk.Index;
        var stacks = new List<BomStack>(picks.Length);
        for (var s = 0; s < picks.Length; s++)
        {
            var at = index.AlternativeAt(recipe, s, picks[s]);
            stacks.Add(new BomStack(walk.IdOf(index.AlternativeItem[at]), index.AlternativeAmount[at]));
        }
        return stacks;
    }

    /// <summary>The cheapest garage-legal producer of any loop item that does not itself draw on
    /// the loop — the route a player takes once to get the first unit.</summary>
    private LoopSeed? Seed(
        SolverIndex index, CostTable costs, Garage garage, Dictionary<int, int> pins,
        Dictionary<int, LoopSeed> seeds, List<int> members)
    {
        var memberSet = members.ToHashSet();
        LoopSeed? best = null;
        var bestCost = double.PositiveInfinity;
        foreach (var item in members)
        {
            var chosen = Chosen(index, pins, costs, item);
            for (var p = index.ProducerStart[item]; p < index.ProducerStart[item + 1]; p++)
            {
                var producer = index.ProducerRecipe[p];
                if (producer == chosen || !legality.IsLegal(index, producer, garage))
                {
                    continue;
                }
                var picks = SlotChoice.Picks(costs, item, producer);
                var total = 0.0;
                for (var s = 0; s < picks.Length; s++)
                {
                    var at = index.AlternativeAt(producer, s, picks[s]);
                    total += costs.TryCost(index.AlternativeItem[at], out var unit)
                        ? unit * index.AlternativeAmount[at]
                        : double.PositiveInfinity;
                }
                var cost = total / index.Yield(producer, item);
                if (!(cost < bestCost)
                    || Reaches(index, costs, pins, seeds, PickedItems(index, producer, picks), memberSet))
                {
                    continue;
                }
                best = new LoopSeed(item, producer, picks);
                bestCost = cost;
            }
        }
        return best;
    }

    /// <summary>Whether any of the items reaches a loop member over chosen edges — a seed that
    /// draws on the loop it should start is no seed.</summary>
    private static bool Reaches(
        SolverIndex index, CostTable costs, Dictionary<int, int> pins,
        Dictionary<int, LoopSeed> seeds, IEnumerable<int> from, HashSet<int> members)
    {
        var pending = new Stack<int>(from);
        var seen = new HashSet<int>();
        while (pending.TryPop(out var item))
        {
            if (members.Contains(item))
            {
                return true;
            }
            if (!seen.Add(item))
            {
                continue;
            }
            foreach (var child in Children(index, costs, pins, seeds, item))
            {
                pending.Push(child);
            }
        }
        return false;
    }

    private static LoopSystem AnalyzeLoop(
        SolverIndex index, CostTable costs, Dictionary<int, int> pins, List<int> members)
    {
        var row = members.Select((item, i) => (item, i)).ToDictionary(pair => pair.item, pair => pair.i);
        var count = members.Count;
        var recipes = new int[count];
        var yields = new double[count];
        var picks = new int[count][];
        var gain = new double[count, count];
        for (var j = 0; j < count; j++)
        {
            recipes[j] = Chosen(index, pins, costs, members[j]);
            yields[j] = index.Yield(recipes[j], members[j]);
            picks[j] = SlotChoice.Picks(costs, members[j], recipes[j]);
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

    /// <summary>The loop's demands net of what the seed supplies: members the supply covers
    /// entirely drop to zero and out of the system, and the rest is solved again, until no
    /// member is asked for less than nothing.</summary>
    private static double[] SolveSupplied(double[,] gain, double[] demand, double[] supply)
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
                    reduced[i, j] = active[i] && active[j] ? gain[i, j] : 0;
                }
            }
            var solution = Eliminate(reduced, rhs)!;
            var negative = Array.FindIndex(solution, value => value < 0);
            if (negative < 0)
            {
                return solution;
            }
            active[negative] = false;
        }
    }

    /// <summary>Solves <c>(I − gain) · x = demand</c> by elimination without pivoting. The
    /// matrix has non-positive off-diagonals, so every pivot stays positive exactly when the
    /// loop's gain is below one and the series converges; a pivot at or below zero means the
    /// loop feeds itself for free, and null is returned.</summary>
    private static double[]? Eliminate(double[,] gain, double[] demand)
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
            if (pivot <= PivotEpsilon)
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

    private Dictionary<int, int> ValidatePins(
        SolverIndex index, Garage garage, IReadOnlyDictionary<string, string> pins,
        List<BomWarning> warnings)
    {
        var active = new Dictionary<int, int>();
        foreach (var (itemId, recipeId) in pins)
        {
            if (!index.RecipeIndex.TryGetValue(recipeId, out var recipe)
                || !index.TryGetItem(itemId, out var item)
                || index.Yield(recipe, item) <= 0)
            {
                warnings.Add(new BomWarning("pin_unknown", itemId));
                continue;
            }
            if (!legality.IsLegal(index, recipe, garage))
            {
                warnings.Add(new BomWarning("pin_illegal", itemId));
                continue;
            }
            active[item] = recipe;
        }
        return active;
    }

    /// <summary>Tarjan's walk over chosen edges from the roots. Returns the components with
    /// consumers before producers, or the pinned item of the first loop that has no finite plan
    /// — a pin is the only way to build one, since the solve's own loops converge.</summary>
    private static (List<BomComponent> Components, int? CyclePin) Walk(
        SolverIndex index, CostTable costs, Dictionary<int, int> pins,
        List<int> roots, Dictionary<int, LoopSeed> seeds, IReadOnlyList<string> extraIds)
    {
        var order = 0;
        var indices = new Dictionary<int, int>();
        var low = new Dictionary<int, int>();
        var onStack = new HashSet<int>();
        var stack = new Stack<int>();
        var components = new List<BomComponent>();
        foreach (var root in roots)
        {
            if (indices.ContainsKey(root))
            {
                continue;
            }
            var work = new Stack<(int Item, int Next, List<int> Children)>();
            indices[root] = low[root] = order++;
            stack.Push(root);
            onStack.Add(root);
            work.Push((root, 0, Children(index, costs, pins, seeds, root)));
            while (work.Count > 0)
            {
                var (item, next, children) = work.Pop();
                if (next < children.Count)
                {
                    work.Push((item, next + 1, children));
                    var child = children[next];
                    if (!indices.ContainsKey(child))
                    {
                        indices[child] = low[child] = order++;
                        stack.Push(child);
                        onStack.Add(child);
                        work.Push((child, 0, Children(index, costs, pins, seeds, child)));
                    }
                    else if (onStack.Contains(child))
                    {
                        low[item] = Math.Min(low[item], indices[child]);
                    }
                    continue;
                }
                if (work.Count > 0)
                {
                    var parent = work.Peek().Item;
                    low[parent] = Math.Min(low[parent], low[item]);
                }
                if (low[item] != indices[item])
                {
                    continue;
                }
                var members = new List<int>();
                int member;
                do
                {
                    member = stack.Pop();
                    onStack.Remove(member);
                    members.Add(member);
                }
                while (member != item);
                var loop = members.Count > 1 || children.Contains(item);
                if (loop)
                {
                    var system = AnalyzeLoop(index, costs, pins, members);
                    if (Eliminate(system.Gain, new double[members.Count]) is null)
                    {
                        foreach (var candidate in members)
                        {
                            if (pins.ContainsKey(candidate))
                            {
                                return ([], candidate);
                            }
                        }
                        var id = item < index.ItemCount ? index.ItemIds[item] : extraIds[item - index.ItemCount];
                        throw new InvalidOperationException(
                            $"recipe loop through '{id}' never converges and contains no pin; the solve is inconsistent");
                    }
                }
                components.Add(new BomComponent(members, loop));
            }
        }
        components.Reverse();
        return (components, null);
    }

    /// <summary>An item's chosen inputs, plus the seed route's inputs where the item seeds its
    /// loop, so the walk orders that route after the loop it starts.</summary>
    private static List<int> Children(
        SolverIndex index, CostTable costs, Dictionary<int, int> pins,
        Dictionary<int, LoopSeed> seeds, int item)
    {
        if (item >= index.ItemCount || index.IsLeaf(item))
        {
            return [];
        }
        var recipe = Chosen(index, pins, costs, item);
        if (recipe < 0)
        {
            return [];
        }
        var children = PickedItems(index, recipe, SlotChoice.Picks(costs, item, recipe)).ToList();
        if (seeds.TryGetValue(item, out var seed))
        {
            children.AddRange(PickedItems(index, seed.Recipe, seed.Picks));
        }
        return children;
    }

    private static IEnumerable<int> PickedItems(SolverIndex index, int recipe, int[] picks)
    {
        for (var s = 0; s < picks.Length; s++)
        {
            yield return index.AlternativeItem[index.AlternativeAt(recipe, s, picks[s])];
        }
    }

    /// <summary>The pinned recipe, else the solve's, else -1; a position past the index has
    /// neither.</summary>
    private static int Chosen(SolverIndex index, Dictionary<int, int> pins, CostTable costs, int item) =>
        pins.TryGetValue(item, out var pinned)
            ? pinned
            : item < index.ItemCount ? costs.BestRecipe(item) : -1;

    /// <summary>The fewest whole runs whose expected yield covers the demand; the tolerance
    /// keeps an exactly-divisible demand from rounding one run too high. Chanced outputs
    /// still divide by chance (§4), so the cover holds in expectation only.</summary>
    private static long WholeRuns(long demanded, double yield) =>
        (long)Math.Ceiling(demanded / yield - 1e-9);

    private static BomTargetResult TargetResult(
        SolverIndex index, CostTable costs, Dictionary<int, int> pins, BomTarget target)
    {
        if (!index.TryGetItem(target.ItemId, out var item) || index.IsLeaf(item))
        {
            return new BomTargetResult(target.ItemId, target.Count, null, []);
        }
        var recipe = Chosen(index, pins, costs, item);
        if (recipe < 0)
        {
            return new BomTargetResult(target.ItemId, target.Count, null, []);
        }

        var runs = target.Count / index.Yield(recipe, item);
        var inputs = new Dictionary<string, double>();
        var picks = SlotChoice.Picks(costs, item, recipe);
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
