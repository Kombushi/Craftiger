using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces.Recipes;

/// <summary>Flattens dump recipes into planner recipes over canonical items.</summary>
public interface IRecipeTransformService
{
    List<PlannerRecipe> Run(Dump dump, UnifiedItems unified);
}
