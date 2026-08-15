using Craftiger.Builder.Models;

namespace Craftiger.Builder.Interfaces;

/// <summary>Solves each item's production era: a min-of-max fixpoint over the recipe graph.</summary>
public interface IEraSolveService
{
    EraSolve Run(
        List<PlannerRecipe> recipes, Dictionary<string, string> leafClasses, UnifiedItems unified,
        Dump dump, WorldgenEras worldgen);

    void Explain(EraSolve solve, Dictionary<string, string> names, string itemId);
}
