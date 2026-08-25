using Dapper;
using Microsoft.Data.Sqlite;

namespace Craftiger.Builder.UnitTests;

/// <summary>What the world provides: block drops, crops, pumpable fluids, ore veins per dimension, and mob drops.</summary>
public static partial class FixtureDump
{
    private static void AddWorldgen(SqliteConnection db)
    {
        BlockDrop(db, "minecraft:clay", ClayBlock, ClayBall, 4);
        BlockDrop(db, "minecraft:obsidian", ObsidianBlock, ObsidianBlock, 1);

        // A berry grows only on naquadah ore, so harvesting it waits for Mars.
        Crop(db, "naqBerry", "Naquadah Oreberry", BerrySeed, hidden: false, drops: [Berry], underBlocks: [NaqOreMars]);
        Crop(db, "weed", "Weed", WeedSeed, hidden: true, drops: [Weed], underBlocks: []);
        // Clay balls are also farmed, but breaking the block already prices them.
        Crop(db, "clayCrop", "Clay Crop", BerrySeed, hidden: false, drops: [ClayBall], underBlocks: []);

        // Oil lies in the Overworld, but only a drilling rig gets it out.
        db.Execute("INSERT INTO GREG_TECH_UNDERGROUND_FLUID(ID, FLUID_NAME, FLUID_ID) VALUES ('gtuf~oil', 'oil', @id)", new { id = Oil });
        db.Execute("INSERT INTO GREG_TECH_UNDERGROUND_FLUID_DIMENSIONS(GREG_TECH_UNDERGROUND_FLUID_ID, DIMENSIONS_DIMENSION_ABBREVIATION, DIMENSIONS_MAX_AMOUNT, DIMENSIONS_MIN_AMOUNT, DIMENSIONS_PROBABILITY) VALUES ('gtuf~oil', 'Ow', 100, 0, 1.0)");

        // Naquadah seeds at the Mars-tier era via a vein placing an un-oredicted
        // variant; the disabled Overworld vein must not drag it to era 0.
        db.Execute("INSERT INTO GREG_TECH_DIMENSION VALUES ('gtdim~Ow', 'Ow', 'Overworld', 'overworld', 0)");
        db.Execute("INSERT INTO GREG_TECH_DIMENSION VALUES ('gtdim~Ma', 'Ma', 'GalacticraftMars_Mars', 'mars', 2)");
        db.Execute("INSERT INTO GREG_TECH_DIMENSION_STONE_TYPES VALUES ('gtdim~Ow', 'Stone')");
        db.Execute("INSERT INTO GREG_TECH_DIMENSION_STONE_TYPES VALUES ('gtdim~Ma', 'Mars')");
        db.Execute("INSERT INTO GREG_TECH_ORE_VEIN VALUES ('gtov~ore.mix.naq', 5, 1, 'Naquadah', 60, 10, 24, 'ore.mix.naq', 40)");
        db.Execute("INSERT INTO GREG_TECH_ORE_VEIN_DIMENSIONS VALUES ('gtov~ore.mix.naq', 'Ma', 60, 10, 1.0)");
        db.Execute($"INSERT INTO GREG_TECH_ORE_VEIN_ORES VALUES ('gtov~ore.mix.naq', '{NaqOreMars}', 'Naquadah', 'Mars', 'PRIMARY')");
        db.Execute("INSERT INTO GREG_TECH_ORE_VEIN VALUES ('gtov~ore.mix.off', 5, 0, 'Naquadah', 60, 10, 24, 'ore.mix.off', 40)");
        db.Execute("INSERT INTO GREG_TECH_ORE_VEIN_DIMENSIONS VALUES ('gtov~ore.mix.off', 'Ow', 60, 10, 1.0)");
        db.Execute($"INSERT INTO GREG_TECH_ORE_VEIN_ORES VALUES ('gtov~ore.mix.off', '{NaqOre}', 'Naquadah', 'Stone', 'PRIMARY')");

        // Copper dust drops from a Mars small ore, but the Overworld ore route must win.
        db.Execute("INSERT INTO GREG_TECH_SMALL_ORE VALUES ('gtso~ore.small.cu', 8, 1, 'Copper', 40, 20, 'ore.small.cu')");
        db.Execute("INSERT INTO GREG_TECH_SMALL_ORE_DIMENSIONS VALUES ('gtso~ore.small.cu', 'Ma', 1.0)");
        db.Execute($"INSERT INTO GREG_TECH_SMALL_ORE_DROPS VALUES ('gtso~ore.small.cu', '{CopperDust}')");

        // Ordinary Overworld veins: without one, a material the world never places gets no era.
        // Runite's placed block is un-oredicted; its mined rawOre* chunk carries the vein era.
        db.Execute("INSERT INTO GREG_TECH_ORE_VEIN VALUES ('gtov~ore.mix.alu', 5, 1, 'Bauxite', 60, 10, 24, 'ore.mix.alu', 40)");
        db.Execute("INSERT INTO GREG_TECH_ORE_VEIN_DIMENSIONS VALUES ('gtov~ore.mix.alu', 'Ow', 60, 10, 1.0)");
        db.Execute($"INSERT INTO GREG_TECH_ORE_VEIN_ORES VALUES ('gtov~ore.mix.alu', '{AluOre}', 'Aluminium', 'Stone', 'PRIMARY')");
        db.Execute("INSERT INTO GREG_TECH_ORE_VEIN VALUES ('gtov~ore.mix.cu', 5, 1, 'Copper', 60, 10, 24, 'ore.mix.cu', 40)");
        db.Execute("INSERT INTO GREG_TECH_ORE_VEIN_DIMENSIONS VALUES ('gtov~ore.mix.cu', 'Ow', 60, 10, 1.0)");
        db.Execute($"INSERT INTO GREG_TECH_ORE_VEIN_ORES VALUES ('gtov~ore.mix.cu', '{CopperOre}', 'Copper', 'Stone', 'PRIMARY')");
        db.Execute("INSERT INTO GREG_TECH_ORE_VEIN VALUES ('gtov~ore.mix.gem', 5, 1, 'Gemium', 60, 10, 24, 'ore.mix.gem', 40)");
        db.Execute("INSERT INTO GREG_TECH_ORE_VEIN_DIMENSIONS VALUES ('gtov~ore.mix.gem', 'Ow', 60, 10, 1.0)");
        db.Execute($"INSERT INTO GREG_TECH_ORE_VEIN_ORES VALUES ('gtov~ore.mix.gem', '{GemOre}', 'Gemium', 'Stone', 'PRIMARY')");
        db.Execute("INSERT INTO GREG_TECH_ORE_VEIN VALUES ('gtov~ore.mix.runite', 3, 1, 'Runite', 50, 10, 16, 'ore.mix.runite', 20)");
        db.Execute("INSERT INTO GREG_TECH_ORE_VEIN_DIMENSIONS VALUES ('gtov~ore.mix.runite', 'Ma', 50, 10, 1.0)");
        db.Execute($"INSERT INTO GREG_TECH_ORE_VEIN_ORES VALUES ('gtov~ore.mix.runite', '{RuniteBlock}', 'Runite', 'Mars', 'PRIMARY')");

        // A vein in both worlds: the Mars-stone block must seed at Mars's era, not at the vein's cheapest world.
        db.Execute("INSERT INTO GREG_TECH_ORE_VEIN VALUES ('gtov~ore.mix.dual', 5, 1, 'Dualium', 60, 10, 24, 'ore.mix.dual', 40)");
        db.Execute("INSERT INTO GREG_TECH_ORE_VEIN_DIMENSIONS VALUES ('gtov~ore.mix.dual', 'Ow', 60, 10, 1.0)");
        db.Execute("INSERT INTO GREG_TECH_ORE_VEIN_DIMENSIONS VALUES ('gtov~ore.mix.dual', 'Ma', 60, 10, 1.0)");
        db.Execute($"INSERT INTO GREG_TECH_ORE_VEIN_ORES VALUES ('gtov~ore.mix.dual', '{DualOreOw}', 'Dualium', 'Stone', 'PRIMARY')");
        db.Execute($"INSERT INTO GREG_TECH_ORE_VEIN_ORES VALUES ('gtov~ore.mix.dual', '{DualOreMars}', 'Dualium', 'Mars', 'PRIMARY')");

        // A vial-capturable mob's drop and an uncapturable one's: only the first seeds. The
        // pearl feeds a recipe so it reaches the item set the way real mob drops do.
        Item(db, "i~fixture~mob_pearl", "Fixture Mob Pearl", "fixture");
        Item(db, "i~fixture~boss_relic", "Fixture Boss Relic", "fixture");
        Item(db, "i~fixture~pearl_block", "Fixture Pearl Block", "fixture");
        Group(db, "g_mob_pearl", ("i~fixture~mob_pearl", 1));
        Recipe(db, "r_pearl_grind", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_mob_pearl", 0)], outputs: [("i~fixture~pearl_block", 1, 1.0)], voltage: 4, duration: 100);
        db.Execute("INSERT INTO MOB_INFO VALUES ('mi~1', 1, 0, 0, 1, 'mob~1')");
        db.Execute("INSERT INTO MOB_INFO_DROPS VALUES ('mi~1', 'i~fixture~mob_pearl', 1, 0, 0.5, 1, 'NORMAL')");
        db.Execute("INSERT INTO MOB_INFO VALUES ('mi~2', 0, 0, 0, 0, 'mob~2')");
        db.Execute("INSERT INTO MOB_INFO_DROPS VALUES ('mi~2', 'i~fixture~boss_relic', 1, 0, 1.0, 1, 'NORMAL')");
        // The relic smelts into an ingot, so a dated mob shows up as the ingot's tier.
        Item(db, RelicIngot, "Relicium Ingot", "fixture");
        Group(db, "g_boss_relic", ("i~fixture~boss_relic", 1));
        Group(db, "g_relic_ingot", (RelicIngot, 1));
        Oredict(db, "ingotRelicium", "g_relic_ingot");
        Unify(db, "ingotRelicium", RelicIngot);
        Recipe(db, "r_relic_smelt", "t_furnace", inputs: [("g_boss_relic", 0)], outputs: [(RelicIngot, 1, 1.0)]);
    }
}
