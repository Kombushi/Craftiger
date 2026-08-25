using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Eras;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces.Eras;

/// <summary>Runs the min-of-max fixpoint over the recipes to exhaustion, lowering eras strictly.</summary>
public interface IEraPropagationService
{
    void Run(IReadOnlyList<PlannerRecipe> recipes, EraTable table, UnifiedItems unified, Dump dump);
}
