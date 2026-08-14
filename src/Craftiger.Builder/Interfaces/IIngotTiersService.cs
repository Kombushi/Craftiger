using Craftiger.Builder.Models;

namespace Craftiger.Builder.Interfaces;

/// <summary>Tiers ingots by production era: a min-of-max fixpoint over the recipe graph.</summary>
public interface IIngotTiersService
{
    EraSolve Run(List<PlannerRecipe> recipes, Dictionary<string, string> leafClasses, UnifiedItems unified, Dump dump);

    void Explain(EraSolve solve, Dictionary<string, string> names, string itemId);
}
