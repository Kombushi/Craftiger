using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;

namespace Craftiger.Builder.Services;

public sealed class LeafTaggingService(BuilderConfig config) : ILeafTaggingService
{
    public Dictionary<string, string> Run(IEnumerable<string> canonicalIds, Dump dump, UnifiedItems unified)
    {
        // The primary oredict may hide the minable name (blockObsidian over obsidian).
        var oredictsByCanonical = new Dictionary<string, HashSet<string>>();
        foreach (var (name, members) in unified.MembersByOredict)
        {
            foreach (var member in members)
            {
                var canonical = unified.Canonical(member);
                if (!oredictsByCanonical.TryGetValue(canonical, out var set))
                {
                    oredictsByCanonical[canonical] = set = [];
                }
                set.Add(name);
            }
        }

        var classes = new Dictionary<string, string>();

        foreach (var id in canonicalIds)
        {
            if (dump.Fluids.TryGetValue(id, out var fluid))
            {
                if (config.FreeFluids.Contains(fluid.InternalName))
                {
                    classes[id] = "free_fluid";
                }
                continue;
            }

            if (config.MinableBlockItemIds.Contains(id))
            {
                classes[id] = "minable_block";
                continue;
            }

            var oredict = unified.PrimaryOredictByCanonical.GetValueOrDefault(id);
            if (oredict is null)
            {
                continue;
            }

            var leafClass = Classify(oredict, oredictsByCanonical.GetValueOrDefault(id));
            if (leafClass is not null)
            {
                classes[id] = leafClass;
            }
        }

        return classes;
    }

    private string? Classify(string oredict, HashSet<string>? allOredicts)
    {
        if (config.MinableBlockOredicts.Contains(oredict) ||
            (allOredicts is not null && config.MinableBlockOredicts.Any(allOredicts.Contains)))
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
