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
    /// says. A pin that closes a loop with no finite plan is ignored with a warning (§9).</summary>
    public BomResult Compute(
        SolverGraph graph, CostTable costs, Garage garage,
        IReadOnlyList<BomTarget> targets, IReadOnlyDictionary<string, string> pins)
    {
        var warnings = new List<BomWarning>();
        var activePins = ValidatePins(graph, garage, pins, warnings);
        var roots = targets.Select(target => target.ItemId).Distinct().ToList();

        // A seed hangs its route off the loop it starts, so the walk is rerun until every loop
        // has been offered one and the seeds' own subtrees are ordered after their loops.
        var seeds = new Dictionary<string, LoopSeed?>();
        List<Component> components;
        while (true)
        {
            var (walked, cyclePin) = Walk(graph, costs, activePins, roots, SeedsByItem(seeds));
            if (cyclePin is not null)
            {
                activePins.Remove(cyclePin);
                warnings.Add(new BomWarning("pin_cycle", cyclePin));
                continue;
            }
            var seeded = false;
            foreach (var component in walked!.Where(component => component.Loop))
            {
                var key = LoopKey(component.Items);
                if (seeds.ContainsKey(key))
                {
                    continue;
                }
                var seed = Seed(graph, costs, garage, activePins, SeedsByItem(seeds), component.Items);
                seeds[key] = seed;
                seeded |= seed is not null;
            }
            if (!seeded)
            {
                components = walked;
                break;
            }
        }

        var demand = new Dictionary<string, double>();
        var wholeDemand = new Dictionary<string, long>();
        foreach (var target in targets)
        {
            demand[target.ItemId] = demand.GetValueOrDefault(target.ItemId) + target.Count;
            wholeDemand[target.ItemId] = wholeDemand.GetValueOrDefault(target.ItemId) + target.Count;
        }

        var walk = new WalkState(graph, costs, garage, activePins, roots.ToHashSet(), demand, wholeDemand, warnings);
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
            targets.Select(target => TargetResult(graph, costs, activePins, target)).ToList(),
            walk.Leaves.Select(leaf => new BomLeaf(leaf.Key, leaf.Value.Amount, leaf.Value.Whole)).ToList(),
            warnings,
            walk.Nodes);
    }

    /// <summary>Everything one walk accumulates, shared by the item and loop expansions.</summary>
    private sealed record WalkState(
        SolverGraph Graph, CostTable Costs, Garage Garage, Dictionary<string, SolverRecipe> Pins,
        HashSet<string> Roots, Dictionary<string, double> Demand, Dictionary<string, long> WholeDemand,
        List<BomWarning> Warnings)
    {
        public Dictionary<string, (double Amount, long Whole)> Leaves { get; } = new();

        public List<BomNode> Nodes { get; } = [];

        public int Loops { get; set; }

        public void Add(string itemId, double amount, long whole)
        {
            Demand[itemId] = Demand.GetValueOrDefault(itemId) + amount;
            WholeDemand[itemId] = WholeDemand.GetValueOrDefault(itemId) + whole;
        }
    }

    /// <summary>A strongly connected component of the chosen-edge graph: one item, or a loop
    /// of items whose chosen recipes consume each other.</summary>
    private sealed record Component(List<string> Items, bool Loop);

    /// <summary>The one outside unit that starts a loop: which member, through which recipe,
    /// with which input stacks.</summary>
    private sealed record LoopSeed(string ItemId, SolverRecipe Recipe, IReadOnlyList<SolverStack> Inputs);

    /// <summary>Loops keep their identity across walks by their smallest member id.</summary>
    private static string LoopKey(IEnumerable<string> members) => members.Min(StringComparer.Ordinal)!;

    private static Dictionary<string, LoopSeed> SeedsByItem(Dictionary<string, LoopSeed?> seeds) =>
        seeds.Values
            .Where(seed => seed is not null)
            .ToDictionary(seed => seed!.ItemId, seed => seed!);

    private void ExpandItem(WalkState walk, string itemId)
    {
        var demanded = walk.Demand.GetValueOrDefault(itemId);
        if (demanded <= 0)
        {
            return;
        }
        var wholeDemanded = walk.WholeDemand.GetValueOrDefault(itemId);
        if (walk.Graph.IsLeaf(itemId))
        {
            var (amount, whole) = walk.Leaves.GetValueOrDefault(itemId);
            walk.Leaves[itemId] = (amount + demanded, whole + wholeDemanded);
            return;
        }

        if (Chosen(walk.Graph, walk.Pins, walk.Costs, itemId) is not { } recipe)
        {
            walk.Warnings.Add(new BomWarning(
                walk.Roots.Contains(itemId) ? "unreachable_target" : "unreachable_input", itemId));
            return;
        }

        var yield = ExpectedYield(recipe, itemId);
        var runs = demanded / yield;
        var wholeRuns = WholeRuns(wholeDemanded, yield);
        var chosen = SlotChoice.Inputs(walk.Costs, itemId, recipe);
        walk.Nodes.Add(new BomNode(
            itemId, demanded, runs, wholeDemanded, wholeRuns, recipe.Id,
            chosen.Select(alternative => new BomStack(alternative.ItemId, alternative.Amount)).ToList(),
            Loop: null, Seed: false));
        foreach (var alternative in chosen)
        {
            walk.Add(alternative.ItemId, runs * alternative.Amount, wholeRuns * alternative.Amount);
        }
    }

    /// <summary>Solves a loop's demands as one system — fractional by elimination, whole runs
    /// by iterating the same equations on integers until they settle — then seeds it with one
    /// unit from outside.</summary>
    private static void ExpandLoop(WalkState walk, Component component, LoopSeed? seed)
    {
        var system = AnalyzeLoop(walk.Graph, walk.Costs, walk.Pins, component.Items);
        var members = component.Items;
        var count = members.Count;
        var external = members.Select(id => walk.Demand.GetValueOrDefault(id)).ToArray();
        if (external.All(amount => amount <= 0))
        {
            return;
        }
        var externalWhole = members.Select(id => walk.WholeDemand.GetValueOrDefault(id)).ToArray();

        var totals = Eliminate(system.Gain, external)!;
        var runs = new double[count];
        for (var i = 0; i < count; i++)
        {
            runs[i] = totals[i] / system.Yields[i];
        }

        var wholeTotals = (long[])externalWhole.Clone();
        var wholeRuns = new long[count];
        for (var round = 0; round < MaxWholeRounds; round++)
        {
            for (var i = 0; i < count; i++)
            {
                wholeRuns[i] = WholeRuns(wholeTotals[i], system.Yields[i]);
            }
            var next = (long[])externalWhole.Clone();
            for (var j = 0; j < count; j++)
            {
                foreach (var input in system.Inputs[j])
                {
                    if (system.Index.TryGetValue(input.ItemId, out var i))
                    {
                        next[i] += wholeRuns[j] * input.Amount;
                    }
                }
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
            walk.Nodes.Add(new BomNode(
                members[i], totals[i], runs[i], wholeTotals[i], wholeRuns[i], system.Recipes[i].Id,
                system.Inputs[i].Select(input => new BomStack(input.ItemId, input.Amount)).ToList(),
                Loop: loop, Seed: false));
            foreach (var input in system.Inputs[i])
            {
                if (!system.Index.ContainsKey(input.ItemId))
                {
                    walk.Add(input.ItemId, runs[i] * input.Amount, wholeRuns[i] * input.Amount);
                }
            }
        }

        if (seed is null)
        {
            walk.Warnings.Add(new BomWarning("loop_unseeded", members[0]));
            return;
        }
        var seedYield = ExpectedYield(seed.Recipe, seed.ItemId);
        var seedRuns = 1 / seedYield;
        var seedWholeRuns = WholeRuns(1, seedYield);
        walk.Nodes.Add(new BomNode(
            seed.ItemId, 1, seedRuns, 1, seedWholeRuns, seed.Recipe.Id,
            seed.Inputs.Select(input => new BomStack(input.ItemId, input.Amount)).ToList(),
            Loop: loop, Seed: true));
        foreach (var input in seed.Inputs)
        {
            walk.Add(input.ItemId, seedRuns * input.Amount, seedWholeRuns * input.Amount);
        }
    }

    /// <summary>The cheapest garage-legal producer of any loop item that does not itself draw on
    /// the loop — the route a player takes once to get the first unit.</summary>
    private LoopSeed? Seed(
        SolverGraph graph, CostTable costs, Garage garage, Dictionary<string, SolverRecipe> pins,
        Dictionary<string, LoopSeed> seeds, List<string> members)
    {
        var memberSet = members.ToHashSet();
        LoopSeed? best = null;
        var bestCost = double.PositiveInfinity;
        foreach (var itemId in members)
        {
            var chosen = Chosen(graph, pins, costs, itemId);
            foreach (var producer in graph.Producers.GetValueOrDefault(itemId) ?? [])
            {
                if (producer.Id == chosen?.Id || !legality.IsLegal(producer, garage))
                {
                    continue;
                }
                var inputs = SlotChoice.Inputs(costs, itemId, producer);
                var total = 0.0;
                foreach (var input in inputs)
                {
                    total += costs.Costs.TryGetValue(input.ItemId, out var unit)
                        ? unit * input.Amount
                        : double.PositiveInfinity;
                }
                var cost = total / ExpectedYield(producer, itemId);
                if (!(cost < bestCost)
                    || Reaches(graph, costs, pins, seeds, inputs.Select(input => input.ItemId), memberSet))
                {
                    continue;
                }
                best = new LoopSeed(itemId, producer, inputs);
                bestCost = cost;
            }
        }
        return best;
    }

    /// <summary>Whether any of the items reaches a loop member over chosen edges — a seed that
    /// draws on the loop it should start is no seed.</summary>
    private static bool Reaches(
        SolverGraph graph, CostTable costs, Dictionary<string, SolverRecipe> pins,
        Dictionary<string, LoopSeed> seeds, IEnumerable<string> from, HashSet<string> members)
    {
        var pending = new Stack<string>(from);
        var seen = new HashSet<string>();
        while (pending.TryPop(out var itemId))
        {
            if (members.Contains(itemId))
            {
                return true;
            }
            if (!seen.Add(itemId))
            {
                continue;
            }
            foreach (var child in Children(graph, costs, pins, seeds, itemId))
            {
                pending.Push(child);
            }
        }
        return false;
    }

    /// <summary>A loop's recipes and the gain matrix between its members: how many units of
    /// member <c>i</c> one unit of member <c>j</c> consumes through <c>j</c>'s recipe.</summary>
    private sealed record LoopSystem(
        IReadOnlyDictionary<string, int> Index, SolverRecipe[] Recipes, double[] Yields,
        IReadOnlyList<SolverStack>[] Inputs, double[,] Gain);

    private static LoopSystem AnalyzeLoop(
        SolverGraph graph, CostTable costs, Dictionary<string, SolverRecipe> pins, List<string> members)
    {
        var index = members.Select((id, i) => (id, i)).ToDictionary(pair => pair.id, pair => pair.i);
        var count = members.Count;
        var recipes = new SolverRecipe[count];
        var yields = new double[count];
        var inputs = new IReadOnlyList<SolverStack>[count];
        var gain = new double[count, count];
        for (var j = 0; j < count; j++)
        {
            recipes[j] = Chosen(graph, pins, costs, members[j])!;
            yields[j] = ExpectedYield(recipes[j], members[j]);
            inputs[j] = SlotChoice.Inputs(costs, members[j], recipes[j]);
            foreach (var input in inputs[j])
            {
                if (index.TryGetValue(input.ItemId, out var i))
                {
                    gain[i, j] += input.Amount / yields[j];
                }
            }
        }
        return new LoopSystem(index, recipes, yields, inputs, gain);
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
            solution[i] = Math.Max(0, sum / matrix[i, i]);
        }
        return solution;
    }

    private Dictionary<string, SolverRecipe> ValidatePins(
        SolverGraph graph, Garage garage, IReadOnlyDictionary<string, string> pins,
        List<BomWarning> warnings)
    {
        var active = new Dictionary<string, SolverRecipe>();
        foreach (var (itemId, recipeId) in pins)
        {
            if (!graph.RecipesById.TryGetValue(recipeId, out var recipe)
                || recipe.Outputs.All(output => output.ItemId != itemId))
            {
                warnings.Add(new BomWarning("pin_unknown", itemId));
                continue;
            }
            if (!legality.IsLegal(recipe, garage))
            {
                warnings.Add(new BomWarning("pin_illegal", itemId));
                continue;
            }
            active[itemId] = recipe;
        }
        return active;
    }

    /// <summary>Tarjan's walk over chosen edges from the roots. Returns the components with
    /// consumers before producers, or the pinned item of the first loop that has no finite plan
    /// — a pin is the only way to build one, since the solve's own loops converge.</summary>
    private static (List<Component>? Components, string? CyclePin) Walk(
        SolverGraph graph, CostTable costs, Dictionary<string, SolverRecipe> pins,
        List<string> roots, Dictionary<string, LoopSeed> seeds)
    {
        var index = 0;
        var indices = new Dictionary<string, int>();
        var low = new Dictionary<string, int>();
        var onStack = new HashSet<string>();
        var stack = new Stack<string>();
        var components = new List<Component>();
        foreach (var root in roots)
        {
            if (indices.ContainsKey(root))
            {
                continue;
            }
            var work = new Stack<(string Id, int Next, List<string> Children)>();
            indices[root] = low[root] = index++;
            stack.Push(root);
            onStack.Add(root);
            work.Push((root, 0, Children(graph, costs, pins, seeds, root)));
            while (work.Count > 0)
            {
                var (id, next, children) = work.Pop();
                if (next < children.Count)
                {
                    work.Push((id, next + 1, children));
                    var child = children[next];
                    if (!indices.ContainsKey(child))
                    {
                        indices[child] = low[child] = index++;
                        stack.Push(child);
                        onStack.Add(child);
                        work.Push((child, 0, Children(graph, costs, pins, seeds, child)));
                    }
                    else if (onStack.Contains(child))
                    {
                        low[id] = Math.Min(low[id], indices[child]);
                    }
                    continue;
                }
                if (work.Count > 0)
                {
                    var parent = work.Peek().Id;
                    low[parent] = Math.Min(low[parent], low[id]);
                }
                if (low[id] != indices[id])
                {
                    continue;
                }
                var members = new List<string>();
                string member;
                do
                {
                    member = stack.Pop();
                    onStack.Remove(member);
                    members.Add(member);
                }
                while (member != id);
                var loop = members.Count > 1 || children.Contains(id);
                if (loop)
                {
                    var system = AnalyzeLoop(graph, costs, pins, members);
                    if (Eliminate(system.Gain, new double[members.Count]) is null)
                    {
                        var pinned = members.FirstOrDefault(pins.ContainsKey);
                        if (pinned is not null)
                        {
                            return (null, pinned);
                        }
                        throw new InvalidOperationException(
                            $"recipe loop through '{id}' never converges and contains no pin; the solve is inconsistent");
                    }
                }
                components.Add(new Component(members, loop));
            }
        }
        components.Reverse();
        return (components, null);
    }

    /// <summary>An item's chosen inputs, plus the seed route's inputs where the item seeds its
    /// loop, so the walk orders that route after the loop it starts.</summary>
    private static List<string> Children(
        SolverGraph graph, CostTable costs, Dictionary<string, SolverRecipe> pins,
        Dictionary<string, LoopSeed> seeds, string itemId)
    {
        if (graph.IsLeaf(itemId) || Chosen(graph, pins, costs, itemId) is not { } recipe)
        {
            return [];
        }
        var children = SlotChoice.Inputs(costs, itemId, recipe).Select(input => input.ItemId).ToList();
        if (seeds.TryGetValue(itemId, out var seed))
        {
            children.AddRange(seed.Inputs.Select(input => input.ItemId));
        }
        return children;
    }

    private static SolverRecipe? Chosen(
        SolverGraph graph, Dictionary<string, SolverRecipe> pins, CostTable costs, string itemId) =>
        pins.TryGetValue(itemId, out var pinned)
            ? pinned
            : costs.BestRecipes.GetValueOrDefault(itemId);

    /// <summary>The expected amount one run yields, summing chanced twin rows.</summary>
    private static double ExpectedYield(SolverRecipe recipe, string itemId) =>
        recipe.Outputs
            .Where(output => output.ItemId == itemId)
            .Sum(output => output.Amount * output.Chance);

    /// <summary>The fewest whole runs whose expected yield covers the demand; the tolerance
    /// keeps an exactly-divisible demand from rounding one run too high. Chanced outputs
    /// still divide by chance (§4), so the cover holds in expectation only.</summary>
    private static long WholeRuns(long demanded, double yield) =>
        (long)Math.Ceiling(demanded / yield - 1e-9);

    private BomTargetResult TargetResult(
        SolverGraph graph, CostTable costs, Dictionary<string, SolverRecipe> pins, BomTarget target)
    {
        if (graph.IsLeaf(target.ItemId)
            || Chosen(graph, pins, costs, target.ItemId) is not { } recipe)
        {
            return new BomTargetResult(target.ItemId, target.Count, null, []);
        }

        var runs = target.Count / ExpectedYield(recipe, target.ItemId);
        var inputs = new Dictionary<string, double>();
        foreach (var alternative in SlotChoice.Inputs(costs, target.ItemId, recipe))
        {
            inputs[alternative.ItemId] = inputs.GetValueOrDefault(alternative.ItemId) + runs * alternative.Amount;
        }
        return new BomTargetResult(
            target.ItemId, target.Count, recipe.Id,
            inputs.Select(input => new BomStack(input.Key, input.Value)).ToList());
    }
}
