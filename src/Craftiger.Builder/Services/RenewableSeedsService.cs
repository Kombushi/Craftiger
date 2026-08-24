using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Craftiger.Builder.Models.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

/// <summary>Marks the auto-infinite primitives. Derivation through recipe chains is the
/// solver's job at run time; only the seeds ship, so the classification can never claim a
/// garage-dependent fact.</summary>
public sealed class RenewableSeedsService(
    IOptions<RenewableSeedsConfiguration> config,
    ILogger<RenewableSeedsService> logger) : IRenewableSeedsService
{
    private static readonly HashSet<string> FarmLeafClasses = ["log", "crop_drop", "farmable"];

    private readonly RenewableSeedsConfiguration _config = config.Value;

    public IReadOnlyList<PlannerRenewableSeed> Run(
        Dump dump, UnifiedItems unified, IReadOnlyDictionary<string, string> leafClasses,
        IReadOnlySet<string> itemIds)
    {
        var seeds = new Dictionary<string, PlannerRenewableSeed>();

        foreach (var name in _config.WorldSeedNames)
        {
            var found = false;
            foreach (var itemId in NamedIds(dump, name).Select(unified.Canonical))
            {
                if (itemIds.Contains(itemId))
                {
                    seeds.TryAdd(itemId, new PlannerRenewableSeed(itemId, "WORLD"));
                    found = true;
                }
            }
            if (!found)
            {
                logger.LogWarning("world seed '{Name}' matches no reachable item", name);
            }
        }

        foreach (var (itemId, leafClass) in leafClasses)
        {
            if (FarmLeafClasses.Contains(leafClass))
            {
                seeds.TryAdd(itemId, new PlannerRenewableSeed(itemId, "FARM"));
            }
        }

        foreach (var itemId in dump.MobDropItemIds.Select(unified.Canonical))
        {
            if (itemIds.Contains(itemId))
            {
                seeds.TryAdd(itemId, new PlannerRenewableSeed(itemId, "MOB"));
            }
        }

        return [.. seeds.Values];
    }

    private static IEnumerable<string> NamedIds(Dump dump, string name) =>
        dump.ItemIdsNamed(name)
            .Concat(dump.Fluids.Values.Where(f => f.Name == name).Select(f => f.Id));
}
