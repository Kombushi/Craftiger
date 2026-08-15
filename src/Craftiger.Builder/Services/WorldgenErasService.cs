using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;

namespace Craftiger.Builder.Services;

public sealed class WorldgenErasService(BuilderConfig config) : IWorldgenErasService
{
    public WorldgenEras Run(Dump dump, UnifiedItems unified)
    {
        var oreBlocks = new Dictionary<string, int>();
        var drops = new Dictionary<string, int>();

        var materialEras = new Dictionary<string, int>();
        foreach (var ore in dump.WorldgenOres)
        {
            if (DimensionEra(ore) is not { } era)
            {
                continue;
            }
            var target = ore.IsDrop ? drops : oreBlocks;
            Credit(target, unified.Canonical(ore.ItemId), era);
            if (ore.MaterialName is null)
            {
                continue;
            }
            Credit(materialEras, ore.MaterialName, era);

            // Space-stone variants are not oredicted; the material's ore* oredict reaches the canonical item.
            if (unified.CanonicalByOredict.TryGetValue("ore" + ore.MaterialName, out var oredicted))
            {
                Credit(target, oredicted, era);
            }
        }

        // Mining a placed block drops its rawOre* chunk at the same era.
        foreach (var (material, era) in materialEras)
        {
            if (unified.CanonicalByOredict.TryGetValue("rawOre" + material, out var chunk))
            {
                Credit(drops, chunk, era);
            }
        }

        // A drop's placed-block era already admits mining it; keep only the lower value.
        foreach (var (id, era) in oreBlocks)
        {
            if (drops.TryGetValue(id, out var dropEra) && era <= dropEra)
            {
                drops.Remove(id);
            }
        }

        var fluids = new Dictionary<string, int>();
        foreach (var fluid in dump.UndergroundFluids)
        {
            if (DimensionEra(fluid.DimensionAbbreviation, fluid.DimensionTier) is { } era)
            {
                Credit(fluids, fluid.FluidId, era);
            }
        }

        var materials = materialEras
            .Select(m => (m.Key, (int?)m.Value))
            .Concat(config.NonSpawningOres.Select(m => (m, (int?)null)))
            .OrderByDescending(m => m.Item1.Length)
            .ToList();
        return new WorldgenEras(oreBlocks, drops, materials, fluids);
    }

    private static void Credit(Dictionary<string, int> target, string id, int era)
    {
        if (!target.TryGetValue(id, out var current) || era < current)
        {
            target[id] = era;
        }
    }

    private int? DimensionEra(DumpWorldgenOre ore) =>
        DimensionEra(ore.DimensionAbbreviation, ore.DimensionTier);

    /// <summary>Unknown dimensions contribute nothing rather than wrongly lowering an era.</summary>
    private int? DimensionEra(string abbreviation, int rocketTier) =>
        config.DimensionEraOverrides.TryGetValue(abbreviation, out var overrideEra) ? overrideEra
        : config.DimensionTierEras.TryGetValue(rocketTier, out var era) ? era
        : null;
}
