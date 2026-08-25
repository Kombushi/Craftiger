using Craftiger.Builder.Interfaces.DumpReaders;
using Craftiger.Builder.Models.Dump;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Craftiger.Builder.Repositories.DumpReaders;

public sealed class DumpWorldgenReader : IDumpWorldgenReader
{
    public DumpWorldgenSet Read(SqliteConnection db)
    {
        var worldgenOres = new List<DumpWorldgenOre>();
        foreach (var r in db.Query<(string ItemId, string? MaterialName, string Dimension, int Tier)>("""
            SELECT O.ORES_ITEM_ID, O.ORES_MATERIAL_NAME, D.ABBREVIATION, D.ROCKET_TIER
            FROM GREG_TECH_ORE_VEIN_ORES O
            JOIN GREG_TECH_ORE_VEIN V ON V.ID = O.GREG_TECH_ORE_VEIN_ID AND V.ENABLED_BY_DEFAULT != 0
            JOIN GREG_TECH_ORE_VEIN_DIMENSIONS VD ON VD.GREG_TECH_ORE_VEIN_ID = V.ID
            JOIN GREG_TECH_DIMENSION D ON D.ABBREVIATION = VD.DIMENSIONS_DIMENSION_ABBREVIATION
            JOIN GREG_TECH_DIMENSION_STONE_TYPES ST ON ST.GREG_TECH_DIMENSION_ID = D.ID AND ST.STONE_TYPES = O.ORES_STONE_TYPE
            """))
        {
            worldgenOres.Add(new DumpWorldgenOre(r.ItemId, r.MaterialName, r.Dimension, r.Tier, IsDrop: false));
        }
        foreach (var r in db.Query<(string ItemId, string? MaterialName, string Dimension, int Tier)>("""
            SELECT B.BLOCKS_ITEM_ID, S.MATERIAL_NAME, D.ABBREVIATION, D.ROCKET_TIER
            FROM GREG_TECH_SMALL_ORE_BLOCKS B
            JOIN GREG_TECH_SMALL_ORE S ON S.ID = B.GREG_TECH_SMALL_ORE_ID AND S.ENABLED_BY_DEFAULT != 0
            JOIN GREG_TECH_SMALL_ORE_DIMENSIONS SD ON SD.GREG_TECH_SMALL_ORE_ID = S.ID
            JOIN GREG_TECH_DIMENSION D ON D.ABBREVIATION = SD.DIMENSIONS_DIMENSION_ABBREVIATION
            JOIN GREG_TECH_DIMENSION_STONE_TYPES ST ON ST.GREG_TECH_DIMENSION_ID = D.ID AND ST.STONE_TYPES = B.BLOCKS_STONE_TYPE
            """))
        {
            worldgenOres.Add(new DumpWorldgenOre(r.ItemId, r.MaterialName, r.Dimension, r.Tier, IsDrop: false));
        }
        foreach (var r in db.Query<(string ItemId, string Dimension, int Tier)>("""
            SELECT P.DROPS_ITEM_ID, D.ABBREVIATION, D.ROCKET_TIER
            FROM GREG_TECH_SMALL_ORE_DROPS P
            JOIN GREG_TECH_SMALL_ORE S ON S.ID = P.GREG_TECH_SMALL_ORE_ID AND S.ENABLED_BY_DEFAULT != 0
            JOIN GREG_TECH_SMALL_ORE_DIMENSIONS SD ON SD.GREG_TECH_SMALL_ORE_ID = S.ID
            JOIN GREG_TECH_DIMENSION D ON D.ABBREVIATION = SD.DIMENSIONS_DIMENSION_ABBREVIATION
            """))
        {
            worldgenOres.Add(new DumpWorldgenOre(r.ItemId, MaterialName: null, r.Dimension, r.Tier, IsDrop: true));
        }

        var undergroundFluids = new List<DumpUndergroundFluid>();
        foreach (var r in db.Query<(string FluidId, string Dimension, int Tier)>("""
            SELECT F.FLUID_ID, D.ABBREVIATION, D.ROCKET_TIER
            FROM GREG_TECH_UNDERGROUND_FLUID F
            JOIN GREG_TECH_UNDERGROUND_FLUID_DIMENSIONS FD ON FD.GREG_TECH_UNDERGROUND_FLUID_ID = F.ID
            JOIN GREG_TECH_DIMENSION D ON D.ABBREVIATION = FD.DIMENSIONS_DIMENSION_ABBREVIATION
            WHERE F.FLUID_ID IS NOT NULL
            """))
        {
            undergroundFluids.Add(new DumpUndergroundFluid(r.FluidId, r.Dimension, r.Tier));
        }

        return new DumpWorldgenSet(worldgenOres, undergroundFluids);
    }
}
