using Dapper;
using Microsoft.Data.Sqlite;

namespace Craftiger.Builder.UnitTests;

/// <summary>Hand-written mini NESQL dump exercising every builder rule once.</summary>
public static partial class FixtureDump
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
    public const string OakLeaves = "i~minecraft~leaves~0";
    public const string CherryLeaves = "i~etfuturum~leaves~1";
    public const string FixturePipe = "i~IC2~blockFixturePipe~0";
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
    public const string Ic2Steam = "f~IC2~ic2steam";
    public const string BronzeBoiler = "i~gregtech~gt.blockmachines~15529";
    public const string SteamTurbine = "i~gregtech~gt.blockmachines~1120";
    public const string DeadTurbine = "i~gregtech~gt.blockmachines~7001";
    public const string LiveTurbine = "i~gregtech~gt.blockmachines~7002";
    public const string XlTurbine = "i~gregtech~gt.blockmachines~15522";
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
    public const string TargetVanilla = "i~minecraft~targetium~0";
    public const string TargetGt = "i~gregtech~gt.metaitem.01~11900";
    public const string PureMetal = "i~gregtech~gt.metaitem.01~11901";
    public const string BlackMetal = "i~tconstruct~blackium~0";
    public const string PlanetDust = "i~GalaxySpace~planetdust~0";
    public const string FlawedGem = "i~gregtech~gt.metaitem.01~8501";
    public const string ExquisiteGem = "i~gregtech~gt.metaitem.01~8502";
    public const string CopperWire = "i~gregtech~gt.metaitem.02~30035";
    public const string SteamGrinder = "i~gregtech~gt.blockmachines~104";
    public const string SteamIngot = "i~gregtech~gt.metaitem.01~11888";
    public const string TinkerSaw = "i~tconstruct~fixture_saw~3";
    public const string SoupBucket = "i~fixture~soup_bucket~0";
    public const string BrewCell = "i~gregtech~gt.metaitem.01~30002";
    public const string BronzeSmall = "i~gregtech~gt.metaitem.01~1300";
    public const string FixtureShard = "i~fixture~shard~0";
    public const string ShardDust = "i~fixture~shard_dust~0";
    public const string ClayDust = "i~fixture~clay_dust~0";
    public const string DataBox = "i~fixture~databox~0";
    public const string DataGhost = "i~fixture~dataghost~0";
    public const string FixtureWidget = "i~fixture~widget~0";
    public const string TreeFarm = "i~gregtech~gt.blockmachines~15541";
    public const string OakSapling = "i~minecraft~sapling~0";
    public const string PineLog = "i~minecraft~log~1";
    public const string Chainsaw = "i~gregtech~gt.metatool.01~110";
    public const string BranchCutter = "i~gregtech~gt.metatool.01~30";
    public const string WireCutterLv = "i~gregtech~gt.metatool.01~196";

    public static string Create(string directory)
    {
        var path = Path.Combine(directory, "dump.sqlite");
        using var db = new SqliteConnection($"Data Source={path}");
        db.Open();

        CreateSchema(db);
        AddItems(db);
        AddRecipes(db);
        AddMachines(db);
        AddWorldgen(db);

        db.Execute("INSERT INTO METADATA VALUES (0, 1754900000000, 'fixture')");
        return path;
    }

    private static void CreateSchema(SqliteConnection db) =>
        db.Execute("""
            CREATE TABLE ITEM(ID TEXT, IMAGE_FILE_PATH TEXT, INTERNAL_NAME TEXT, ITEM_DAMAGE INTEGER,
                ITEM_ID INTEGER, LOCALIZED_NAME TEXT, MAX_DAMAGE INTEGER, MAX_STACK_SIZE INTEGER,
                MOD_ID TEXT, NBT TEXT, UNLOCALIZED_NAME TEXT);
            CREATE TABLE FLUID(ID TEXT, DENSITY INTEGER, FLUID_ID INTEGER, GASEOUS INTEGER,
                IMAGE_FILE_PATH TEXT, INTERNAL_NAME TEXT, LOCALIZED_NAME TEXT, LUMINOSITY INTEGER,
                MOD_ID TEXT, NBT TEXT, TEMPERATURE INTEGER, UNLOCALIZED_NAME TEXT, VISCOSITY INTEGER);
            CREATE TABLE RECIPE(ID TEXT, RECIPE_TYPE_ID TEXT);
            CREATE TABLE RECIPE_TYPE(ID TEXT, CATEGORY TEXT, TYPE TEXT, SHAPELESS INTEGER);
            CREATE TABLE RECIPE_TYPE_ITEM(RECIPE_TYPE_ID TEXT, ICON_ID TEXT);
            CREATE TABLE GREG_TECH_RECIPE(ID TEXT, AMPERAGE INTEGER, DURATION INTEGER, VOLTAGE INTEGER, VOLTAGE_TIER TEXT, RECIPE_CATEGORY TEXT, REQUIRES_CLEANROOM INTEGER, REQUIRES_LOW_GRAVITY INTEGER, RECIPE_SPECIAL_VALUE INTEGER, ADDITIONAL_INFO TEXT, RECIPE_ID TEXT);
            CREATE TABLE GREG_TECH_RECIPE_METADATA(GREG_TECH_RECIPE_ID TEXT, METADATA_KEY TEXT, METADATA_VALUE INTEGER);
            CREATE TABLE GREG_TECH_RECIPE_ITEM(GREG_TECH_RECIPE_ID TEXT, SPECIAL_ITEMS_ID TEXT);
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
                HAS_SINGLE_BLOCK INTEGER, IS_FUEL INTEGER, LOCALIZED_NAME TEXT, UNLOCALIZED_NAME TEXT);
            CREATE TABLE GREG_TECH_RECIPE_MAP_MACHINES(GREG_TECH_RECIPE_MAP_ID TEXT, MACHINES_ITEM_ID TEXT,
                MACHINES_MULTIBLOCK INTEGER, MACHINES_TIER INTEGER, MACHINES_STEAM INTEGER);
            CREATE TABLE MOB_INFO(ID TEXT, ALLOWED_IN_PEACEFUL INTEGER, ALLOWED_INFERNAL INTEGER,
                ALWAYS_INFERNAL INTEGER, SOUL_VIAL_USABLE INTEGER, MOB_ID TEXT);
            CREATE TABLE MOB_INFO_DROPS(MOB_INFO_ID TEXT, DROPS_ITEM_ID TEXT, DROPS_LOOTABLE INTEGER,
                DROPS_PLAYER_ONLY INTEGER, DROPS_PROBABILITY REAL, DROPS_STACK_SIZE INTEGER,
                DROPS_TYPE TEXT);
            CREATE TABLE GREG_TECH_GENERATOR(ID TEXT, AMPERES_OUT INTEGER, EFFICIENCY REAL,
                MAX_EU_OUTPUT INTEGER, ITEM_ID TEXT);
            CREATE TABLE GREG_TECH_DYNAMO(ID TEXT, AMPERES_OUT INTEGER, MAX_EU_OUTPUT INTEGER,
                MAX_EU_STORE INTEGER, ITEM_ID TEXT);
            CREATE TABLE GREG_TECH_LARGE_BOILER(ID TEXT, EFFICIENCY_INCREASE INTEGER, EUT INTEGER,
                ITEM_ID TEXT);
            CREATE TABLE GREG_TECH_MULTIBLOCK_MACHINE(ID TEXT, MAX_PARALLEL_RECIPES INTEGER, ITEM_ID TEXT);
            CREATE TABLE GREG_TECH_MULTIBLOCK_MACHINE_BONUSES(GREG_TECH_MULTIBLOCK_MACHINE_ID TEXT,
                BONUSES_BONUS_VALUE REAL, BONUSES_KIND TEXT, BONUSES_MULTIPLICATIVE INTEGER,
                BONUSES_SOURCE_LINE TEXT, BONUSES_TIER_AXIS TEXT);
            CREATE TABLE GREG_TECH_TURBINE_ROTOR(ID TEXT, BASE_EFFICIENCY REAL, MATERIAL_NAME TEXT,
                MAX_DURABILITY INTEGER, OVERFLOW_EFFICIENCY INTEGER, SIZE TEXT, ITEM_ID TEXT);
            CREATE TABLE GREG_TECH_TURBINE_ROTOR_FUEL_STATS(GREG_TECH_TURBINE_ROTOR_ID TEXT,
                FUEL_STATS_EFFICIENCY REAL, FUEL_STATS_FUEL TEXT, FUEL_STATS_LOOSE_EFFICIENCY REAL,
                FUEL_STATS_LOOSE_OPTIMAL_EUT REAL, FUEL_STATS_LOOSE_OPTIMAL_FLOW REAL,
                FUEL_STATS_OPTIMAL_EUT REAL, FUEL_STATS_OPTIMAL_FLOW REAL);
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
            CREATE TABLE GREG_TECH_ORE_DICT_UNIFICATION(ID TEXT, NAME TEXT, TARGET_ID TEXT);
            CREATE TABLE GREG_TECH_UNIFICATION_BLACKLIST(ID TEXT, ITEM_ID TEXT);
            CREATE TABLE GREG_TECH_ORE_PREFIX(ID TEXT, NAME TEXT, UNIFIABLE INTEGER,
                SELF_REFERENCING INTEGER, MATERIAL_BASED INTEGER, CONTAINER INTEGER,
                RECYCLABLE INTEGER, MATERIAL_AMOUNT INTEGER);
            CREATE TABLE ITEM_CONTAINER(ID TEXT, CONTAINER_ITEM_ID TEXT, ITEM_ID TEXT);
            CREATE TABLE GREG_TECH_ITEM_DATA(ID TEXT, MATERIAL_AMOUNT INTEGER, MATERIAL_NAME TEXT,
                PREFIX_NAME TEXT, ITEM_ID TEXT);
            CREATE TABLE GREG_TECH_ITEM_DATA_BY_PRODUCTS(GREG_TECH_ITEM_DATA_ID TEXT,
                BY_PRODUCTS_AMOUNT INTEGER, BY_PRODUCTS_MATERIAL_NAME TEXT, BY_PRODUCTS_ORDER INTEGER);
            CREATE TABLE METADATA(ID INTEGER, CREATION_TIME_MILLIS INTEGER, VERSION TEXT);
            """);

    private static void Item(
        SqliteConnection db, string id, string name, string mod, long maxDamage = 0, long maxStack = 64) =>
        db.Execute(
            "INSERT INTO ITEM VALUES (@id, 'item/x.png', @name, 0, 1, @name, @maxDamage, @maxStack, @mod, '', @name)",
            new { id, name, mod, maxDamage, maxStack });

    private static void ItemContainer(SqliteConnection db, string itemId, string containerId) =>
        db.Execute(
            "INSERT INTO ITEM_CONTAINER VALUES (@id, @containerId, @itemId)",
            new { id = "ic~" + itemId, containerId, itemId });

    private static void ItemData(
        SqliteConnection db, string itemId, string material, long amount,
        params (string Material, long Amount)[] byproducts)
    {
        db.Execute(
            "INSERT INTO GREG_TECH_ITEM_DATA VALUES (@id, @amount, @material, NULL, @itemId)",
            new { id = "gtid~" + itemId, amount, material, itemId });
        var order = 0;
        foreach (var (byMaterial, byAmount) in byproducts)
        {
            db.Execute(
                "INSERT INTO GREG_TECH_ITEM_DATA_BY_PRODUCTS VALUES (@id, @byAmount, @byMaterial, @order)",
                new { id = "gtid~" + itemId, byAmount, byMaterial, order = order++ });
        }
    }

    private static void Fluid(SqliteConnection db, string id, string internalName, string name) =>
        db.Execute(
            "INSERT INTO FLUID VALUES (@id, 1000, 1, 0, 'fluid/x.png', @internalName, @name, 0, 'minecraft', '', 300, @name, 1000)",
            new { id, internalName, name });

    private static void FluidStack(SqliteConnection db, string groupId, long amount, string fluidId) =>
        db.Execute(
            "INSERT INTO FLUID_GROUP_FLUID_STACKS VALUES (@groupId, @amount, @fluidId)",
            new { groupId, amount, fluidId });

    private static void FluidContainer(
        SqliteConnection db, string id, long amount, string containerId, string emptyId, string fluidId) =>
        db.Execute(
            "INSERT INTO FLUID_CONTAINER VALUES (@id, @amount, @containerId, @emptyId, @fluidId)",
            new { id, amount, containerId, emptyId, fluidId });

    private static void Group(SqliteConnection db, string id, params (string ItemId, long Size)[] stacks)
    {
        foreach (var (itemId, size) in stacks)
        {
            db.Execute("INSERT INTO ITEM_GROUP_ITEM_STACKS VALUES (@id, @itemId, @size)", new { id, itemId, size });
        }
    }

    private static void Oredict(SqliteConnection db, string name, string groupId) =>
        db.Execute("INSERT INTO ORE_DICTIONARY VALUES (@id, @name, @groupId)", new { id = $"od_{name}", name, groupId });

    private static void OrePrefix(
        SqliteConnection db, string name, long materialAmount, bool container = false,
        bool selfReferencing = false, bool recyclable = false) =>
        db.Execute("""
            INSERT INTO GREG_TECH_ORE_PREFIX(ID, NAME, UNIFIABLE, SELF_REFERENCING, MATERIAL_BASED,
                CONTAINER, RECYCLABLE, MATERIAL_AMOUNT)
            VALUES (@id, @name, 1, @selfReferencing, 1, @container, @recyclable, @materialAmount)
            """,
            new
            {
                id = "gtop~" + name, name, selfReferencing, container, recyclable, materialAmount
            });

    private static void Unify(SqliteConnection db, string name, string targetId) =>
        db.Execute(
            "INSERT INTO GREG_TECH_ORE_DICT_UNIFICATION VALUES (@id, @name, @targetId)",
            new { id = $"gtodu~{name}", name, targetId });

    private static void Blacklist(SqliteConnection db, string itemId) =>
        db.Execute(
            "INSERT INTO GREG_TECH_UNIFICATION_BLACKLIST VALUES (@id, @itemId)",
            new { id = $"gtub~{itemId}", itemId });

    private static void Tooltip(SqliteConnection db, string itemId, string tooltip, int order) =>
        db.Execute("INSERT INTO ITEM_TOOLTIP VALUES (@itemId, @tooltip, @order)", new { itemId, tooltip, order });

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
        SqliteConnection db, string map, string name,
        (string ItemId, bool Multiblock, int? Tier, bool Steam)[] machines, bool isFuel = false)
    {
        var id = $"gtrm~{map}";
        db.Execute(
            "INSERT INTO GREG_TECH_RECIPE_MAP(ID, AMPERAGE, HAS_MULTI_BLOCK, HAS_SINGLE_BLOCK, IS_FUEL, LOCALIZED_NAME, UNLOCALIZED_NAME) VALUES (@id, 1, @multi, @single, @isFuel, @name, @map)",
            new
            {
                id, name, map, isFuel,
                multi = machines.Any(m => m.Multiblock) ? 1 : 0,
                single = machines.Any(m => !m.Multiblock) ? 1 : 0
            });
        foreach (var machine in machines)
        {
            db.Execute(
                "INSERT INTO GREG_TECH_RECIPE_MAP_MACHINES(GREG_TECH_RECIPE_MAP_ID, MACHINES_ITEM_ID, MACHINES_MULTIBLOCK, MACHINES_TIER, MACHINES_STEAM) VALUES (@id, @itemId, @multiblock, @tier, @steam)",
                new
                {
                    id, itemId = machine.ItemId, multiblock = machine.Multiblock ? 1 : 0,
                    tier = machine.Tier, steam = machine.Steam ? 1 : 0
                });
        }
    }

    /// <summary>Single-block maps list a tiered machine family; multiblocks list few controllers.</summary>
    private static void RecipeType(
        SqliteConnection db, string id, string category, string type, int handlerIcons = 12, string? handlerItem = null,
        bool shapeless = true)
    {
        db.Execute("INSERT INTO RECIPE_TYPE VALUES (@id, @category, @type, @shapeless)",
            new { id, category, type, shapeless = shapeless ? 1 : 0 });
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
        string category = "", string? label = null,
        long amperage = 1, bool cleanroom = false, bool lowGravity = false,
        long? specialValue = null, string? additionalInfo = null)
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
        if (voltage is not null || label is not null)
        {
            label ??= voltage <= 32 ? "LV" : voltage <= 128 ? "MV" : "HV";
            db.Execute(
                "INSERT INTO GREG_TECH_RECIPE VALUES (@gtId, @amperage, @duration, @voltage, @label, @category, @cleanroom, @lowGravity, @specialValue, @additionalInfo, @id)",
                new
                {
                    gtId = $"gtr~{id}", amperage, duration, voltage, label, category,
                    cleanroom = cleanroom ? 1 : 0, lowGravity = lowGravity ? 1 : 0,
                    specialValue, additionalInfo, id,
                });
            if (heat is not null)
            {
                db.Execute(
                    "INSERT INTO GREG_TECH_RECIPE_METADATA VALUES (@gtId, 'coil_heat', @heat)",
                    new { gtId = $"gtr~{id}", heat });
            }
        }
    }

    /// <summary>The controller-slot item a GregTech map shows beside a recipe's inputs.</summary>
    private static void SpecialItem(SqliteConnection db, string recipeId, string itemId) =>
        db.Execute(
            "INSERT INTO GREG_TECH_RECIPE_ITEM VALUES (@gtId, @itemId)",
            new { gtId = $"gtr~{recipeId}", itemId });

    private static void FluidOutput(SqliteConnection db, string recipeId, long amount, string fluidId) =>
        db.Execute(
            "INSERT INTO RECIPE_FLUID_OUTPUTS VALUES (@recipeId, @amount, @fluidId, NULL, 0)",
            new { recipeId, amount, fluidId });
}
