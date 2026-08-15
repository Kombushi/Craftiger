using Craftiger.Builder.Models;

namespace Craftiger.Builder.Interfaces;

/// <summary>Prices the finished artifacts once to check no route creates matter from nothing.</summary>
public interface IPriceCheckService
{
    PriceCheck Run(
        List<PlannerRecipe> recipes, Dictionary<string, string> leafClasses,
        IReadOnlyDictionary<string, int> tiers, IReadOnlyDictionary<string, double> weights,
        UnifiedItems unified, Dump dump);
}