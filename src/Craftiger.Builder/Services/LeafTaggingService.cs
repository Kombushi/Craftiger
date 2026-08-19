using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

public sealed class LeafTaggingService(IOptions<BuilderConfig> options, ILogger<LeafTaggingService> logger)
    : ILeafTaggingService
{
    private readonly BuilderConfig _config = options.Value;

    public Dictionary<string, string> Run(
        IEnumerable<string> canonicalIds, IReadOnlySet<string> produced, Dump dump, UnifiedItems unified)
    {
        var cropDrops = dump.Crops
            .Where(c => !c.Hidden)
            .SelectMany(c => c.Drops)
            .Select(unified.Canonical)
            .ToHashSet();
        var classes = new Dictionary<string, string>();

        foreach (var id in canonicalIds)
        {
            if (dump.Fluids.TryGetValue(id, out var fluid))
            {
                if (_config.WorldFluids.ContainsKey(fluid.InternalName))
                {
                    classes[id] = "world_fluid";
                }
                continue;
            }

            if (_config.MinableBlockEras.ContainsKey(id))
            {
                classes[id] = "minable_block";
                continue;
            }

            var oredict = unified.PrimaryOredictByCanonical.GetValueOrDefault(id);
            if (oredict is not null && IsIntermediate(oredict))
            {
                continue;
            }

            var leafClass = oredict is null
                ? null
                : Classify(oredict, unified.OredictsByCanonical.GetValueOrDefault(id), dump);

            // Farming is a leaf only where it is the one way in; anything a recipe also makes
            // is priced from that recipe. Most crop drops carry no oredict, so this comes last.
            leafClass ??= cropDrops.Contains(id) && !produced.Contains(id) ? "crop_drop" : null;
            if (leafClass is not null)
            {
                classes[id] = leafClass;
            }
        }

        return classes;
    }

    /// <summary>Drops leaves whose weight cannot be worked out: a tiered material the era solve
    /// never reached, or a fraction of a parent that is not itself priced. They fall back to
    /// their recipes, which is honest — a placeholder weight would cap everything downstream.</summary>
    public void Prune(
        Dictionary<string, string> classes, IReadOnlyDictionary<string, int> tiers, UnifiedItems unified)
    {
        var untiered = classes
            .Where(c => c.Value is "ingot" or "gem" or "dust" && !tiers.ContainsKey(c.Key))
            .Select(c => c.Key)
            .ToList();
        var parentless = classes
            .Where(c => DerivedLeaf.ByClass.ContainsKey(c.Value) && !HasPricedParent(c.Key, c.Value, unified, tiers))
            .Select(c => c.Key)
            .ToList();

        foreach (var id in untiered.Concat(parentless))
        {
            classes.Remove(id);
        }

        logger.LogInformation(
            "  dropped {Untiered:N0} untiered and {Parentless:N0} parentless leaves",
            untiered.Count, parentless.Count);
    }

    /// <summary>The parent each surviving fraction leaf divides its weight from, resolved the
    /// same way pruning judged it, so a shipped fraction always names a priced parent.</summary>
    public Dictionary<string, ItemParent> Parents(
        IReadOnlyDictionary<string, string> classes, IReadOnlyDictionary<string, int> tiers,
        UnifiedItems unified)
    {
        var parents = new Dictionary<string, ItemParent>();
        foreach (var (id, leafClass) in classes)
        {
            if (!DerivedLeaf.ByClass.TryGetValue(leafClass, out var derived))
            {
                continue;
            }

            var parent = DerivedLeaf.ParentsOf(id, leafClass, unified).FirstOrDefault(tiers.ContainsKey);
            if (parent is not null)
            {
                parents[id] = new ItemParent(parent, derived.Divisor);
            }
        }
        return parents;
    }

    /// <summary>Weights that override the item's leaf class, by item id.</summary>
    public Dictionary<string, double> Overrides(Dump dump) =>
        dump.Fluids.Values
            .Where(f => _config.WorldFluids.ContainsKey(f.InternalName))
            .ToDictionary(f => f.Id, f => _config.WorldFluids[f.InternalName].Weight);

    private static bool HasPricedParent(
        string id, string leafClass, UnifiedItems unified, IReadOnlyDictionary<string, int> tiers) =>
        DerivedLeaf.ParentsOf(id, leafClass, unified).Any(tiers.ContainsKey);

    private bool IsIntermediate(string oredict) =>
        _config.IntermediateOredictPrefixes.Any(p => oredict.StartsWith(p, StringComparison.Ordinal));

    private string? Classify(string oredict, HashSet<string>? allOredicts, Dump dump)
    {
        if (_config.MinableBlockEras.ContainsKey(oredict) ||
            (allOredicts is not null && allOredicts.Any(_config.MinableBlockEras.ContainsKey)))
        {
            return "minable_block";
        }
        if (_config.FarmableOredictPrefixes.Any(p => oredict.StartsWith(p, StringComparison.Ordinal)))
        {
            return "farmable";
        }
        // Material classes need a name GT itself unifies: convention names that merely start
        // with a material prefix (dustSpace*) must not hand their members a material leaf.
        if (dump.UnifiedOredictTargets.ContainsKey(oredict))
        {
            if (oredict.StartsWith("dustSmall", StringComparison.Ordinal))
            {
                return "dust_small";
            }
            if (oredict.StartsWith("dustTiny", StringComparison.Ordinal))
            {
                return "dust_tiny";
            }
            if (oredict.StartsWith("dust", StringComparison.Ordinal))
            {
                return "dust";
            }
            if (oredict.StartsWith("ingot", StringComparison.Ordinal))
            {
                return "ingot";
            }
            if (oredict.StartsWith("gem", StringComparison.Ordinal))
            {
                return "gem";
            }
            if (oredict.StartsWith("nugget", StringComparison.Ordinal))
            {
                return "nugget";
            }
        }
        if (oredict.StartsWith("logWood", StringComparison.Ordinal))
        {
            return "log";
        }
        return null;
    }
}