using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces;

/// <summary>Turns CropsNH crops into era-only recipes for harvesting what they grow.</summary>
public interface ICropHarvestRecipeService
{
    List<PlannerRecipe> Run(Dump dump, UnifiedItems unified);
}
