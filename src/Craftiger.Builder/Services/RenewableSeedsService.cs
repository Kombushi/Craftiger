using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

/// <summary>Only the seeds ship, so the classification can never claim a garage-dependent fact.</summary>
public sealed class RenewableSeedsService(
    IOptions<RenewableSeedsConfiguration> config,
    ILogger<RenewableSeedsService> logger) : IRenewableSeedsService
{
    private static readonly HashSet<string> _farmLeafClasses = ["log", "crop_drop", "farmable"];

    private readonly RenewableSeedsConfiguration _config = config.Value;

    public IReadOnlyList<PlannerRenewableSeed> Run(
        Dump dump, UnifiedItems unified, IReadOnlyDictionary<string, string> leafClasses,
        IReadOnlySet<string> itemIds)
    {
        var seeds = new Dictionary<string, PlannerRenewableSeed>();

        foreach (var name in _config.WorldSeedNames)
        {
            var found = false;
            foreach (var itemId in dump.ItemIdsNamed(name).Concat(dump.FluidIdsNamed(name)).Select(unified.Canonical))
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
            if (_farmLeafClasses.Contains(leafClass))
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
}
