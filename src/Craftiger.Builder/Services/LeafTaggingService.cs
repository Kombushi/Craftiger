using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

public sealed class LeafTaggingService(IOptions<WorldConfiguration> options, ILogger<LeafTaggingService> logger)
    : ILeafTaggingService
{
    /// <summary>Material leaf classes keyed by the oredict's exact GT prefix; intermediates are distinct prefixes and never match.</summary>
    private static readonly Dictionary<string, string> _classByPrefix = new()
    {
        ["dust"] = "dust",
        ["dustSmall"] = "dust_small",
        ["dustTiny"] = "dust_tiny",
        ["ingot"] = "ingot",
        ["gem"] = "gem",
        ["gemChipped"] = "gem_chipped",
        ["gemFlawed"] = "gem_flawed",
        ["gemFlawless"] = "gem_flawless",
        ["gemExquisite"] = "gem_exquisite",
        ["nugget"] = "nugget"
    };

    private readonly WorldConfiguration _config = options.Value;

    public IReadOnlyDictionary<string, string> Run(
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

            var oredict = unified.PrimaryOredictOf(id);
            var leafClass = oredict is null
                ? null
                : Classify(oredict, unified.OredictsOf(id), dump);

            // Farming is a leaf only where it is the one way in; most crop drops carry no oredict, so this comes last.
            leafClass ??= cropDrops.Contains(id) && !produced.Contains(id) ? "crop_drop" : null;
            if (leafClass is not null)
            {
                classes[id] = leafClass;
            }
        }

        return classes;
    }

    /// <summary>A placeholder weight would cap everything downstream, so an unpriceable leaf honestly falls back to its recipes.</summary>
    public IReadOnlyDictionary<string, string> Prune(
        IReadOnlyDictionary<string, string> classes, IReadOnlyDictionary<string, int> tiers, UnifiedItems unified,
        Dump dump)
    {
        var untiered = classes
            .Where(c => c.Value is "ingot" or "gem" or "dust" && !tiers.ContainsKey(c.Key))
            .Select(c => c.Key)
            .ToHashSet();
        var parentless = classes
            .Where(c => DerivedLeaf.ByClass.ContainsKey(c.Value)
                && !HasPricedParent(c.Key, c.Value, unified, dump, tiers))
            .Select(c => c.Key)
            .ToHashSet();

        logger.LogInformation(
            "  dropped {Untiered:N0} untiered and {Parentless:N0} parentless leaves",
            untiered.Count, parentless.Count);
        return classes
            .Where(c => !untiered.Contains(c.Key) && !parentless.Contains(c.Key))
            .ToDictionary(c => c.Key, c => c.Value);
    }

    /// <summary>The parent each fraction leaf divides its weight from, resolved the way pruning judged it.</summary>
    public IReadOnlyDictionary<string, ItemParent> Parents(
        IReadOnlyDictionary<string, string> classes, IReadOnlyDictionary<string, int> tiers,
        UnifiedItems unified, Dump dump)
    {
        var parents = new Dictionary<string, ItemParent>();
        foreach (var (id, leafClass) in classes)
        {
            var (parent, divisor) = DerivedLeaf.ParentsOf(id, leafClass, unified, dump.OrePrefixes)
                .FirstOrDefault(p => tiers.ContainsKey(p.ParentId));
            if (parent is not null)
            {
                parents[id] = new ItemParent(parent, divisor);
            }
        }
        return parents;
    }

    /// <summary>Weights that override the item's leaf class, by item id.</summary>
    public IReadOnlyDictionary<string, double> Overrides(Dump dump) =>
        dump.Fluids.Values
            .Where(f => _config.WorldFluids.ContainsKey(f.InternalName))
            .ToDictionary(f => f.Id, f => _config.WorldFluids[f.InternalName].Weight);

    private static bool HasPricedParent(
        string id, string leafClass, UnifiedItems unified, Dump dump,
        IReadOnlyDictionary<string, int> tiers) =>
        DerivedLeaf.ParentsOf(id, leafClass, unified, dump.OrePrefixes)
            .Any(p => tiers.ContainsKey(p.ParentId));

    private string? Classify(string oredict, IReadOnlySet<string> allOredicts, Dump dump)
    {
        if (_config.MinableBlockEras.ContainsKey(oredict) || allOredicts.Any(_config.MinableBlockEras.ContainsKey))
        {
            return "minable_block";
        }
        if (_config.FarmableOredictPrefixes.Any(p => oredict.StartsWith(p, StringComparison.Ordinal)))
        {
            return "farmable";
        }
        // Material classes need a name GT itself unifies: convention names like dustSpace* must not hand out material leaves.
        if (dump.UnifiedOredictTargets.ContainsKey(oredict)
            && dump.OrePrefixes.Match(oredict) is { } match
            && _classByPrefix.TryGetValue(match.Prefix.Name, out var materialClass))
        {
            return materialClass;
        }
        if (oredict.StartsWith("logWood", StringComparison.Ordinal))
        {
            return "log";
        }
        return null;
    }
}
