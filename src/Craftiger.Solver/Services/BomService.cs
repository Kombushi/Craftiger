using Craftiger.Solver.Interfaces;
using Craftiger.Solver.Models;

namespace Craftiger.Solver.Services;

public sealed class BomService(IGarageLegalityService legality) : IBomService
{
    /// <summary>Walks the bestRecipe DAG with pins overlaid, in reverse topological order, so
    /// shared intermediates accumulate their full demand before they are expanded. Each step
    /// carries two accountings: fractional expected values for pricing, and whole runs — the
    /// accumulated demand rounded up once per item — for a plan a machine can execute. Leaves
    /// never expand, whatever their bestRecipe says. A pin that would close a cycle is
    /// ignored with a warning (§9).</summary>
    public BomResult Compute(
        SolverGraph graph, CostTable costs, Garage garage,
        IReadOnlyList<BomTarget> targets, IReadOnlyDictionary<string, string> pins)
    {
        var warnings = new List<BomWarning>();
        var activePins = ValidatePins(graph, garage, pins, warnings);
        var roots = targets.Select(target => target.ItemId).Distinct().ToList();

        List<string> order;
        while (true)
        {
            var (walked, cyclePin) = Walk(graph, costs, activePins, roots);
            if (cyclePin is null)
            {
                order = walked!;
                break;
            }
            activePins.Remove(cyclePin);
            warnings.Add(new BomWarning("pin_cycle", cyclePin));
        }

        var demand = new Dictionary<string, double>();
        var wholeDemand = new Dictionary<string, long>();
        foreach (var target in targets)
        {
            demand[target.ItemId] = demand.GetValueOrDefault(target.ItemId) + target.Count;
            wholeDemand[target.ItemId] = wholeDemand.GetValueOrDefault(target.ItemId) + target.Count;
        }

        var rootSet = roots.ToHashSet();
        var leaves = new Dictionary<string, (double Amount, long Whole)>();
        var nodes = new List<BomNode>();
        foreach (var itemId in order)
        {
            var demanded = demand.GetValueOrDefault(itemId);
            if (demanded <= 0)
            {
                continue;
            }
            var wholeDemanded = wholeDemand.GetValueOrDefault(itemId);
            if (graph.IsLeaf(itemId))
            {
                var (amount, whole) = leaves.GetValueOrDefault(itemId);
                leaves[itemId] = (amount + demanded, whole + wholeDemanded);
                continue;
            }

            if (Chosen(graph, activePins, costs, itemId) is not { } recipe)
            {
                warnings.Add(new BomWarning(
                    rootSet.Contains(itemId) ? "unreachable_target" : "unreachable_input", itemId));
                continue;
            }

            var yield = ExpectedYield(recipe, itemId);
            var runs = demanded / yield;
            var wholeRuns = WholeRuns(wholeDemanded, yield);
            var chosen = SlotChoice.Inputs(costs, itemId, recipe);
            nodes.Add(new BomNode(
                itemId, demanded, runs, wholeDemanded, wholeRuns, recipe.Id,
                chosen.Select(alternative => new BomStack(alternative.ItemId, alternative.Amount)).ToList()));
            foreach (var alternative in chosen)
            {
                demand[alternative.ItemId] =
                    demand.GetValueOrDefault(alternative.ItemId) + runs * alternative.Amount;
                wholeDemand[alternative.ItemId] =
                    wholeDemand.GetValueOrDefault(alternative.ItemId) + wholeRuns * alternative.Amount;
            }
        }

        return new BomResult(
            targets.Select(target => TargetResult(graph, costs, activePins, target)).ToList(),
            leaves.Select(leaf => new BomLeaf(leaf.Key, leaf.Value.Amount, leaf.Value.Whole)).ToList(),
            warnings,
            nodes);
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

    /// <summary>Depth-first walk over chosen edges. Returns the reverse topological order, or
    /// the pinned item closing a cycle — the unpinned DAG is acyclic by construction, so any
    /// cycle must run through a pin.</summary>
    private static (List<string>? Order, string? CyclePin) Walk(
        SolverGraph graph, CostTable costs, Dictionary<string, SolverRecipe> pins,
        List<string> roots)
    {
        var color = new Dictionary<string, int>();
        var order = new List<string>();
        foreach (var root in roots)
        {
            if (color.GetValueOrDefault(root) != 0)
            {
                continue;
            }
            color[root] = 1;
            var stack = new List<(string Id, int Next)> { (root, 0) };
            while (stack.Count > 0)
            {
                var (id, next) = stack[^1];
                var children = Children(graph, costs, pins, id);
                if (next < children.Count)
                {
                    stack[^1] = (id, next + 1);
                    var child = children[next];
                    var childColor = color.GetValueOrDefault(child);
                    if (childColor == 1)
                    {
                        var from = stack.FindIndex(frame => frame.Id == child);
                        for (var i = from; i < stack.Count; i++)
                        {
                            if (pins.ContainsKey(stack[i].Id))
                            {
                                return (null, stack[i].Id);
                            }
                        }
                        throw new InvalidOperationException(
                            $"recipe cycle through '{child}' contains no pin; the bestRecipe DAG is broken");
                    }
                    if (childColor == 0)
                    {
                        color[child] = 1;
                        stack.Add((child, 0));
                    }
                }
                else
                {
                    color[id] = 2;
                    order.Add(id);
                    stack.RemoveAt(stack.Count - 1);
                }
            }
        }
        order.Reverse();
        return (order, null);
    }

    private static List<string> Children(
        SolverGraph graph, CostTable costs, Dictionary<string, SolverRecipe> pins, string itemId)
    {
        if (graph.IsLeaf(itemId) || Chosen(graph, pins, costs, itemId) is not { } recipe)
        {
            return [];
        }
        return SlotChoice.Inputs(costs, itemId, recipe).Select(input => input.ItemId).ToList();
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
