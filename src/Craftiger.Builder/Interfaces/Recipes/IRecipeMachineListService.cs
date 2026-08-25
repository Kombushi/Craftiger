using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces.Recipes;

/// <summary>The craftable machines serving each recipe type, GregTech's own map lists outranking NEI handler icons.</summary>
public interface IRecipeMachineListService
{
    IReadOnlyDictionary<string, IReadOnlyList<RecipeMachine>> Run(Dump dump, UnifiedItems unified);
}
