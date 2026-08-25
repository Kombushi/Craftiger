using Craftiger.Builder.Interfaces.Eras;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Eras;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Services.Eras;

public sealed class EraSolveService(
    IEraSeedService seeds,
    IEraPropagationService propagation,
    ILeafTierService leafTiers,
    IMachineAvailabilityService availability) : IEraSolveService
{
    public EraSolve Run(
        IReadOnlyList<PlannerRecipe> recipes,
        IReadOnlyDictionary<string, string> leafClasses,
        UnifiedItems unified,
        Dump dump,
        WorldgenEras worldgen)
    {
        var table = seeds.Run(leafClasses, unified, dump, worldgen);
        propagation.Run(recipes, table, unified, dump);
        var tiers = leafTiers.Run(recipes, leafClasses, unified, dump.OrePrefixes, table);
        return table.ToSolve(tiers, availability.Run(recipes, table));
    }
}
