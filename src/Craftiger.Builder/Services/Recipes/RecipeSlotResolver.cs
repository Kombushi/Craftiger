using Craftiger.Builder.Interfaces.Recipes;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Services.Recipes;

/// <summary>A catalyst is a zero-size stack by the dump's own mark, or a tool that crafts into its own worn self.</summary>
public sealed class RecipeSlotResolver : IRecipeSlotResolver
{
    public ResolvedSlot Resolve(Dump dump, UnifiedItems unified, ToolIndex tools, string groupId)
    {
        if (!dump.GroupStacks.TryGetValue(groupId, out var stacks))
        {
            return ResolvedSlot.Empty;
        }

        var catalyst = false;
        var members = new List<SlotMember>();
        foreach (var stack in stacks.OrderBy(stack => unified.Canonical(stack.ItemId), StringComparer.Ordinal))
        {
            var canonical = unified.Canonical(stack.ItemId);
            var tool = tools.IsTool(stack.ItemId) || tools.IsTool(canonical);
            if (stack.Size <= 0 || tool)
            {
                catalyst = true;
            }
            members.Add(new SlotMember(canonical, Math.Max(1, stack.Size), tool));
        }
        return new ResolvedSlot(members, catalyst);
    }
}
