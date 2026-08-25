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

        var cropDrops = new Dictionary<string, List<string>>();
        foreach (var (cropId, itemId) in db.Query<(string, string)>(
            """SELECT CROPS_NH_CROP_ID, DROPS_ITEM_ID FROM CROPS_NH_CROP_DROPS"""))
        {
            DumpQueries.Add(cropDrops, cropId, itemId);
        }
        var cropUnderBlocks = new Dictionary<string, List<string>>();
        foreach (var (cropId, itemId) in db.Query<(string, string)>(
            """SELECT CROPS_NH_CROP_ID, UNDER_BLOCKS_ITEM_ID FROM CROPS_NH_CROP_UNDER_BLOCKS"""))
        {
            DumpQueries.Add(cropUnderBlocks, cropId, itemId);
        }
        var crops = new List<DumpCrop>();
        foreach (var r in db.Query<(string Id, string CropId, string Name, string? SeedId, long Hidden)>("""
            SELECT ID, CROP_ID, NAME, SEED_ID, HIDDEN FROM CROPS_NH_CROP
            """))
        {
            crops.Add(new DumpCrop(
                r.Id, r.CropId, r.Name, r.SeedId, r.Hidden != 0,
                cropDrops.GetValueOrDefault(r.Id) ?? [],
                cropUnderBlocks.GetValueOrDefault(r.Id) ?? []));
        }

        return new DumpCropSet(crops, blockDrops, ReadMobDropItemIds(db));
    }

    private HashSet<string> ReadMobDropItemIds(SqliteConnection db)
    {
        if (!DumpQueries.HasTable(db, "MOB_INFO_DROPS"))
        {
            logger.LogWarning("dump has no mob drops; no mob-farm seeds will ship");
            return [];
        }
        return [.. db.Query<string>("""
            SELECT DISTINCT d.DROPS_ITEM_ID
            FROM MOB_INFO_DROPS d
            JOIN MOB_INFO m ON m.ID = d.MOB_INFO_ID
            WHERE m.SOUL_VIAL_USABLE = 1 AND d.DROPS_ITEM_ID IS NOT NULL
            """)];
    }
}
