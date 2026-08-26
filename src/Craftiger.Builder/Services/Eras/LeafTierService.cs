using System.Collections.Frozen;
using Craftiger.Builder.Interfaces.Eras;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Eras;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services.Eras;

public sealed class LeafTierService(
    IOptions<ErasConfiguration> eras,
    ILogger<LeafTierService> logger) : ILeafTierService
{
    /// <summary>Leaf classes priced by production era rather than a flat weight.</summary>
    private static readonly FrozenSet<string> _tieredClasses = FrozenSet.ToFrozenSet<string>(["ingot", "gem", "dust"]);

    private readonly CoilLadder _coils = new(eras.Value.Coils);

    public IReadOnlyDictionary<string, int> Run(
        IReadOnlyList<PlannerRecipe> recipes,
        IReadOnlyDictionary<string, string> leafClasses,
        UnifiedItems unified,
        OrePrefixIndex prefixes,
        EraTable table)
    {
        var tiers = new Dictionary<string, int>();
        foreach (var (id, leafClass) in leafClasses)
        {
            if (_tieredClasses.Contains(leafClass) && table.TryGetEra(id, out var itemEra))
            {
                tiers[id] = itemEra;
            }
        }

        var recycled = ApplyRecyclingFallback(tiers, recipes, unified, prefixes, leafClasses);
        InheritTwinTiers(tiers, leafClasses, unified);

        logger.LogInformation("  {Recycled:N0} materials tiered by recycling fallback", recycled);
        return tiers;
    }

    /// <summary>Materials that never bootstrap (recycling-only) fall back to the cheapest direct recipe.</summary>
    private int ApplyRecyclingFallback(
        Dictionary<string, int> tiers,
        IReadOnlyList<PlannerRecipe> recipes,
        UnifiedItems unified,
        OrePrefixIndex prefixes,
        IReadOnlyDictionary<string, string> leafClasses)
    {
        // Pile packing and remelting exist for every material at ULV, so a reshuffle only speaks when nothing else does.
        var fallback = new Dictionary<string, int>();
        var reshuffle = new Dictionary<string, int>();
        foreach (var recipe in recipes)
        {
            // Era-only recipes never price, so they cannot stand in for one that does.
            if (recipe.EraOnly)
            {
                continue;
            }
            var intrinsic = _coils.Floor(recipe.BestCaseTier, recipe.Heat);
            foreach (var output in recipe.Outputs)
            {
                if (!_tieredClasses.Contains(leafClasses.GetValueOrDefault(output.ItemId) ?? "")
                    || tiers.ContainsKey(output.ItemId))
                {
                    continue;
                }
                var pool = ReshufflesOwnShapes(recipe, output.ItemId, unified, prefixes)
                    ? reshuffle
                    : fallback;
                if (!pool.TryGetValue(output.ItemId, out var current) || intrinsic < current)
                {
                    pool[output.ItemId] = intrinsic;
                }
            }
        }
        foreach (var (id, tier) in reshuffle)
        {
            fallback.TryAdd(id, tier);
        }
        foreach (var (id, tier) in fallback)
        {
            tiers[id] = tier;
        }
        return fallback.Count;
    }

    /// <summary>True when every ingredient is another shape of the output's own material: a conversion, never a source.</summary>
    private static bool ReshufflesOwnShapes(
        PlannerRecipe recipe, string outputId, UnifiedItems unified, OrePrefixIndex prefixes)
    {
        var oredict = unified.PrimaryOredictOf(outputId);
        if (oredict is null || prefixes.Match(oredict) is not { } match)
        {
            return false;
        }
        var found = false;
        foreach (var (id, _) in recipe.Ingredients)
        {
            if (!IsShapeOf(id, match.Material, unified, prefixes))
            {
                return false;
            }
            found = true;
        }
        return found;
    }

    /// <summary>Any shape prefix with exactly the material behind it qualifies, piles and intermediates included.</summary>
    private static bool IsShapeOf(string id, string material, UnifiedItems unified, OrePrefixIndex prefixes) =>
        unified.OredictsOf(id).Any(
            oredict => prefixes.Match(oredict) is { } match
                && match.Material == material
                && match.Prefix.IsShape);

    /// <summary>A dust is the same material as its ingot or gem; it inherits that tier.</summary>
    private static void InheritTwinTiers(
        Dictionary<string, int> tiers, IReadOnlyDictionary<string, string> leafClasses, UnifiedItems unified)
    {
        foreach (var (id, leafClass) in leafClasses)
        {
            if (leafClass != "dust")
            {
                continue;
            }
            var oredict = unified.PrimaryOredictOf(id);
            if (oredict is null || !oredict.StartsWith("dust", StringComparison.Ordinal))
            {
                continue;
            }
            var material = oredict["dust".Length..];
            foreach (var twinOredict in new[] { "ingot" + material, "gem" + material })
            {
                if (unified.CanonicalByOredict.TryGetValue(twinOredict, out var twinId)
                    && tiers.TryGetValue(twinId, out var twinTier))
                {
                    tiers[id] = twinTier;
                    break;
                }
            }
        }
    }
}
