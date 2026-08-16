using Dapper;
using Microsoft.Data.Sqlite;

namespace Craftiger.Builder.UnitTests;

/// <summary>Hand-written mini NESQL dump exercising every builder rule once.</summary>
public static class FixtureDump
{
    public const string GtBronze = "i~gregtech~gt.metaitem.01~11300";
    public const string Ic2Bronze = "i~IC2~itemIngot~2";
    public const string BronzeBlock = "i~gregtech~gt.blockmetal~2";
    public const string BronzeDust = "i~gregtech~gt.metaitem.01~2300";
    public const string AluIngot = "i~gregtech~gt.metaitem.01~11019";
    public const string AluDust = "i~gregtech~gt.metaitem.01~2019";
    public const string AluBlock = "i~gregtech~gt.blockmetal~0";
    public const string AluRod = "i~gregtech~gt.metaitem.02~23019";
    public const string Saw = "i~gregtech~gt.metatool.01~10";
    public const string Mold = "i~gregtech~gt.metaitem.01~32306";
    public const string Plank = "i~minecraft~planks~0";
    public const string Log = "i~minecraft~log~0";
    public const string IronIngot = "i~minecraft~iron_ingot~0";
    public const string CastIron = "i~gregtech~gt.metaitem.01~11304";
    public const string AluOre = "i~gregtech~gt.blockores~19";
    public const string NaqOre = "i~gregtech~gt.blockores~324";
    public const string NaqOreMars = "i~gregtech~gt.blockores3~324";
    public const string NaqDust = "i~gregtech~gt.metaitem.01~2324";
    public const string NaqIngot = "i~gregtech~gt.metaitem.01~11324";
    public const string FreezerItem = "i~gregtech~gt.blockmachines~1002";
    public const string ColdIngot = "i~gregtech~gt.metaitem.01~11999";
    public const string CopperOre = "i~gregtech~gt.blockores~35";
    public const string CopperDust = "i~gregtech~gt.metaitem.01~2035";
    public const string CopperIngot = "i~gregtech~gt.metaitem.01~11035";
    public const string AnnealedIngot = "i~gregtech~gt.metaitem.01~11345";
    public const string AnnealedDust = "i~gregtech~gt.metaitem.01~2345";
    public const string SpaceMiner = "i~gregtech~gt.blockmachines~2001";
    public const string KobOre = "i~miscutils~oreKoboldite~0";
    public const string KobDust = "i~miscutils~dustKoboldite~0";
    public const string KobIngot = "i~miscutils~ingotKoboldite~0";
    public const string RuniteBlock = "i~miscutils~oreRunite~0";
    public const string RawRunite = "i~miscutils~oreRawRunite~0";
    public const string RuniteDust = "i~miscutils~dustRunite~0";
    public const string RuniteIngot = "i~miscutils~ingotRunite~0";
    public const string ComOre = "i~miscutils~oreComancheite~0";
    public const string ComDust = "i~miscutils~dustComancheite~0";
    public const string ComIngot = "i~miscutils~ingotComancheite~0";
    public const string ObsidianBlock = "i~minecraft~obsidian~0";
    public const string Dryer = "i~gregtech~gt.blockmachines~2002";
    public const string EbfController = "i~gregtech~gt.blockmachines~1003";
    public const string MaceratorLv = "i~gregtech~gt.blockmachines~106";
    public const string MixerLv = "i~gregtech~gt.blockmachines~110";
    public const string MixerStack = "i~gregtech~gt.blockmachines~798";
    public const string DearStack = "i~gregtech~gt.blockmachines~799";
    public const string MixIngot = "i~gregtech~gt.metaitem.01~11040";
    public const string DearIngot = "i~gregtech~gt.metaitem.01~11041";
    public const string BerryIngot = "i~gregtech~gt.metaitem.01~11044";
    public const string ChoiceBrick = "i~miscutils~choiceBrick~0";
    public const string WirelessIngot = "i~gregtech~gt.metaitem.01~11666";
    public const string DualOreOw = "i~gregtech~gt.blockores~555";
    public const string DualOreMars = "i~gregtech~gt.blockores3~555";
    public const string DualDust = "i~gregtech~gt.metaitem.01~2555";
    public const string DualIngot = "i~gregtech~gt.metaitem.01~11555";
    public const string InertDust = "i~miscutils~dustInertium~0";
    public const string InertSmall = "i~miscutils~dustSmallInertium~0";
    public const string VoidShard = "i~miscutils~voidShard~0";
    public const string BerrySeed = "i~cropsnh~genericSeed~0";
    public const string Berry = "i~cropsnh~berry~0";
    public const string WeedSeed = "i~cropsnh~genericSeed~1";
    public const string Weed = "i~cropsnh~weed~0";
    public const string Oil = "f~oil";
    public const string OilIngot = "i~gregtech~gt.metaitem.01~11043";
    public const string Rig = "i~gregtech~gt.blockmachines~1004";
    public const string EndStone = "i~minecraft~end_stone~0";
    public const string EndIngot = "i~gregtech~gt.metaitem.01~11042";
    public const string GemOre = "i~gregtech~gt.blockores~500";
    public const string Gem = "i~gregtech~gt.metaitem.01~8500";
    public const string GemDust = "i~gregtech~gt.metaitem.01~2500";
    public const string ClayBlock = "i~minecraft~clay~0";
    public const string ClayBall = "i~minecraft~clay_ball~0";
    public const string DryIngot = "i~gregtech~gt.metaitem.01~11777";
    public const string ByDust = "i~gregtech~gt.metaitem.01~2778";
    public const string ByIngot = "i~gregtech~gt.metaitem.01~11778";
    public const string NugIngot = "i~gregtech~gt.metaitem.01~11045";
    public const string NugNugget = "i~gregtech~gt.metaitem.01~9045";
    public const string NugImpure = "i~gregtech~gt.metaitem.01~2945";
    public const string LostIngot = "i~gregtech~gt.metaitem.01~11046";
    public const string PhantomOre = "i~gregtech~gt.blockores~501";
    public const string PhantomIngot = "i~gregtech~gt.metaitem.01~11047";
    public const string Oxygen = "f~oxygen";
    public const string Water = "f~water";
    public const string Lava = "f~lava";
    public const string Hydrogen = "f~hydrogen";
    public const string WaterCell = "i~gregtech~gt.metaitem.01~30001";
    public const string EmptyCell = "i~gregtech~gt.metaitem.01~32000";

    public static string Create(string directory)
    {
        var path = Path.Combine(directory, "dump.sqlite");
        using var db = new SqliteConnection($"Data Source={path}");
        db.Open();

        db.Execute("""
            CREATE TABLE ITEM(ID TEXT, IMAGE_FILE_PATH TEXT, INTERNAL_NAME TEXT, ITEM_DAMAGE INTEGER,
                ITEM_ID INTEGER, LOCALIZED_NAME TEXT, MAX_DAMAGE INTEGER, MAX_STACK_SIZE INTEGER,
                MOD_ID TEXT, NBT TEXT, UNLOCALIZED_NAME TEXT);
            CREATE TABLE FLUID(ID TEXT, DENSITY INTEGER, FLUID_ID INTEGER, GASEOUS INTEGER,
                IMAGE_FILE_PATH TEXT, INTERNAL_NAME TEXT, LOCALIZED_NAME TEXT, LUMINOSITY INTEGER,
                MOD_ID TEXT, NBT TEXT, TEMPERATURE INTEGER, UNLOCALIZED_NAME TEXT, VISCOSITY INTEGER);
            CREATE TABLE RECIPE(ID TEXT, RECIPE_TYPE_ID TEXT);
            CREATE TABLE RECIPE_TYPE(ID TEXT, CATEGORY TEXT, TYPE TEXT);
            CREATE TABLE RECIPE_TYPE_ITEM(RECIPE_TYPE_ID TEXT, ICON_ID TEXT);
            CREATE TABLE GREG_TECH_RECIPE(ID TEXT, AMPERAGE INTEGER, DURATION INTEGER, VOLTAGE INTEGER, VOLTAGE_TIER TEXT, RECIPE_CATEGORY TEXT, REQUIRES_CLEANROOM INTEGER, RECIPE_ID TEXT);
            CREATE TABLE GREG_TECH_RECIPE_METADATA(GREG_TECH_RECIPE_ID TEXT, METADATA_KEY TEXT, METADATA_VALUE INTEGER);
            CREATE TABLE ITEM_GROUP_ITEM_STACKS(ITEM_GROUP_ID TEXT, ITEM_STACKS_ITEM_ID TEXT, ITEM_STACKS_STACK_SIZE INTEGER);
            CREATE TABLE ORE_DICTIONARY(ID TEXT, NAME TEXT, ITEM_GROUP_ID TEXT);
            CREATE TABLE RECIPE_ITEM_GROUP(RECIPE_ID TEXT, ITEM_INPUTS_ID TEXT, ITEM_INPUTS_KEY INTEGER);
            CREATE TABLE RECIPE_ITEM_OUTPUTS(RECIPE_ID TEXT, ITEM_OUTPUTS_VALUE_ITEM_ID TEXT,
                ITEM_OUTPUTS_VALUE_PROBABILITY REAL, ITEM_OUTPUTS_VALUE_STACK_SIZE INTEGER, ITEM_OUTPUTS_KEY INTEGER);
            CREATE TABLE RECIPE_FLUID_GROUP(RECIPE_ID TEXT, FLUID_INPUTS_ID TEXT, FLUID_INPUTS_KEY INTEGER);
            CREATE TABLE FLUID_GROUP_FLUID_STACKS(FLUID_GROUP_ID TEXT, FLUID_STACKS_AMOUNT INTEGER, FLUID_STACKS_FLUID_ID TEXT);
            CREATE TABLE RECIPE_FLUID_OUTPUTS(RECIPE_ID TEXT, FLUID_OUTPUTS_VALUE_AMOUNT INTEGER,
                FLUID_OUTPUTS_VALUE_FLUID_ID TEXT, FLUID_OUTPUTS_VALUE_PROBABILITY REAL, FLUID_OUTPUTS_KEY INTEGER);
            CREATE TABLE FLUID_CONTAINER(ID TEXT, FLUID_STACK_AMOUNT INTEGER, CONTAINER_ID TEXT,
                EMPTY_CONTAINER_ID TEXT, FLUID_STACK_FLUID_ID TEXT);
            CREATE TABLE GREG_TECH_DIMENSION(ID TEXT, ABBREVIATION TEXT, FULL_NAME TEXT, INTERNAL_NAME TEXT, ROCKET_TIER INTEGER);
            CREATE TABLE GREG_TECH_DIMENSION_STONE_TYPES(GREG_TECH_DIMENSION_ID TEXT, STONE_TYPES TEXT);
            CREATE TABLE GREG_TECH_ORE_VEIN(ID TEXT, DENSITY INTEGER, ENABLED_BY_DEFAULT INTEGER, LOCALIZED_NAME TEXT,
                MAXY INTEGER, MINY INTEGER, SIZE INTEGER, VEIN_NAME TEXT, WEIGHT INTEGER);
            CREATE TABLE GREG_TECH_ORE_VEIN_DIMENSIONS(GREG_TECH_ORE_VEIN_ID TEXT,
                DIMENSIONS_DIMENSION_ABBREVIATION TEXT, DIMENSIONS_MAXY INTEGER, DIMENSIONS_MINY INTEGER, DIMENSIONS_PROBABILITY REAL);
            CREATE TABLE GREG_TECH_ORE_VEIN_ORES(GREG_TECH_ORE_VEIN_ID TEXT, ORES_ITEM_ID TEXT,
                ORES_MATERIAL_NAME TEXT, ORES_STONE_TYPE TEXT, ORES_VEIN_LAYER TEXT);
            CREATE TABLE GREG_TECH_SMALL_ORE(ID TEXT, AMOUNT_PER_CHUNK INTEGER, ENABLED_BY_DEFAULT INTEGER,
                MATERIAL_NAME TEXT, MAXY INTEGER, MINY INTEGER, SMALL_ORE_NAME TEXT);
            CREATE TABLE GREG_TECH_SMALL_ORE_DIMENSIONS(GREG_TECH_SMALL_ORE_ID TEXT,
                DIMENSIONS_DIMENSION_ABBREVIATION TEXT, DIMENSIONS_PROBABILITY REAL);
            CREATE TABLE GREG_TECH_SMALL_ORE_BLOCKS(GREG_TECH_SMALL_ORE_ID TEXT, BLOCKS_ITEM_ID TEXT, BLOCKS_STONE_TYPE TEXT);
            CREATE TABLE GREG_TECH_SMALL_ORE_DROPS(GREG_TECH_SMALL_ORE_ID TEXT, DROPS_ITEM_ID TEXT);
            CREATE TABLE ITEM_TOOLTIP(ITEM_ID TEXT, TOOLTIP TEXT, TOOLTIP_ORDER INTEGER);
            CREATE TABLE GREG_TECH_RECIPE_MAP(ID TEXT, AMPERAGE INTEGER, HAS_MULTI_BLOCK INTEGER,
                HAS_SINGLE_BLOCK INTEGER, LOCALIZED_NAME TEXT, UNLOCALIZED_NAME TEXT);
            CREATE TABLE GREG_TECH_RECIPE_MAP_MACHINES(GREG_TECH_RECIPE_MAP_ID TEXT, MACHINES_ITEM_ID TEXT,
                MACHINES_MULTIBLOCK INTEGER, MACHINES_TIER INTEGER);
            CREATE TABLE BLOCK_DROP(ID TEXT, BLOCK_META INTEGER, BLOCK_NAME TEXT, QUANTITY INTEGER,
                BLOCK_ITEM_ID TEXT, DROP_ID TEXT);
            CREATE TABLE CROPS_NH_CROP(ID TEXT, CROP_ID TEXT, DROP_CHANCE REAL, GROWTH_DURATION INTEGER,
                HIDDEN INTEGER, MACHINE_BREEDING_RECIPE_TIER INTEGER, MAX_LIGHT_LEVEL INTEGER,
                MIN_LIGHT_LEVEL INTEGER, MIN_SEED_BED_TIER INTEGER, NAME TEXT, SOIL_LIST_ID TEXT,
                TIER INTEGER, SEED_ID TEXT);
            CREATE TABLE CROPS_NH_CROP_DROPS(CROPS_NH_CROP_ID TEXT, DROPS_ITEM_ID TEXT, DROPS_WEIGHT INTEGER);
            CREATE TABLE CROPS_NH_CROP_UNDER_BLOCKS(CROPS_NH_CROP_ID TEXT, UNDER_BLOCKS_ITEM_ID TEXT);
            CREATE TABLE CROPS_NH_CROP_ALTERNATE_SEEDS(CROPS_NH_CROP_ID TEXT, ALTERNATE_SEEDS_ITEM_ID TEXT);
            CREATE TABLE GREG_TECH_UNDERGROUND_FLUID(ID TEXT, FLUID_NAME TEXT, FLUID_ID TEXT);
            CREATE TABLE GREG_TECH_UNDERGROUND_FLUID_DIMENSIONS(GREG_TECH_UNDERGROUND_FLUID_ID TEXT,
                DIMENSIONS_DIMENSION_ABBREVIATION TEXT, DIMENSIONS_MAX_AMOUNT INTEGER,
                DIMENSIONS_MIN_AMOUNT INTEGER, DIMENSIONS_PROBABILITY REAL);
            CREATE TABLE METADATA(ID INTEGER, CREATION_TIME_MILLIS INTEGER, VERSION TEXT);
            """);

        Item(db, GtBronze, "Bronze Ingot", "gregtech");
        Item(db, Ic2Bronze, "Bronze Ingot", "IC2");
        Item(db, BronzeBlock, "Block of Bronze", "gregtech");
        Item(db, BronzeDust, "Bronze Dust", "gregtech");
        Item(db, AluIngot, "Aluminium Ingot", "gregtech");
        Item(db, AluDust, "Aluminium Dust", "gregtech");
        Item(db, AluBlock, "Block of Aluminium", "gregtech");
        Item(db, AluRod, "Aluminium Rod", "gregtech");
        Item(db, Saw, "Saw", "gregtech");
        Item(db, Mold, "Extruder Shape (Rod)", "gregtech");
        Item(db, Plank, "Oak Wood Planks", "minecraft");
        Item(db, Log, "Oak Wood", "minecraft");
        Fluid(db, Water, "water", "Water");
        Fluid(db, Lava, "lava", "Lava");
        Fluid(db, Hydrogen, "hydrogen", "Hydrogen");
        Item(db, WaterCell, "Water Cell", "gregtech");
        Item(db, EmptyCell, "Empty Cell", "gregtech");
        Item(db, IronIngot, "Iron Ingot", "minecraft");
        Item(db, CastIron, "Cast Iron Ingot", "gregtech");
        Item(db, AluOre, "Aluminium Ore", "gregtech");
        Item(db, NaqOre, "Naquadah Ore", "gregtech");
        Item(db, NaqOreMars, "Naquadah Ore", "gregtech");
        Item(db, NaqDust, "Naquadah Dust", "gregtech");
        Item(db, NaqIngot, "Naquadah Ingot", "gregtech");
        Item(db, FreezerItem, "Vacuum Freezer", "gregtech");
        Item(db, ColdIngot, "Cold Ingot", "gregtech");
        Item(db, CopperOre, "Copper Ore", "gregtech");
        Item(db, CopperDust, "Copper Dust", "gregtech");
        Item(db, CopperIngot, "Copper Ingot", "gregtech");
        Item(db, AnnealedIngot, "Annealed Copper Ingot", "gregtech");
        Item(db, AnnealedDust, "Annealed Copper Dust", "gregtech");
        Fluid(db, Oxygen, "oxygen", "Oxygen");
        Item(db, SpaceMiner, "Space Mining Module", "gregtech");
        Item(db, ChoiceBrick, "Choice Brick", "miscutils");
        Item(db, WirelessIngot, "Wirelessium Ingot", "gregtech");
        Item(db, DualOreOw, "Dualium Ore", "gregtech");
        Item(db, DualOreMars, "Dualium Ore", "gregtech");
        Item(db, DualDust, "Dualium Dust", "gregtech");
        Item(db, DualIngot, "Dualium Ingot", "gregtech");
        Item(db, InertDust, "Inertium Dust", "miscutils");
        Item(db, InertSmall, "Small Pile of Inertium Dust", "miscutils");
        Item(db, VoidShard, "Void Shard", "miscutils");
        Item(db, KobOre, "Koboldite Ore", "miscutils");
        Item(db, KobDust, "Koboldite Dust", "miscutils");
        Item(db, KobIngot, "Koboldite Ingot", "miscutils");
        Item(db, RuniteBlock, "Runite Ore", "miscutils");
        Item(db, RawRunite, "Raw Runite Ore", "miscutils");
        Item(db, RuniteDust, "Runite Dust", "miscutils");
        Item(db, RuniteIngot, "Runite Ingot", "miscutils");
        Item(db, ComOre, "Comancheite Ore", "miscutils");
        Item(db, ComDust, "Comancheite Dust", "miscutils");
        Item(db, ComIngot, "Comancheite Ingot", "miscutils");
        Item(db, ObsidianBlock, "Obsidian", "minecraft");
        Item(db, Dryer, "Basic Dryer", "gregtech");
        Item(db, EbfController, "Electric Blast Furnace", "gregtech");
        Item(db, MaceratorLv, "Macerator", "gregtech");
        Item(db, MixerLv, "Mixer", "gregtech");
        Item(db, MixerStack, "Mixer Array", "gregtech");
        Item(db, DearStack, "Dear Mixer Array", "gregtech");
        Item(db, MixIngot, "Mixium Ingot", "gregtech");
        Item(db, DearIngot, "Dearium Ingot", "gregtech");
        Item(db, BerryIngot, "Berrium Ingot", "gregtech");
        Item(db, BerrySeed, "Naquadah Oreberry Seeds", "cropsnh");
        Item(db, Berry, "Naquadah Oreberry", "cropsnh");
        Item(db, WeedSeed, "Weed Seeds", "cropsnh");
        Item(db, Weed, "Weed", "cropsnh");
        Fluid(db, Oil, "oil", "Oil");
        Item(db, OilIngot, "Oilium Ingot", "gregtech");
        Item(db, Rig, "Fluid Drilling Rig", "gregtech");
        Item(db, EndStone, "End Stone", "minecraft");
        Item(db, EndIngot, "Endium Ingot", "gregtech");
        Item(db, GemOre, "Gemium Ore", "gregtech");
        Item(db, Gem, "Gemium", "gregtech");
        Item(db, GemDust, "Gemium Dust", "gregtech");
        Item(db, ClayBlock, "Clay", "minecraft");
        Item(db, ClayBall, "Clay Ball", "minecraft");
        Item(db, PhantomOre, "Phantomium Ore", "gregtech");
        Item(db, PhantomIngot, "Phantomium Ingot", "gregtech");
        Item(db, NugIngot, "Nugium Ingot", "gregtech");
        Item(db, NugNugget, "Nugium Nugget", "gregtech");
        Item(db, NugImpure, "Impure Pile of Nugium Dust", "gregtech");
        Item(db, LostIngot, "Lostium Ingot", "gregtech");
        Item(db, DryIngot, "Dryium Ingot", "gregtech");
        Item(db, ByDust, "Byprodium Dust", "gregtech");
        Item(db, ByIngot, "Byprodium Ingot", "gregtech");
        db.Execute($"INSERT INTO ITEM_TOOLTIP VALUES ('{Dryer}', 'Voltage IN: §e128§7 (§eMV§7)', 2)");
        db.Execute($"INSERT INTO FLUID_CONTAINER VALUES ('fc_water', 1000, '{WaterCell}', '{EmptyCell}', '{Water}')");

        Group(db, "g_bronze_ingot", (GtBronze, 1), (Ic2Bronze, 1));
        Group(db, "g_bronze_ingot9", (GtBronze, 9), (Ic2Bronze, 9));
        Group(db, "g_bronze_block", (BronzeBlock, 1));
        Group(db, "g_bronze_dust", (BronzeDust, 1));
        Group(db, "g_alu_ingot", (AluIngot, 1));
        Group(db, "g_alu_ingot9", (AluIngot, 9));
        Group(db, "g_alu_block", (AluBlock, 1));
        Group(db, "g_alu_dust", (AluDust, 1));
        Group(db, "g_saw", (Saw, 1));
        Group(db, "g_mold", (Mold, 0));
        Group(db, "g_log", (Log, 1));
        Group(db, "g_mix_ingot", (MixIngot, 1));
        Group(db, "g_dear_ingot", (DearIngot, 1));
        Oredict(db, "ingotMixium", "g_mix_ingot");
        Oredict(db, "ingotDearium", "g_dear_ingot");
        db.Execute($"INSERT INTO FLUID_GROUP_FLUID_STACKS VALUES ('g_oil', 1000, '{Oil}')");
        Group(db, "g_oil_ingot", (OilIngot, 1));
        Group(db, "g_berry", (Berry, 1));
        Group(db, "g_berry_ingot", (BerryIngot, 1));
        Oredict(db, "ingotBerrium", "g_berry_ingot");
        Oredict(db, "ingotOilium", "g_oil_ingot");
        Group(db, "g_phantom_ore", (PhantomOre, 1));
        Group(db, "g_phantom_ingot", (PhantomIngot, 1));
        Oredict(db, "oreStonePhantomium", "g_phantom_ore");
        Oredict(db, "ingotPhantomium", "g_phantom_ingot");
        Group(db, "g_alu_rod", (AluRod, 1));
        Group(db, "g_nug_ingot", (NugIngot, 1));
        Group(db, "g_nug_nugget", (NugNugget, 9));
        Group(db, "g_nug_impure", (NugImpure, 1));
        Group(db, "g_lost_ingot", (LostIngot, 1));
        Oredict(db, "ingotNugium", "g_nug_ingot");
        Oredict(db, "nuggetNugium", "g_nug_nugget");
        Oredict(db, "dustImpureNugium", "g_nug_impure");
        Oredict(db, "ingotLostium", "g_lost_ingot");
        Group(db, "g_endstone", (EndStone, 1));
        Group(db, "g_end_ingot", (EndIngot, 1));
        Oredict(db, "endstone", "g_endstone");
        Oredict(db, "ingotEndium", "g_end_ingot");
        Group(db, "g_gem_ore", (GemOre, 1));
        Group(db, "g_gem", (Gem, 1));
        Group(db, "g_gem_dust", (GemDust, 1));
        Oredict(db, "oreGemium", "g_gem_ore");
        Oredict(db, "gemGemium", "g_gem");
        Oredict(db, "dustGemium", "g_gem_dust");
        Group(db, "g_clay_ball", (ClayBall, 1));
        db.Execute($"INSERT INTO FLUID_GROUP_FLUID_STACKS VALUES ('g_water', 1000, '{Water}')");

        Oredict(db, "ingotBronze", "g_bronze_ingot");
        Oredict(db, "dustBronze", "g_bronze_dust");
        Oredict(db, "blockBronze", "g_bronze_block");
        Oredict(db, "ingotAluminium", "g_alu_ingot");
        Oredict(db, "dustAluminium", "g_alu_dust");
        Oredict(db, "blockAluminium", "g_alu_block");
        Oredict(db, "stickAluminium", "g_alu_rod_oredict_unused");
        Oredict(db, "logWood", "g_log");
        Oredict(db, "plankWood", "g_plank_oredict_unused");
        Group(db, "g_iron", (IronIngot, 1));
        Group(db, "g_cast_iron", (CastIron, 1));
        Group(db, "g_any_iron", (IronIngot, 1), (CastIron, 1));
        Oredict(db, "ingotIron", "g_iron");
        Oredict(db, "ingotCastIron", "g_cast_iron");
        Oredict(db, "ingotAnyIron", "g_any_iron");
        Group(db, "g_alu_ore", (AluOre, 1));
        Oredict(db, "oreAluminium", "g_alu_ore");
        Group(db, "g_naq_ore", (NaqOre, 1));
        Group(db, "g_naq_dust", (NaqDust, 1));
        Oredict(db, "oreNaquadah", "g_naq_ore");
        Oredict(db, "dustNaquadah", "g_naq_dust");
        Group(db, "g_naq_ingot", (NaqIngot, 1));
        Oredict(db, "ingotNaquadah", "g_naq_ingot");
        Group(db, "g_cold_ingot", (ColdIngot, 1));
        Oredict(db, "ingotCold", "g_cold_ingot");
        Group(db, "g_copper_ore", (CopperOre, 1));
        Group(db, "g_copper_dust", (CopperDust, 1));
        Group(db, "g_copper_ingot", (CopperIngot, 1));
        Group(db, "g_annealed_ingot", (AnnealedIngot, 1));
        Group(db, "g_annealed_dust", (AnnealedDust, 1));
        db.Execute($"INSERT INTO FLUID_GROUP_FLUID_STACKS VALUES ('g_oxygen', 63, '{Oxygen}')");
        Group(db, "g_wireless_ingot", (WirelessIngot, 1));
        Oredict(db, "ingotWirelessium", "g_wireless_ingot");
        Group(db, "g_dual_ore_ma", (DualOreMars, 1));
        Group(db, "g_dual_dust", (DualDust, 1));
        Group(db, "g_dual_ingot", (DualIngot, 1));
        Oredict(db, "dustDualium", "g_dual_dust");
        Oredict(db, "ingotDualium", "g_dual_ingot");
        Group(db, "g_inert_dust", (InertDust, 1));
        Group(db, "g_inert_small", (InertSmall, 1));
        Group(db, "g_inert_small4", (InertSmall, 4));
        Group(db, "g_void", (VoidShard, 1));
        Oredict(db, "dustInertium", "g_inert_dust");
        Oredict(db, "dustSmallInertium", "g_inert_small");
        Group(db, "g_kob_ore", (KobOre, 1));
        Group(db, "g_kob_dust", (KobDust, 1));
        Group(db, "g_kob_ingot", (KobIngot, 1));
        Oredict(db, "oreKoboldite", "g_kob_ore");
        Oredict(db, "dustKoboldite", "g_kob_dust");
        Oredict(db, "ingotKoboldite", "g_kob_ingot");
        Group(db, "g_raw_runite", (RawRunite, 1));
        Group(db, "g_runite_dust", (RuniteDust, 1));
        Group(db, "g_runite_ingot", (RuniteIngot, 1));
        Oredict(db, "rawOreRunite", "g_raw_runite");
        Oredict(db, "dustRunite", "g_runite_dust");
        Oredict(db, "ingotRunite", "g_runite_ingot");
        Group(db, "g_com_ore", (ComOre, 1));
        Group(db, "g_com_dust", (ComDust, 1));
        Group(db, "g_com_ingot", (ComIngot, 1));
        Oredict(db, "oreComancheite", "g_com_ore");
        Oredict(db, "dustComancheite", "g_com_dust");
        Oredict(db, "ingotComancheite", "g_com_ingot");
        Group(db, "g_dry_ingot", (DryIngot, 1));
        Oredict(db, "ingotDryium", "g_dry_ingot");
        // The block* name wins the primary pick and must not hide the minable name.
        Group(db, "g_obsidian", (ObsidianBlock, 1));
        Oredict(db, "blockObsidian", "g_obsidian");
        Oredict(db, "obsidian", "g_obsidian");
        Group(db, "g_by_dust", (ByDust, 1));
        Group(db, "g_by_ingot", (ByIngot, 1));
        Oredict(db, "dustByprodium", "g_by_dust");
        Oredict(db, "ingotByprodium", "g_by_ingot");
        Oredict(db, "oreCopper", "g_copper_ore");
        Oredict(db, "dustCopper", "g_copper_dust");
        Oredict(db, "ingotCopper", "g_copper_ingot");
        Oredict(db, "ingotAnnealedCopper", "g_annealed_ingot");
        Oredict(db, "dustAnnealedCopper", "g_annealed_dust");

        RecipeType(db, "t_shaped", "minecraft", "Crafting (Shaped)");
        RecipeType(db, "t_furnace", "minecraft", "Furnace");
        RecipeType(db, "rt~gregtech~gt.recipe.blastfurnace~MV", "gregtech", "Blast Furnace (MV)", handlerIcons: 2);
        RecipeType(db, "rt~gregtech~gt.recipe.extruder~MV", "gregtech", "Extruder (MV)");
        RecipeType(db, "rt~gregtech~gt.recipe.macerator~ULV", "gregtech", "Macerator (ULV)");
        RecipeType(db, "rt~gregtech~gt.recipe.fluidsolidifier~MV", "gregtech", "Fluid Solidifier (MV)");
        RecipeType(db, "rt~gregtech~gt.recipe.largeboilerfakefuels~ULV", "gregtech", "Large Boiler Fuels (ULV)");
        RecipeType(db, "rt~gregtech~gt.recipe.electrolyzer~MV", "gregtech", "Electrolyzer (MV)");
        RecipeType(db, "rt~gregtech~gt.recipe.arcfurnace~LV", "gregtech", "Arc Furnace (LV)");
        RecipeType(db, "rt~gregtech~gt.recipe.fluidextractor~LV", "gregtech", "Fluid Extractor (LV)");
        RecipeType(db, "rt~gregtech~gt.recipe.alloysmelter~ULV", "gregtech", "Alloy Smelter (ULV)");
        RecipeType(db, "rt~gregtech~gt.recipe.vacuumfreezer~MV", "gregtech", "Vacuum Freezer (MV)", handlerIcons: 0, handlerItem: FreezerItem);
        RecipeType(db, "rt~gregtech~gt.recipe.spacemining~HV", "gregtech", "Space Mining (HV)", handlerIcons: 0, handlerItem: SpaceMiner);
        RecipeType(db, "rt~gregtech~gt.recipe.dryer~LV", "gregtech", "Dryer (LV)", handlerIcons: 0, handlerItem: Dryer);
        RecipeType(db, "rt~gregtech~gt.recipe.mixer~HV", "gregtech", "Mixer (HV)", handlerIcons: 0);
        RecipeType(db, "rt~gregtech~gt.recipe.dearmixer~HV", "gregtech", "Dear Mixer (HV)", handlerIcons: 0);
        RecipeType(db, "rt~gregtech~gt.recipe.packager~ULV", "gregtech", "Packager (ULV)", handlerIcons: 0);
        RecipeType(db, "rt~gregtech~gt.recipe.mixer~MAX", "gregtech", "Mixer (MAX)", handlerIcons: 0);

        BlockDrop(db, "minecraft:clay", ClayBlock, ClayBall, 4);
        BlockDrop(db, "minecraft:obsidian", ObsidianBlock, ObsidianBlock, 1);

        RecipeMap(db, "gt.recipe.blastfurnace", "Blast Furnace", [(EbfController, true, null)]);
        RecipeMap(db, "gt.recipe.macerator", "Macerator", [(MaceratorLv, false, 1)]);
        RecipeMap(db, "gt.recipe.mixer", "Mixer", [(MixerLv, false, 1), (MixerStack, true, null)]);
        RecipeMap(db, "gt.recipe.dearmixer", "Dear Mixer", [(MixerLv, false, 1), (DearStack, true, null)]);

        // Ingot <-> block cycle, both directions on the crafting table.
        Recipe(db, "r_block", "t_shaped", inputs: [("g_bronze_ingot9", 0)], outputs: [(BronzeBlock, 1, 1.0)]);
        Recipe(db, "r_unblock", "t_shaped", inputs: [("g_bronze_block", 0)], outputs: [(GtBronze, 9, 1.0)]);
        Recipe(db, "r_alu_block", "t_shaped", inputs: [("g_alu_ingot9", 0)], outputs: [(AluBlock, 1, 1.0)]);
        Recipe(db, "r_alu_unblock", "t_shaped", inputs: [("g_alu_block", 0)], outputs: [(AluIngot, 9, 1.0)]);

        // Smelting bronze dust is the tier-0 route; EBF is aluminium's only real route.
        Recipe(db, "r_smelt", "t_furnace", inputs: [("g_bronze_dust", 0)], outputs: [(GtBronze, 1, 1.0)]);
        Recipe(db, "r_ebf", "rt~gregtech~gt.recipe.blastfurnace~MV", inputs: [("g_alu_dust", 0)], outputs: [(AluIngot, 1, 1.0)], voltage: 120, duration: 500, heat: 1700);

        // Chanced byproduct plus a saw catalyst on the grid.
        Recipe(db, "r_macerate", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_bronze_ingot", 0)], outputs: [(BronzeDust, 1, 1.0), (BronzeDust, 1, 0.9)], voltage: 4, duration: 100);
        Recipe(db, "r_planks", "t_shaped", inputs: [("g_log", 0), ("g_saw", 1)], outputs: [(Plank, 4, 1.0)]);

        // Extruder-only rod with a zero-size shape mold; solidifier gives a pinnable alternative.
        Recipe(db, "r_extrude", "rt~gregtech~gt.recipe.extruder~MV", inputs: [("g_alu_ingot", 0), ("g_mold", 1)], outputs: [(AluRod, 2, 1.0)], voltage: 96, duration: 200);
        Recipe(db, "r_solidify", "rt~gregtech~gt.recipe.fluidsolidifier~MV", inputs: [("g_alu_ingot", 0)], outputs: [(AluRod, 1, 1.0)], voltage: 24, duration: 100, fluidInputs: [("g_water", 0)]);

        // Fuel tabs are pseudo-recipes and must be dropped.
        Recipe(db, "r_fuel", "rt~gregtech~gt.recipe.largeboilerfakefuels~ULV", inputs: [("g_bronze_dust", 0)], outputs: [(BronzeDust, 1, 1.0)], voltage: 32, duration: 1);

        // Distinct materials share the wildcard ingotAnyIron group but must stay separate.
        Recipe(db, "r_iron_use", "t_shaped", inputs: [("g_iron", 0)], outputs: [(Plank, 1, 1.0)]);
        Recipe(db, "r_cast_use", "t_shaped", inputs: [("g_cast_iron", 0)], outputs: [(Plank, 1, 1.0)]);
        // A slot that takes either iron must ship both, not whichever sorts first.
        Recipe(db, "r_any_iron_use", "t_shaped", inputs: [("g_any_iron", 0)], outputs: [(Plank, 1, 1.0)]);
        // A tool anywhere in a slot marks the whole slot as tools, third-party ones included.
        Group(db, "g_saw_or_iron", (Saw, 1), (IronIngot, 1));
        Recipe(db, "r_tool_choice", "t_shaped", inputs: [("g_saw_or_iron", 0), ("g_log", 1)],
            outputs: [(Plank, 1, 1.0)]);
        // A concrete input and a choice must not share a slot number.
        Recipe(db, "r_mixed_slots", "t_shaped", inputs: [("g_log", 0), ("g_any_iron", 1)],
            outputs: [(Plank, 1, 1.0)]);

        // Annealed copper: real era is the LV arc route; the dust-smelting loop
        // must inherit it rather than grant era 0. The slot-1 byproduct only
        // exists on HV+ macerators, splitting the recipe into tiered variants.
        Recipe(db, "r_cu_macerate", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_copper_ore", 0)], outputs: [(CopperDust, 2, 1.0)], voltage: 4, duration: 100,
            byproducts: [(ByDust, 1, 0.5, 1)]);
        Recipe(db, "r_by_smelt", "t_furnace", inputs: [("g_by_dust", 0)], outputs: [(ByIngot, 1, 1.0)]);

        // The dryer is buildable at era 0 but runs on MV voltage.
        Recipe(db, "r_dryer_craft", "t_shaped", inputs: [("g_log", 0)], outputs: [(Dryer, 1, 1.0)]);
        // A berry grows only on naquadah ore, so harvesting it waits for Mars.
        Crop(db, "naqBerry", "Naquadah Oreberry", BerrySeed, hidden: false, drops: [Berry], underBlocks: [NaqOreMars]);
        Crop(db, "weed", "Weed", WeedSeed, hidden: true, drops: [Weed], underBlocks: []);
        Recipe(db, "r_berry_seed_craft", "t_shaped", inputs: [("g_copper_ingot", 0)], outputs: [(BerrySeed, 1, 1.0)]);
        Recipe(db, "r_berry_press", "t_furnace", inputs: [("g_berry", 0)], outputs: [(BerryIngot, 1, 1.0)]);

        // Oil lies in the Overworld, but only a drilling rig gets it out.
        db.Execute("INSERT INTO GREG_TECH_UNDERGROUND_FLUID(ID, FLUID_NAME, FLUID_ID) VALUES ('gtuf~oil', 'oil', @id)", new { id = Oil });
        db.Execute("INSERT INTO GREG_TECH_UNDERGROUND_FLUID_DIMENSIONS(GREG_TECH_UNDERGROUND_FLUID_ID, DIMENSIONS_DIMENSION_ABBREVIATION, DIMENSIONS_MAX_AMOUNT, DIMENSIONS_MIN_AMOUNT, DIMENSIONS_PROBABILITY) VALUES ('gtuf~oil', 'Ow', 100, 0, 1.0)");
        Recipe(db, "r_rig_craft", "t_shaped", inputs: [("g_naq_ingot", 0)], outputs: [(Rig, 1, 1.0)]);
        Recipe(db, "r_oil_smelt", "t_furnace", inputs: [], outputs: [(OilIngot, 1, 1.0)], fluidInputs: [("g_oil", 0)]);

        // End Stone is only minable once the End is open, so what it smelts into waits too.
        Recipe(db, "r_end_smelt", "t_furnace", inputs: [("g_endstone", 0)], outputs: [(EndIngot, 1, 1.0)]);

        // A gem has no ingot twin: its era comes from cutting it, and its dust inherits that.
        Recipe(db, "r_gem_cut", "rt~gregtech~gt.recipe.extruder~MV", inputs: [("g_gem_ore", 0)],
            outputs: [(Gem, 1, 1.0)], voltage: 120, duration: 100);
        Recipe(db, "r_gem_grind", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_gem", 0)],
            outputs: [(GemDust, 1, 1.0)], voltage: 4, duration: 100);
        // Nugium covers the derived and intermediate leaf rules: a nugget priced off its
        // ingot, and an ore-washing pile that has to price from its own recipe.
        Recipe(db, "r_nug_smelt", "t_furnace", inputs: [("g_copper_ingot", 0)], outputs: [(NugIngot, 1, 1.0)]);
        Recipe(db, "r_nug_split", "t_shaped", inputs: [("g_nug_ingot", 0)], outputs: [(NugNugget, 9, 1.0)]);
        Recipe(db, "r_nug_wash", "t_furnace", inputs: [("g_nug_impure", 0)], outputs: [(NugIngot, 1, 1.0)]);
        Recipe(db, "r_nug_grind", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_nug_ingot", 0)],
            outputs: [(NugImpure, 1, 1.0)], voltage: 4, duration: 100);
        // Lostium is only ever consumed, so the era solve never reaches it.
        Recipe(db, "r_lost_use", "t_furnace", inputs: [("g_lost_ingot", 0)], outputs: [(IronIngot, 1, 1.0)]);
        // Clay balls are also farmed, but breaking the block already prices them.
        Crop(db, "clayCrop", "Clay Crop", BerrySeed, hidden: false, drops: [ClayBall], underBlocks: []);
        // Melting a manufactured item down gives back more than crafting it cost. The rod
        // carries no material-shape oredict, so it stands in for a door or a piston.
        Recipe(db, "r_recycle", "rt~gregtech~gt.recipe.arcfurnace~LV", inputs: [("g_alu_rod", 0)],
            outputs: [(AluIngot, 6, 1.0)], voltage: 30, duration: 100, category: "arcFurnaceRecycling");
        // Melting one shape of a material into another gives back exactly what went in, and is
        // often the only route to the molten form, so it survives the same category.
        Recipe(db, "r_melt", "rt~gregtech~gt.recipe.fluidextractor~LV", inputs: [("g_alu_dust", 0)],
            outputs: [(AluRod, 1, 1.0)], voltage: 30, duration: 100,
            category: "fluidExtractorRecycling");
        Recipe(db, "r_brick", "t_furnace", inputs: [("g_clay_ball", 0)], outputs: [(IronIngot, 1, 1.0)]);
        Recipe(db, "r_ebf_craft", "t_shaped", inputs: [("g_copper_ingot", 0)], outputs: [(EbfController, 1, 1.0)]);
        Recipe(db, "r_macerator_craft", "t_shaped", inputs: [("g_copper_ingot", 0)], outputs: [(MaceratorLv, 1, 1.0)]);
        Recipe(db, "r_mixer_craft", "t_shaped", inputs: [("g_copper_ingot", 0)], outputs: [(MixerLv, 1, 1.0)]);

        // A mixed map takes the cheaper of its single blocks and its multiblock's allowance.
        Recipe(db, "r_mixer_stack_craft", "t_shaped", inputs: [("g_copper_ingot", 0)], outputs: [(MixerStack, 1, 1.0)]);
        Recipe(db, "r_dear_stack_craft", "t_shaped", inputs: [("g_naq_ingot", 0)], outputs: [(DearStack, 1, 1.0)]);
        Recipe(db, "r_mix", "rt~gregtech~gt.recipe.mixer~HV", inputs: [("g_copper_ingot", 0)],
            outputs: [(MixIngot, 1, 1.0)], voltage: 480, duration: 100);
        Recipe(db, "r_dear", "rt~gregtech~gt.recipe.dearmixer~HV", inputs: [("g_copper_ingot", 0)],
            outputs: [(DearIngot, 1, 1.0)], voltage: 480, duration: 100);
        Recipe(db, "r_dry", "rt~gregtech~gt.recipe.dryer~LV", inputs: [("g_copper_dust", 0)], outputs: [(DryIngot, 1, 1.0)], voltage: 24, duration: 100);
        Recipe(db, "r_cu_hammer", "t_shaped", inputs: [("g_copper_ore", 0)], outputs: [(CopperDust, 1, 1.0)]);
        Recipe(db, "r_alu_macerate", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_alu_ore", 0)], outputs: [(AluDust, 2, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_naq_macerate", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_naq_ore", 0)], outputs: [(NaqDust, 2, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_naq_smelt", "rt~gregtech~gt.recipe.alloysmelter~ULV", inputs: [("g_naq_dust", 0)], outputs: [(NaqIngot, 1, 1.0)], voltage: 16, duration: 100);

        // The freezer itself is naquadah-era, gating its recipes regardless of voltage.
        Recipe(db, "r_freezer_craft", "t_shaped", inputs: [("g_naq_ingot", 0)], outputs: [(FreezerItem, 1, 1.0)]);
        Recipe(db, "r_freeze", "rt~gregtech~gt.recipe.vacuumfreezer~MV", inputs: [("g_alu_ingot", 0)], outputs: [(ColdIngot, 1, 1.0)], voltage: 30, duration: 100);
        Recipe(db, "r_cu_smelt", "t_furnace", inputs: [("g_copper_dust", 0)], outputs: [(CopperIngot, 1, 1.0)]);
        Recipe(db, "r_oxygen", "rt~gregtech~gt.recipe.electrolyzer~MV", inputs: [], outputs: [], voltage: 30, duration: 100, fluidInputs: [("g_water", 0)]);
        Recipe(db, "r_anneal", "rt~gregtech~gt.recipe.arcfurnace~LV", inputs: [("g_copper_ingot", 0)], outputs: [(AnnealedIngot, 1, 1.0)], voltage: 30, duration: 100, fluidInputs: [("g_oxygen", 0)]);
        Recipe(db, "r_ann_macerate", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_annealed_ingot", 0)], outputs: [(AnnealedDust, 1, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_ann_smelt", "t_furnace", inputs: [("g_annealed_dust", 0)], outputs: [(AnnealedIngot, 1, 1.0)]);
        db.Execute($"INSERT INTO RECIPE_FLUID_OUTPUTS VALUES ('r_oxygen', 500, '{Oxygen}', NULL, 0)");

        // Cell-based recipe: decomposition plus netting must leave only fluids.
        Group(db, "g_water_cell", (WaterCell, 1));
        Recipe(db, "r_electrolyze", "rt~gregtech~gt.recipe.electrolyzer~MV", inputs: [("g_water_cell", 0)], outputs: [(EmptyCell, 1, 1.0)], voltage: 30, duration: 300);
        db.Execute($"INSERT INTO RECIPE_FLUID_OUTPUTS VALUES ('r_electrolyze', 1000, '{Hydrogen}', NULL, 0)");

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

        // Runite's placed block is un-oredicted; its mined rawOre* chunk carries the vein era.
        // GregTech oredicts a stone variant for every material, placed or not; this one is not,
        // so the cheap smelt is a dead end and the MV route decides the era.
        Recipe(db, "r_phantom_smelt", "t_furnace", inputs: [("g_phantom_ore", 0)],
            outputs: [(PhantomIngot, 1, 1.0)]);
        Recipe(db, "r_phantom_alt", "rt~gregtech~gt.recipe.extruder~MV", inputs: [("g_copper_ingot", 0)],
            outputs: [(PhantomIngot, 1, 1.0)], voltage: 96, duration: 200);

        // Ordinary Overworld veins: without one, a material the world never places gets no era.
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

        // Koboldite never world-generates; its era comes from the era-only Space Mining map.
        Recipe(db, "r_miner_craft", "t_shaped", inputs: [("g_naq_ingot", 0)], outputs: [(SpaceMiner, 1, 1.0)]);
        Recipe(db, "r_space", "rt~gregtech~gt.recipe.spacemining~HV", inputs: [("g_naq_dust", 0)], outputs: [(KobOre, 1, 1.0)], voltage: 512, duration: 100);
        Recipe(db, "r_kob_macerate", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_kob_ore", 0)], outputs: [(KobDust, 2, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_kob_smelt", "t_furnace", inputs: [("g_kob_dust", 0)], outputs: [(KobIngot, 1, 1.0)]);
        Recipe(db, "r_runite_macerate", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_raw_runite", 0)], outputs: [(RuniteDust, 2, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_runite_smelt", "t_furnace", inputs: [("g_runite_dust", 0)], outputs: [(RuniteIngot, 1, 1.0)]);
        Recipe(db, "r_com_macerate", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_com_ore", 0)], outputs: [(ComDust, 2, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_com_smelt", "t_furnace", inputs: [("g_com_dust", 0)], outputs: [(ComIngot, 1, 1.0)]);
        Recipe(db, "r_obs_use", "t_shaped", inputs: [("g_obsidian", 0)], outputs: [(Plank, 1, 1.0)]);

        // A fluid slot with alternatives at unequal amounts: 144 mB of water or 1000 mB of
        // oxygen, whichever is cheaper.
        db.Execute($"INSERT INTO FLUID_GROUP_FLUID_STACKS VALUES ('g_either_fluid', 144, '{Water}')");
        db.Execute($"INSERT INTO FLUID_GROUP_FLUID_STACKS VALUES ('g_either_fluid', 1000, '{Oxygen}')");
        Recipe(db, "r_fluid_choice", "rt~gregtech~gt.recipe.mixer~HV", inputs: [("g_copper_dust", 0)],
            outputs: [(ChoiceBrick, 1, 1.0)], voltage: 512, duration: 100, fluidInputs: [("g_either_fluid", 0)]);

        // A wirelessly powered recipe: the sentinel voltage means no hatch requirement, so
        // the era must come from the machine and inputs, not from the MAX label.
        Recipe(db, "r_wireless", "rt~gregtech~gt.recipe.mixer~MAX", inputs: [("g_copper_ingot", 0)],
            outputs: [(WirelessIngot, 1, 1.0)], voltage: 2013265912, duration: 100, label: "MAX");

        // A vein in both worlds, processed only through its Mars-stone block: the block must
        // seed at Mars's era, not at the vein's cheapest world.
        db.Execute("INSERT INTO GREG_TECH_ORE_VEIN VALUES ('gtov~ore.mix.dual', 5, 1, 'Dualium', 60, 10, 24, 'ore.mix.dual', 40)");
        db.Execute("INSERT INTO GREG_TECH_ORE_VEIN_DIMENSIONS VALUES ('gtov~ore.mix.dual', 'Ow', 60, 10, 1.0)");
        db.Execute("INSERT INTO GREG_TECH_ORE_VEIN_DIMENSIONS VALUES ('gtov~ore.mix.dual', 'Ma', 60, 10, 1.0)");
        db.Execute($"INSERT INTO GREG_TECH_ORE_VEIN_ORES VALUES ('gtov~ore.mix.dual', '{DualOreOw}', 'Dualium', 'Stone', 'PRIMARY')");
        db.Execute($"INSERT INTO GREG_TECH_ORE_VEIN_ORES VALUES ('gtov~ore.mix.dual', '{DualOreMars}', 'Dualium', 'Mars', 'PRIMARY')");
        Recipe(db, "r_dual_macerate", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_dual_ore_ma", 0)], outputs: [(DualDust, 2, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_dual_smelt", "t_furnace", inputs: [("g_dual_dust", 0)], outputs: [(DualIngot, 1, 1.0)]);

        // Inertium never bootstraps: its pile loop starves and its one real recipe eats an
        // unreachable shard. The fallback tier must come from that recipe, not the pile packing.
        Recipe(db, "r_inert_pack", "rt~gregtech~gt.recipe.packager~ULV", inputs: [("g_inert_small4", 0)], outputs: [(InertDust, 1, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_inert_split", "rt~gregtech~gt.recipe.packager~ULV", inputs: [("g_inert_dust", 0)], outputs: [(InertSmall, 4, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_inert_real", "rt~gregtech~gt.recipe.mixer~HV", inputs: [("g_void", 0)], outputs: [(InertDust, 1, 1.0)], voltage: 512, duration: 100);

        db.Execute("INSERT INTO METADATA VALUES (0, 1754900000000, 'fixture')");
        return path;
    }

    private static void Item(SqliteConnection db, string id, string name, string mod) =>
        db.Execute(
            "INSERT INTO ITEM VALUES (@id, 'item/x.png', @name, 0, 1, @name, 0, 64, @mod, '', @name)",
            new { id, name, mod });

    private static void Fluid(SqliteConnection db, string id, string internalName, string name) =>
        db.Execute(
            "INSERT INTO FLUID VALUES (@id, 1000, 1, 0, 'fluid/x.png', @internalName, @name, 0, 'minecraft', '', 300, @name, 1000)",
            new { id, internalName, name });

    private static void Group(SqliteConnection db, string id, params (string ItemId, long Size)[] stacks)
    {
        foreach (var (itemId, size) in stacks)
        {
            db.Execute("INSERT INTO ITEM_GROUP_ITEM_STACKS VALUES (@id, @itemId, @size)", new { id, itemId, size });
        }
    }

    private static void Oredict(SqliteConnection db, string name, string groupId) =>
        db.Execute("INSERT INTO ORE_DICTIONARY VALUES (@id, @name, @groupId)", new { id = $"od_{name}", name, groupId });

    private static void Crop(
        SqliteConnection db, string cropId, string name, string seedId, bool hidden,
        string[] drops, string[] underBlocks)
    {
        var id = $"cnh~cropsnh:{cropId}";
        db.Execute(
            """
            INSERT INTO CROPS_NH_CROP(ID, CROP_ID, DROP_CHANCE, GROWTH_DURATION, HIDDEN,
                MACHINE_BREEDING_RECIPE_TIER, MAX_LIGHT_LEVEL, MIN_LIGHT_LEVEL, MIN_SEED_BED_TIER,
                NAME, SOIL_LIST_ID, TIER, SEED_ID)
            VALUES (@id, @cropId, 0.95, 600, @hidden, 1, NULL, NULL, -1, @name, 'dirt', 1, @seedId)
            """,
            new { id, cropId = $"cropsnh:{cropId}", hidden = hidden ? 1 : 0, name, seedId });
        foreach (var drop in drops)
        {
            db.Execute(
                "INSERT INTO CROPS_NH_CROP_DROPS(CROPS_NH_CROP_ID, DROPS_ITEM_ID, DROPS_WEIGHT) VALUES (@id, @drop, 500)",
                new { id, drop });
        }
        foreach (var block in underBlocks)
        {
            db.Execute(
                "INSERT INTO CROPS_NH_CROP_UNDER_BLOCKS(CROPS_NH_CROP_ID, UNDER_BLOCKS_ITEM_ID) VALUES (@id, @block)",
                new { id, block });
        }
    }

    private static void BlockDrop(SqliteConnection db, string blockName, string blockItemId, string dropId, int quantity) =>
        db.Execute(
            "INSERT INTO BLOCK_DROP(ID, BLOCK_META, BLOCK_NAME, QUANTITY, BLOCK_ITEM_ID, DROP_ID) VALUES (@id, 0, @blockName, @quantity, @blockItemId, @dropId)",
            new { id = $"bd~{blockName}~0", blockName, quantity, blockItemId, dropId });

    /// <summary>A recipe map and the machines serving it; only a multiblock earns the tier allowance.</summary>
    private static void RecipeMap(
        SqliteConnection db, string map, string name, (string ItemId, bool Multiblock, int? Tier)[] machines)
    {
        var id = $"gtrm~{map}";
        db.Execute(
            "INSERT INTO GREG_TECH_RECIPE_MAP(ID, AMPERAGE, HAS_MULTI_BLOCK, HAS_SINGLE_BLOCK, LOCALIZED_NAME, UNLOCALIZED_NAME) VALUES (@id, 1, @multi, @single, @name, @map)",
            new
            {
                id, name, map,
                multi = machines.Any(m => m.Multiblock) ? 1 : 0,
                single = machines.Any(m => !m.Multiblock) ? 1 : 0
            });
        foreach (var machine in machines)
        {
            db.Execute(
                "INSERT INTO GREG_TECH_RECIPE_MAP_MACHINES(GREG_TECH_RECIPE_MAP_ID, MACHINES_ITEM_ID, MACHINES_MULTIBLOCK, MACHINES_TIER) VALUES (@id, @itemId, @multiblock, @tier)",
                new { id, itemId = machine.ItemId, multiblock = machine.Multiblock ? 1 : 0, tier = machine.Tier });
        }
    }

    /// <summary>Single-block maps list a tiered machine family; multiblocks list few controllers.</summary>
    private static void RecipeType(
        SqliteConnection db, string id, string category, string type, int handlerIcons = 12, string? handlerItem = null)
    {
        db.Execute("INSERT INTO RECIPE_TYPE VALUES (@id, @category, @type)", new { id, category, type });
        for (var i = 0; i < handlerIcons; i++)
        {
            db.Execute("INSERT INTO RECIPE_TYPE_ITEM VALUES (@id, @iconId)", new { id, iconId = $"icon~{id}~{i}" });
        }
        if (handlerItem is not null)
        {
            db.Execute("INSERT INTO RECIPE_TYPE_ITEM VALUES (@id, @handlerItem)", new { id, handlerItem });
        }
    }

    private static void Recipe(
        SqliteConnection db, string id, string typeId,
        (string GroupId, int Slot)[] inputs,
        (string ItemId, long Amount, double Chance)[] outputs,
        long? voltage = null, long duration = 0, int? heat = null,
        (string GroupId, int Slot)[]? fluidInputs = null,
        (string ItemId, long Amount, double Chance, int Slot)[]? byproducts = null,
        string category = "", string? label = null)
    {
        db.Execute("INSERT INTO RECIPE VALUES (@id, @typeId)", new { id, typeId });
        foreach (var (groupId, slot) in inputs)
        {
            db.Execute("INSERT INTO RECIPE_ITEM_GROUP VALUES (@id, @groupId, @slot)", new { id, groupId, slot });
        }
        foreach (var (itemId, amount, chance) in outputs)
        {
            db.Execute(
                "INSERT INTO RECIPE_ITEM_OUTPUTS VALUES (@id, @itemId, @chance, @amount, 0)",
                new { id, itemId, chance, amount });
        }
        foreach (var (itemId, amount, chance, slot) in byproducts ?? [])
        {
            db.Execute(
                "INSERT INTO RECIPE_ITEM_OUTPUTS VALUES (@id, @itemId, @chance, @amount, @slot)",
                new { id, itemId, chance, amount, slot });
        }
        foreach (var (groupId, slot) in fluidInputs ?? [])
        {
            db.Execute("INSERT INTO RECIPE_FLUID_GROUP VALUES (@id, @groupId, @slot)", new { id, groupId, slot });
        }
        if (voltage is not null)
        {
            label ??= voltage <= 32 ? "LV" : voltage <= 128 ? "MV" : "HV";
            db.Execute(
                "INSERT INTO GREG_TECH_RECIPE VALUES (@gtId, 1, @duration, @voltage, @label, @category, 0, @id)",
                new { gtId = $"gtr~{id}", duration, voltage, label, category, id });
            if (heat is not null)
            {
                db.Execute(
                    "INSERT INTO GREG_TECH_RECIPE_METADATA VALUES (@gtId, 'coil_heat', @heat)",
                    new { gtId = $"gtr~{id}", heat });
            }
        }
    }
}
