using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces;

/// <summary>Prices the finished artifacts once to check no route creates matter from nothing.</summary>
public interface IPriceCheckService
{
    PriceCheck Run(
        IReadOnlyList<PlannerRecipe> recipes,
        IReadOnlyDictionary<string, string> leafClasses,
        IReadOnlyDictionary<string, int> tiers,
        IReadOnlyDictionary<string, double> weights,
        UnifiedItems unified,
        Dump dump);
}
