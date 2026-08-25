using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Eras;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces.Eras;

/// <summary>Tiers the era-priced leaf classes, falling back to direct recipes and twin materials.</summary>
public interface ILeafTierService
{
    IReadOnlyDictionary<string, int> Run(
        IReadOnlyList<PlannerRecipe> recipes,
        IReadOnlyDictionary<string, string> leafClasses,
        UnifiedItems unified,
        OrePrefixIndex prefixes,
        EraTable table);
}
