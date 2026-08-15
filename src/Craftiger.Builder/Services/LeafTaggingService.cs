using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;

namespace Craftiger.Builder.Services;

public sealed class LeafTaggingService(BuilderConfig config) : ILeafTaggingService
{
    public Dictionary<string, string> Run(IEnumerable<string> canonicalIds, Dump dump, UnifiedItems unified)
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
                if (config.WorldFluids.ContainsKey(fluid.InternalName))
                {
                    classes[id] = "world_fluid";
                }
                continue;
            }

            if (config.MinableBlockEras.ContainsKey(id))
            {
                classes[id] = "minable_block";
                continue;
            }

            var oredict = unified.PrimaryOredictByCanonical.GetValueOrDefault(id);
            var leafClass = oredict is null
                ? null
                : Classify(oredict, unified.OredictsByCanonical.GetValueOrDefault(id));

            // Most crop drops carry no oredict at all, so they are classified last.
            leafClass ??= cropDrops.Contains(id) ? "crop_drop" : null;
            if (leafClass is not null)
            {
                classes[id] = leafClass;
            }
        }

        return classes;
    }

    private string? Classify(string oredict, HashSet<string>? allOredicts)
    {
        if (config.MinableBlockEras.ContainsKey(oredict) ||
            (allOredicts is not null && allOredicts.Any(config.MinableBlockEras.ContainsKey)))
        {
            return "minable_block";
        }
        if (config.FarmableOredictPrefixes.Any(p => oredict.StartsWith(p, StringComparison.Ordinal)))
        {
            return "farmable";
        }
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
        if (oredict.StartsWith("logWood", StringComparison.Ordinal))
        {
            return "log";
        }
        return null;
    }
}
