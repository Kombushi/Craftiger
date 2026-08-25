using Craftiger.Builder.Interfaces.Recipes;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Services.Recipes;

/// <summary>Machine items gate an era only when they exist as real craftable items.</summary>
public sealed class RecipeMachineListService : IRecipeMachineListService
{
    public IReadOnlyDictionary<string, IReadOnlyList<RecipeMachine>> Run(Dump dump, UnifiedItems unified)
    {
        var machinesByTypeId = new Dictionary<string, IReadOnlyList<RecipeMachine>>();
        foreach (var (typeId, icons) in dump.HandlerItemsByRecipeTypeId)
        {
            machinesByTypeId[typeId] = icons
                .Where(dump.Items.ContainsKey)
                .Select(unified.Canonical)
                .Distinct()
                .Select(id => new RecipeMachine(id, Multiblock: false, Tier: null, Steam: false))
                .ToList();
        }
        // GregTech maps name their real machines, which the NEI handler icons only approximate.
        foreach (var (typeId, map) in dump.RecipeMapByTypeId)
        {
            var machines = map.Machines
                .Where(m => dump.Items.ContainsKey(m.ItemId))
                .GroupBy(m => unified.Canonical(m.ItemId))
                .Select(g => new RecipeMachine(
                    g.Key, g.Any(m => m.Multiblock), g.Min(m => m.Tier), g.Any(m => m.Steam)))
                .ToList();
            if (machines.Count > 0)
            {
                machinesByTypeId[typeId] = machines;
            }
        }
        return machinesByTypeId;
    }
}
