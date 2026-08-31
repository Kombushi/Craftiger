using Craftiger.Builder.Interfaces.Eras;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Eras;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services.Eras;

public sealed class EraSolveService(
    IEraSeedService seeds,
    IEraPropagationService propagation,
    ILeafTierService leafTiers,
    IMachineAvailabilityService availability,
    IOptions<ErasConfiguration> eras,
    ILogger<EraSolveService> logger) : IEraSolveService
{
    private readonly ErasConfiguration _eras = eras.Value;

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
        return table.ToSolve(tiers, availability.Run(recipes, table), Environment(dump, unified, table));
    }

    /// <summary>The cleanroom wall from the solved table, falling back to the configured floor when the dump lacks the controller.</summary>
    private PlannerEnvironment Environment(Dump dump, UnifiedItems unified, EraTable table)
    {
        var cleanroomIds = dump.ItemIdsNamed(_eras.CleanroomItemName).Select(unified.Canonical).ToHashSet();
        var era = table.CheapestEra(cleanroomIds);
        if (era == int.MaxValue)
        {
            logger.LogWarning("the cleanroom controller is unreachable; its era wall falls back to {Era}", _eras.CleanroomMinEra);
            era = _eras.CleanroomMinEra;
        }
        var lowGravityEra = _eras.DimensionTierEras.TryGetValue(1, out var t1)
            ? t1
            : throw new InvalidOperationException("ErasConfiguration.DimensionTierEras carries no T1 rocket era");
        return new PlannerEnvironment(cleanroomIds.Order(StringComparer.Ordinal).FirstOrDefault() ?? "", era, lowGravityEra);
    }
}
