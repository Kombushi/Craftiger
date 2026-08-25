using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces;

/// <summary>Turns the Tree Growth Simulator's input-less NEI rows into recipes with their sapling and tool catalysts.</summary>
public interface ITreeFarmRecipeService
{
    List<PlannerRecipe> Run(Dump dump, UnifiedItems unified);
}
