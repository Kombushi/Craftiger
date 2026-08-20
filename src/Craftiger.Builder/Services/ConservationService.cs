using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Craftiger.Builder.Models.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

/// <summary>Matter-conservation prune. A recipe that takes one kind of item apart into
/// nothing but material shapes is a claim about how much matter that item holds; when
/// every accountable route to the item carries less than the recipe hands out, the claim
/// is false and the recipe is amplifying reverse-crafting. Conservation is of volume, not
/// identity — GT transmutes freely (alloying, implosion residue, stone byproducts), so
/// outputs may be materials the inputs never contained as long as the total adds up.
/// Anything unprovable is innocent: world-obtained items, containers, farmables, fluids,
/// and items with no accountable producer all stay.</summary>
public sealed class ConservationService(
    IOptions<WorldConfiguration> options,
    ILogger<ConservationService> logger) : IConservationService
{
    private const double Tolerance = 1e-9;

    /// <summary>Fluids count at molten density, 144 L to the ingot — the most generous
    /// reading, so an arc's whiff of noble gas cannot launder an amplifier while a full
    /// molten measure honestly carries its matter.</summary>
    private const double MatterPerLiter = 3628800.0 / 144;

    private readonly WorldConfiguration _config = options.Value;

    public List<PlannerRecipe> Run(List<PlannerRecipe> recipes, Dump dump, UnifiedItems unified)
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

    private bool Amplifies(
        PlannerRecipe recipe, Dump dump, UnifiedItems unified, HashSet<string> world,
        Dictionary<string, List<PlannerRecipe>> producers)
    {
        if (recipe.EraOnly || recipe.Choices.Count > 0 || recipe.Outputs.Count == 0)
        {
            return false;
        }
        // Reverse-crafting takes exactly one kind of item apart; recipes mixing several
        // ingredients are production, however lopsided the matter — the primitive blast
        // furnace really does boost two dusts and coke into three ingots.
        var items = recipe.Inputs.Where(i => !dump.Fluids.ContainsKey(i.Key)).ToList();
        if (items.Count != 1)
        {
            return false;
        }

        var claimed = 0.0;
        foreach (var output in recipe.Outputs)
        {
            // A fluid out only lowers the claim, so ignoring it never condemns wrongly.
            if (dump.Fluids.ContainsKey(output.ItemId))
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
            .Where(i => dump.Fluids.ContainsKey(i.Key))
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

    /// <summary>The least total matter any fully accountable route puts into one unit of
    /// the item, or null when no route is accountable — an unprovable content is not zero.</summary>
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
                if (!dump.Fluids.ContainsKey(inputId) && Matter(inputId, dump, unified) is { } matter)
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

    /// <summary>The matter in an item. GT's per-item composition record is the truth — a
    /// quartz block holds four gems, not the block prefix's nine-ingot default — and the
    /// shape prefix amount is the fallback for shapes GT records no data for.</summary>
    private static double? Matter(string itemId, Dump dump, UnifiedItems unified) =>
        dump.ItemData.Content(itemId) ?? ShapePrefixAmount(itemId, dump, unified);

    private static long? ShapePrefixAmount(string itemId, Dump dump, UnifiedItems unified)
    {
        foreach (var oredict in (unified.OredictsByCanonical.GetValueOrDefault(itemId) ?? []).Order(StringComparer.Ordinal))
        {
            if (dump.OrePrefixes.Match(oredict) is { } match
                && OrePrefixIndex.IsShape(match.Prefix) && match.Prefix.MaterialAmount > 0)
            {
                return match.Prefix.MaterialAmount;
            }
        }
        return null;
    }

    private static bool IsShape(string itemId, Dump dump, UnifiedItems unified) =>
        (unified.OredictsByCanonical.GetValueOrDefault(itemId) ?? [])
            .Any(dump.OrePrefixes.IsMaterialShape)
        || (dump.ItemData.PrefixOf(itemId) is { } prefix && dump.OrePrefixes.IsShapeName(prefix));

    private bool IsFarmable(string itemId, UnifiedItems unified) =>
        (unified.OredictsByCanonical.GetValueOrDefault(itemId) ?? [])
            .Any(o => _config.FarmableOredictPrefixes.Any(p => o.StartsWith(p, StringComparison.Ordinal)));

    private static bool IsContainer(string itemId, Dump dump, UnifiedItems unified) =>
        (unified.OredictsByCanonical.GetValueOrDefault(itemId) ?? [])
            .Any(o => dump.OrePrefixes.Match(o) is { Prefix.Container: true });

    /// <summary>Items the world hands over: what a recipe takes from these is primary
    /// production, however lopsided the matter looks — water and lava really do make
    /// cobblestone.</summary>
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
        // Any placed block drops itself when broken; only drops of world-minable blocks
        // are the world handing something over.
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
                world.Add(unified.Canonical(drop));
            }
        }
        return world;
    }
}
