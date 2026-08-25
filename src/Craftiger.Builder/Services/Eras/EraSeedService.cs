using Craftiger.Builder.Interfaces.Eras;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Eras;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services.Eras;

/// <summary>Order matters here: the first seed of an item wins, and only seeds are immune to recipes.</summary>
public sealed class EraSeedService(
    IOptions<WorldConfiguration> world,
    ILogger<EraSeedService> logger) : IEraSeedService
{
    private static readonly HashSet<string> _worldOriginClasses = ["minable_block", "farmable", "log"];

    private readonly WorldConfiguration _world = world.Value;

    public EraTable Run(
        IReadOnlyDictionary<string, string> leafClasses, UnifiedItems unified, Dump dump, WorldgenEras worldgen)
    {
        var table = new EraTable();
        // Dusts are not seeds: one obtainable only by macerating its metal inherits the metal's era.
        foreach (var (id, leafClass) in leafClasses)
        {
            if (leafClass == "minable_block")
            {
                table.Seed(id, MinableEra(id, unified));
            }
            else if (_worldOriginClasses.Contains(leafClass))
            {
                table.Seed(id, 0);
            }
        }
        foreach (var (id, oredict) in unified.PrimaryOredictByCanonical)
        {
            if (!oredict.StartsWith("ore", StringComparison.Ordinal))
            {
                continue;
            }
            var seed = worldgen.OreBlocks.TryGetValue(id, out var blockEra)
                ? blockEra
                : worldgen.OredictSeed(oredict);
            if (seed is { } seedEra)
            {
                table.Seed(id, seedEra);
            }
        }
        foreach (var (id, blockEra) in worldgen.OreBlocks)
        {
            table.Seed(id, blockEra);
        }
        foreach (var fluid in dump.Fluids.Values)
        {
            // A null era means the fluid is pumped, and its own recipe decides when.
            if (_world.WorldFluids.TryGetValue(fluid.InternalName, out var worldFluid) && worldFluid.Era is { } free)
            {
                table.Seed(fluid.Id, free);
            }
        }

        // Mined small-ore drops start at their dimension era; recipes may still lower them.
        foreach (var (id, dropEra) in worldgen.Drops)
        {
            table.Lower(id, dropEra);
        }

        logger.LogInformation(
            "  {Seeds:N0} world-origin seeds, {Soft:N0} lowerable drops", table.SeedCount, table.Count - table.SeedCount);
        return table;
    }

    /// <summary>The cheapest world a block can be mined in, by item id or any of its oredicts.</summary>
    private int MinableEra(string id, UnifiedItems unified)
    {
        var cheapest = _world.MinableBlockEras.GetValueOrDefault(id, int.MaxValue);
        foreach (var oredict in unified.OredictsOf(id))
        {
            if (_world.MinableBlockEras.TryGetValue(oredict, out var era) && era < cheapest)
            {
                cheapest = era;
            }
        }
        return cheapest == int.MaxValue ? 0 : cheapest;
    }
}
