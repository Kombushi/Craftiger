using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Eras;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces;

/// <summary>Turns pumpable underground fluids into era-only recipes for drilling them up.</summary>
public interface IUndergroundFluidRecipeService
{
    List<PlannerRecipe> Run(Dump dump, UnifiedItems unified, WorldgenEras worldgen);
}
