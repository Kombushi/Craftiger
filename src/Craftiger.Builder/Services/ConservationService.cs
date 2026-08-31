using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

/// <summary>Conservation is of volume, not identity: outputs may be any material as long as the total adds up, and anything unprovable stays.</summary>
public sealed class ConservationService(
    IOptions<WorldConfiguration> options,
    ILogger<ConservationService> logger) : IConservationService
{
    private const double Tolerance = 1e-9;

    /// <summary>Fluids count at molten density, 144 L to the ingot: a whiff of gas cannot launder an amplifier.</summary>
    private const double MatterPerLiter = 3628800.0 / 144;

    private readonly WorldConfiguration _config = options.Value;

    public List<PlannerRecipe> Run(IReadOnlyList<PlannerRecipe> recipes, Dump dump, UnifiedItems unified)
    {
        var world = WorldObtainable(dump, unified);
        var producers = new Dictionary<string, List<PlannerRecipe>>();
        // Era-only recipes gate progression without honest matter, so they never vouch content.
        foreach (var recipe in recipes.Where(r => !r.EraOnly))
        {
            foreach (var output in recipe.Outputs.DistinctBy(o => o.ItemId))
            {
                if (!producers.TryGetValue(output.ItemId, out var list))
                {
                    producers[output.ItemId] = list = [];
                }
                list.Add(recipe);
            }
        }

        var kept = new List<PlannerRecipe>(recipes.Count);
        foreach (var recipe in recipes)
        {
            if (Amplifies(recipe, dump, unified, world, producers))
            {
                logger.LogInformation("  dropped {Machine} recipe {Id}: {Inputs} -> {Outputs}",
                    recipe.Machine, recipe.Id,
                    string.Join(" + ", recipe.Inputs.Select(i => $"{i.Value}x {dump.NameOf(i.Key)}")),
                    string.Join(" + ", recipe.Outputs.Select(o => $"{o.Amount}x {dump.NameOf(o.ItemId)}")));
                continue;
            }
            kept.Add(recipe);
        }
        if (kept.Count < recipes.Count)
        {
            logger.LogInformation("  {Dropped} amplifying recipes dropped", recipes.Count - kept.Count);
        }
        return kept;
    }

    /// <summary>Reverse-crafting takes exactly one kind of item apart; multi-ingredient recipes are production, however lopsided.</summary>
    private bool Amplifies(
        PlannerRecipe recipe, Dump dump, UnifiedItems unified, HashSet<string> world,
        Dictionary<string, List<PlannerRecipe>> producers)
    {
        if (recipe.EraOnly || recipe.Choices.Count > 0 || recipe.Outputs.Count == 0)
        {
            return false;
        }
        var items = recipe.Inputs.Where(i => !dump.IsFluid(i.Key)).ToList();
        if (items.Count != 1)
        {
            return false;
        }

        var claimed = 0.0;
        foreach (var output in recipe.Outputs)
        {
            // A fluid out only lowers the claim, so ignoring it never condemns wrongly.
            if (dump.IsFluid(output.ItemId))
            {
                continue;
            }
            if (!IsShape(output.ItemId, dump, unified) || Matter(output.ItemId, dump, unified) is not { } matter)
            {
                return false;
            }
            claimed += matter * output.Amount * output.Chance;
        }

        var available = recipe.Inputs
            .Where(i => dump.IsFluid(i.Key))
            .Sum(i => i.Value * MatterPerLiter);
        var (itemId, count) = items[0];
        if (IsShape(itemId, dump, unified))
        {
            return false;
        }
        if (Matter(itemId, dump, unified) is { } content)
        {
            available += content * count;
        }
        else if (world.Contains(itemId) || IsFarmable(itemId, unified) || IsContainer(itemId, dump, unified))
        {
            return false;
        }
        else if (ProducerBound(itemId, recipe, dump, unified, producers) is { } bound)
        {
            available += bound * count;
        }
        else
        {
            return false;
        }

        return claimed > available * (1 + Tolerance) + Tolerance;
    }

    /// <summary>The least matter any fully accountable route puts into one unit of the item; null when none is accountable.</summary>
    private static double? ProducerBound(
        string itemId, PlannerRecipe candidate, Dump dump, UnifiedItems unified,
        Dictionary<string, List<PlannerRecipe>> producers)
    {
        double? bound = null;
        foreach (var producer in producers.GetValueOrDefault(itemId) ?? [])
        {
            if (producer == candidate || producer.Choices.Count > 0 || producer.Inputs.ContainsKey(itemId))
            {
                continue;
            }

            var content = 0.0;
            var accountable = true;
            foreach (var (inputId, count) in producer.Inputs)
            {
                // A fluid into a producer carries unknown matter, so the route cannot vouch.
                if (!dump.IsFluid(inputId) && Matter(inputId, dump, unified) is { } matter)
                {
                    content += matter * count;
                }
                else
                {
                    accountable = false;
                    break;
                }
            }
            if (!accountable)
            {
                continue;
            }

            var made = producer.Outputs.Where(o => o.ItemId == itemId).Sum(o => o.Amount * o.Chance);
            if (made <= 0)
            {
                continue;
            }
            bound = Math.Min(bound ?? double.MaxValue, content / made);
        }
        return bound;
    }

    /// <summary>GT's per-item composition record is the truth; the shape prefix amount is the fallback.</summary>
    private static double? Matter(string itemId, Dump dump, UnifiedItems unified) =>
        dump.ItemData.Content(itemId) ?? ShapePrefixAmount(itemId, dump, unified);

    private static long? ShapePrefixAmount(string itemId, Dump dump, UnifiedItems unified)
    {
        foreach (var oredict in unified.OredictsOf(itemId).Order(StringComparer.Ordinal))
        {
            if (dump.OrePrefixes.Match(oredict) is { } match
                && match.Prefix.IsShape && match.Prefix.MaterialAmount > 0)
            {
                return match.Prefix.MaterialAmount;
            }
        }
        return null;
    }

    private static bool IsShape(string itemId, Dump dump, UnifiedItems unified) =>
        unified.OredictsOf(itemId).Any(dump.OrePrefixes.IsMaterialShape)
        || (dump.ItemData.PrefixOf(itemId) is { } prefix && dump.OrePrefixes.IsShapeName(prefix));

    private bool IsFarmable(string itemId, UnifiedItems unified) =>
        unified.OredictsOf(itemId)
            .Any(o => _config.FarmableOredictPrefixes.Any(p => o.StartsWith(p, StringComparison.Ordinal)));

    private static bool IsContainer(string itemId, Dump dump, UnifiedItems unified) =>
        unified.OredictsOf(itemId)
            .Any(o => dump.OrePrefixes.Match(o) is { Prefix.Container: true });

    /// <summary>Items the world hands over: taking from these is primary production, however lopsided the matter.</summary>
    private HashSet<string> WorldObtainable(Dump dump, UnifiedItems unified)
    {
        var world = new HashSet<string>();
        foreach (var key in _config.MinableBlockEras.Keys)
        {
            world.Add(unified.Canonical(key));
            if (unified.CanonicalByOredict.TryGetValue(key, out var canonical))
            {
                world.Add(canonical);
            }
        }
        // Any placed block drops itself; only drops of world-minable blocks are the world handing something over.
        foreach (var drop in dump.BlockDrops.Where(d => world.Contains(unified.Canonical(d.BlockItemId))))
        {
            world.Add(unified.Canonical(drop.DropItemId));
        }
        foreach (var ore in dump.WorldgenOres)
        {
            world.Add(unified.Canonical(ore.ItemId));
        }
        foreach (var crop in dump.Crops.Where(c => !c.Hidden))
        {
            foreach (var drop in crop.Drops)
            {
                world.Add(unified.Canonical(drop.ItemId));
            }
        }
        return world;
    }
}
