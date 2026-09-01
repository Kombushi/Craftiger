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

    /// <summary>Bounds the coil fixpoint; the ladder rises monotonically, so more means a cyclic ladder.</summary>
    private const int MaxCoilIterations = 16;

    public EraSolve Run(
        IReadOnlyList<PlannerRecipe> recipes,
        IReadOnlyDictionary<string, string> leafClasses,
        UnifiedItems unified,
        Dump dump,
        WorldgenEras worldgen)
    {
        var unscoped = recipes.Where(recipe => recipe.Scope == RecipeScope.None).ToList();

        // Coil tiers are the eras the coils are first craftable at, which the heat gates they set
        // feed back into: start every coil at era 0 and re-solve until the ladder stops rising.
        var ladder = new CoilLadder([.. dump.Coils
            .Select(coil => new LadderCoil(coil.Name, coil.Heat, 0))
            .OrderBy(coil => coil.MaxHeat)]);
        EraTable table;
        var iteration = 0;
        while (true)
        {
            iteration++;
            table = seeds.Run(leafClasses, unified, dump, worldgen);
            propagation.Run(unscoped, table, unified, dump, ladder);
            var settled = SolvedLadder(dump.Coils, unified, table);
            if (settled.Coils.SequenceEqual(ladder.Coils))
            {
                break;
            }
            if (iteration >= MaxCoilIterations)
            {
                throw new InvalidOperationException("the coil-era fixpoint did not settle; the coil ladder is cyclic");
            }
            ladder = settled;
        }
        logger.LogInformation("  coil ladder settled after {Iterations} era solves", iteration);
        var unresolved = dump.Coils.Count(coil => !table.TryGetEra(unified.Canonical(coil.ItemId), out _));
        if (unresolved > 0)
        {
            logger.LogWarning(
                "{Count} coils never become craftable and ship at the ladder's edge era {Edge}",
                unresolved, ladder.Coils.Max(coil => coil.Tier));
        }

        var tiers = leafTiers.Run(unscoped, leafClasses, unified, dump.OrePrefixes, table, ladder);
        return table.ToSolve(
            tiers, availability.Run(recipes, table), Environment(dump, unified, table), ladder.Coils);
    }

    /// <summary>The ladder the solved table implies: each coil at its item's era. A coil the solve
    /// never reaches ships at the ladder's edge — the garage must still be able to install what
    /// the model merely fails to reach, or every hotter recipe becomes a pricing hole.</summary>
    private static CoilLadder SolvedLadder(IReadOnlyList<DumpCoil> coils, UnifiedItems unified, EraTable table)
    {
        var eras = new Dictionary<string, int>();
        foreach (var coil in coils)
        {
            if (table.TryGetEra(unified.Canonical(coil.ItemId), out var era))
            {
                eras[coil.ItemId] = era;
            }
        }
        var edge = eras.Count == 0 ? 0 : eras.Values.Max() + 1;
        return new CoilLadder([.. coils
            .Select(coil => new LadderCoil(coil.Name, coil.Heat, eras.GetValueOrDefault(coil.ItemId, edge)))
            .OrderBy(coil => coil.MaxHeat)]);
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
