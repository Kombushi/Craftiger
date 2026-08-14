using Gtnh.Planner.Builder.Models;

namespace Gtnh.Planner.Builder.Interfaces;

/// <summary>Flattens dump recipes into planner recipes over canonical items.</summary>
public interface IRecipeTransformService
{
    List<PlannerRecipe> Run(Dump dump, UnifiedItems unified);
}
