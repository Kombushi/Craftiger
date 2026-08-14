using Gtnh.Planner.Builder.Models;

namespace Gtnh.Planner.Builder.Interfaces;

/// <summary>Tiers ingots by production era: a min-of-max fixpoint over the recipe graph.</summary>
public interface IIngotTiersService
{
    EraSolve Run(List<PlannerRecipe> recipes, Dictionary<string, string> leafClasses, UnifiedItems unified, Dump dump);

    void Explain(EraSolve solve, Dictionary<string, string> names, string itemId);
}
