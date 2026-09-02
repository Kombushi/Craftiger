using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces;

/// <summary>Turns the Algae Pond's input-less per-tier rows into recipes at the hatch tier's power, with their compost-boosted twins.</summary>
public interface IAlgaePondRecipeService
{
    List<PlannerRecipe> Run(Dump dump, UnifiedItems unified);
}
