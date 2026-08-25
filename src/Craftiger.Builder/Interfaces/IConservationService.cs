using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces;

/// <summary>Drops recipes that provably output more matter than their inputs could contain.</summary>
public interface IConservationService
{
    List<PlannerRecipe> Run(IReadOnlyList<PlannerRecipe> recipes, Dump dump, UnifiedItems unified);
}
