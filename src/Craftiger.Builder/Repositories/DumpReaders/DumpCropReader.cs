using Craftiger.Builder.Interfaces.DumpReaders;
using Craftiger.Builder.Models.Dump;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Craftiger.Builder.Repositories.DumpReaders;

public sealed class DumpCropReader(ILogger<DumpCropReader> logger) : IDumpCropReader
{
    public DumpCropSet Read(SqliteConnection db)
    {
        var blockDrops = new List<DumpBlockDrop>();
        foreach (var r in db.Query<(string Id, string BlockItemId, string DropItemId, int Quantity)>("""
            SELECT ID, BLOCK_ITEM_ID, DROP_ID, QUANTITY
            FROM BLOCK_DROP
            WHERE BLOCK_ITEM_ID IS NOT NULL AND DROP_ID IS NOT NULL AND QUANTITY > 0
            """))
        {
            blockDrops.Add(new DumpBlockDrop(r.Id, r.BlockItemId, r.DropItemId, r.Quantity));
        }

        var cropDrops = new Dictionary<string, List<DumpCropDrop>>();
        foreach (var (cropId, itemId, weight) in db.Query<(string, string, long)>(
            """SELECT CROPS_NH_CROP_ID, DROPS_ITEM_ID, DROPS_WEIGHT FROM CROPS_NH_CROP_DROPS"""))
        {
            DumpQueries.Add(cropDrops, cropId, new DumpCropDrop(itemId, (int)weight));
        }
        var cropUnderBlocks = new Dictionary<string, List<string>>();
        foreach (var (cropId, itemId) in db.Query<(string, string)>(
            """SELECT CROPS_NH_CROP_ID, UNDER_BLOCKS_ITEM_ID FROM CROPS_NH_CROP_UNDER_BLOCKS"""))
        {
            DumpQueries.Add(cropUnderBlocks, cropId, itemId);
        }
        var crops = new List<DumpCrop>();
        foreach (var r in db.Query<(string Id, string? SeedId, long Hidden, long Tier, long GrowthDuration, double DropChance, long MinSeedBed)>("""
            SELECT ID, SEED_ID, HIDDEN, TIER, GROWTH_DURATION, CAST(DROP_CHANCE AS REAL), MIN_SEED_BED_TIER FROM CROPS_NH_CROP
            """))
        {
            crops.Add(new DumpCrop(
                r.Id, r.SeedId, r.Hidden != 0,
                (int)r.Tier, r.GrowthDuration, r.DropChance, (int)r.MinSeedBed,
                cropDrops.GetValueOrDefault(r.Id) ?? [],
                cropUnderBlocks.GetValueOrDefault(r.Id) ?? []));
        }

        return new DumpCropSet(
            crops, blockDrops, ReadMobs(db), ReadMobDropsByMob(db),
            ReadFertilizers(db), ReadFluidFertilizers(db), ReadFarmComponents(db));
    }

    private static List<DumpFertilizer> ReadFertilizers(SqliteConnection db)
    {
        DumpQueries.RequireMachineData(db, "CROPS_NH_FERTILIZER_ITEM");
        return [.. db.Query<(string ItemId, long Potency)>("""
            SELECT ITEM_ID, POTENCY FROM CROPS_NH_FERTILIZER_ITEM
            """).Select(r => new DumpFertilizer(r.ItemId, (int)r.Potency))];
    }

    private static List<DumpFluidFertilizer> ReadFluidFertilizers(SqliteConnection db)
    {
        DumpQueries.RequireMachineData(db, "CROPS_NH_FERTILIZER_FLUID");
        return [.. db.Query<(string FluidId, long Potency)>("""
            SELECT FLUID_ID, POTENCY FROM CROPS_NH_FERTILIZER_FLUID
            """).Select(r => new DumpFluidFertilizer(r.FluidId, (int)r.Potency))];
    }

    private static List<DumpFarmComponent> ReadFarmComponents(SqliteConnection db)
    {
        DumpQueries.RequireMachineData(db, "CROPS_NH_FARM_COMPONENT");
        return [.. db.Query<(string ComponentClass, long Tier)>("""
            SELECT COMPONENT_CLASS, TIER FROM CROPS_NH_FARM_COMPONENT
            """).Select(r => new DumpFarmComponent(r.ComponentClass, (int)r.Tier))];
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ReadMobDropsByMob(SqliteConnection db)
    {
        var drops = new Dictionary<string, List<string>>();
        if (DumpQueries.HasTable(db, "MOB_INFO_DROPS"))
        {
            foreach (var (mobId, itemId) in db.Query<(string, string)>("""
                SELECT m.MOB_ID, d.DROPS_ITEM_ID
                FROM MOB_INFO_DROPS d
                JOIN MOB_INFO m ON m.ID = d.MOB_INFO_ID
                WHERE d.DROPS_ITEM_ID IS NOT NULL
                """))
            {
                DumpQueries.Add(drops, mobId, itemId);
            }
        }
        return DumpQueries.Freeze(drops);
    }

    private List<DumpMob> ReadMobs(SqliteConnection db)
    {
        if (!DumpQueries.HasTable(db, "MOB_INFO_DROPS"))
        {
            logger.LogWarning("dump has no mob info; no mob lines will ship");
            return [];
        }
        var drops = new Dictionary<string, List<DumpMobDrop>>();
        foreach (var r in db.Query<(string MobInfoId, string ItemId, double Probability, long StackSize, string Type)>("""
            SELECT MOB_INFO_ID, DROPS_ITEM_ID, CAST(DROPS_PROBABILITY AS REAL), DROPS_STACK_SIZE, DROPS_TYPE
            FROM MOB_INFO_DROPS
            WHERE DROPS_ITEM_ID IS NOT NULL AND DROPS_PROBABILITY > 0 AND DROPS_STACK_SIZE > 0
            """))
        {
            DumpQueries.Add(drops, r.MobInfoId, new DumpMobDrop(r.ItemId, r.Probability, (int)r.StackSize, r.Type));
        }
        var mobs = new List<DumpMob>();
        foreach (var r in db.Query<(string InfoId, string MobId, long SoulVial, long AlwaysInfernal, double Health)>("""
            SELECT i.ID, i.MOB_ID, i.SOUL_VIAL_USABLE, i.ALWAYS_INFERNAL, CAST(m.HEALTH AS REAL)
            FROM MOB_INFO i
            JOIN MOB m ON m.ID = i.MOB_ID
            """))
        {
            mobs.Add(new DumpMob(
                r.MobId, r.Health, r.SoulVial != 0, r.AlwaysInfernal != 0,
                drops.GetValueOrDefault(r.InfoId) ?? []));
        }
        return mobs;
    }
}
