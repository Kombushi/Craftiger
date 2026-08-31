using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Models.Eras;

/// <summary>The settled era solve: material tiers, every reachable item's era and the recipe that set it, the seeds, and each map's availability era.</summary>
public sealed record EraSolve(
    IReadOnlyDictionary<string, int> Tiers,
    IReadOnlyDictionary<string, int> Era,
    IReadOnlyDictionary<string, PlannerRecipe> BestRecipe,
    IReadOnlySet<string> Seeds,
    IReadOnlyDictionary<string, int?> MachineEras,
    PlannerEnvironment Environment)
{
    private const int ExplainDepth = 12;

    public int? EraOf(string itemId) => Era.TryGetValue(itemId, out var era) ? era : null;

    public bool IsSeed(string itemId) => Seeds.Contains(itemId);

    public bool IsReachable(string itemId) => Era.ContainsKey(itemId);

    /// <summary>The item's era derivation as indented lines: its recipe, the machine that gates it, and each slot's cheapest input.</summary>
    public IReadOnlyList<string> Explain(string itemId, IReadOnlyDictionary<string, string> names)
    {
        var lines = new List<string>();
        Explain(lines, names, itemId, depth: 0);
        return lines;
    }

    private void Explain(List<string> lines, IReadOnlyDictionary<string, string> names, string itemId, int depth)
    {
        var name = names.GetValueOrDefault(itemId, itemId);
        var indent = new string(' ', depth * 2);
        if (EraOf(itemId) is not { } era)
        {
            lines.Add($"{indent}{name}: unreachable");
            return;
        }
        if (IsSeed(itemId) || !BestRecipe.TryGetValue(itemId, out var recipe))
        {
            lines.Add($"{indent}{name}: era {era} (seed)");
            return;
        }
        lines.Add($"{indent}{name}: era {era} via {recipe.Machine} tier {recipe.Tier} ({recipe.Id})");
        if (depth >= ExplainDepth)
        {
            lines.Add($"{indent}  ...");
            return;
        }
        var machine = recipe.Machines
            .Select(m => m.ItemId)
            .Where(IsReachable)
            .OrderBy(id => Era[id])
            .FirstOrDefault();
        if (machine is not null && Era[machine] > 0)
        {
            lines.Add($"{indent}  [machine] {names.GetValueOrDefault(machine, machine)}: era {Era[machine]}");
            if (depth < 3)
            {
                Explain(lines, names, machine, depth + 2);
            }
        }
        foreach (var slot in recipe.InputSlotAlternatives)
        {
            var best = slot.Where(IsReachable).OrderBy(id => Era[id]).FirstOrDefault() ?? slot[0];
            Explain(lines, names, best, depth + 1);
        }
    }
}
