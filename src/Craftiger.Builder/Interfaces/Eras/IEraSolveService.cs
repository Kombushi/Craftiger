using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Eras;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces.Eras;

/// <summary>Solves each item's production era: a min-of-max fixpoint over the recipe graph.</summary>
public interface IEraSolveService
{
    EraSolve Run(
        IReadOnlyList<PlannerRecipe> recipes,
        IReadOnlyDictionary<string, string> leafClasses,
        UnifiedItems unified,
        Dump dump,
        WorldgenEras worldgen);
}
