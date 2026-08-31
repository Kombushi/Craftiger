using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

/// <summary>Only the configured world seeds ship: crops, farmables and mob drops cost farm lines instead.</summary>
public sealed class RenewableSeedsService(IOptions<RenewableSeedsConfiguration> config, ILogger<RenewableSeedsService> logger) : IRenewableSeedsService
{
    private readonly RenewableSeedsConfiguration _config = config.Value;

    public IReadOnlyList<PlannerRenewableSeed> Run(Dump dump, UnifiedItems unified, IReadOnlySet<string> itemIds)
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

        return [.. seeds.Values];
    }
}
