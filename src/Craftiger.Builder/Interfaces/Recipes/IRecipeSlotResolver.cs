using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces.Recipes;

/// <summary>Resolves an input group to its canonical members, flagging catalysts: zero-size stacks and wearing tools.</summary>
public interface IRecipeSlotResolver
{
    ResolvedSlot Resolve(Dump dump, UnifiedItems unified, ToolIndex tools, string groupId);
}
