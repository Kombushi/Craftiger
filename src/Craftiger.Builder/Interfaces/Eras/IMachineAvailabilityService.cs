using Craftiger.Builder.Models.Eras;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces.Eras;

/// <summary>When each recipe map's machinery first exists, floored by any configured gate.</summary>
public interface IMachineAvailabilityService
{
    IReadOnlyDictionary<string, int?> Run(IReadOnlyList<PlannerRecipe> recipes, EraTable table);
}
