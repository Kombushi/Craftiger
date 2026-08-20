using Craftiger.Builder.Models;

namespace Craftiger.Builder.Interfaces;

public interface IConservationService
{
    /// <summary>Drops recipes that provably output more of a material than their inputs
    /// could contain — untagged reverse-crafting that would amplify matter.</summary>
    List<PlannerRecipe> Run(List<PlannerRecipe> recipes, Dump dump, UnifiedItems unified);
}
