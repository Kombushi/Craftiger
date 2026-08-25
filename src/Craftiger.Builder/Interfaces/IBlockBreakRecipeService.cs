using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces;

/// <summary>Turns the dump's block drops into recipes for breaking a block by hand.</summary>
public interface IBlockBreakRecipeService
{
    List<PlannerRecipe> Run(Dump dump, UnifiedItems unified);
}
