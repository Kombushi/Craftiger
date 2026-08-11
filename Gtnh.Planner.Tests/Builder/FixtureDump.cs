using Microsoft.Data.Sqlite;

namespace Gtnh.Planner.Tests.Builder;

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
    public const string NaqDust = "i~gregtech~gt.metaitem.01~2324";
    public const string NaqIngot = "i~gregtech~gt.metaitem.01~11324";
    public const string FreezerItem = "i~gregtech~gt.blockmachines~1002";
    public const string ColdIngot = "i~gregtech~gt.metaitem.01~11999";
    public const string CopperOre = "i~gregtech~gt.blockores~35";
    public const string CopperDust = "i~gregtech~gt.metaitem.01~2035";
    public const string CopperIngot = "i~gregtech~gt.metaitem.01~11035";
    public const string AnnealedIngot = "i~gregtech~gt.metaitem.01~11345";
    public const string AnnealedDust = "i~gregtech~gt.metaitem.01~2345";
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

        Execute(db, """
            CREATE TABLE ITEM(ID TEXT, IMAGE_FILE_PATH TEXT, INTERNAL_NAME TEXT, ITEM_DAMAGE INTEGER,
                ITEM_ID INTEGER, LOCALIZED_NAME TEXT, MAX_DAMAGE INTEGER, MAX_STACK_SIZE INTEGER,
                MOD_ID TEXT, NBT TEXT, UNLOCALIZED_NAME TEXT);
            CREATE TABLE FLUID(ID TEXT, DENSITY INTEGER, FLUID_ID INTEGER, GASEOUS INTEGER,
                IMAGE_FILE_PATH TEXT, INTERNAL_NAME TEXT, LOCALIZED_NAME TEXT, LUMINOSITY INTEGER,
                MOD_ID TEXT, NBT TEXT, TEMPERATURE INTEGER, UNLOCALIZED_NAME TEXT, VISCOSITY INTEGER);
            CREATE TABLE RECIPE(ID TEXT, RECIPE_TYPE_ID TEXT);
            CREATE TABLE RECIPE_TYPE(ID TEXT, CATEGORY TEXT, TYPE TEXT);
            CREATE TABLE RECIPE_TYPE_ITEM(RECIPE_TYPE_ID TEXT, ICON_ID TEXT);
            CREATE TABLE GREG_TECH_RECIPE(ID TEXT, AMPERAGE INTEGER, DURATION INTEGER, VOLTAGE INTEGER, VOLTAGE_TIER TEXT, REQUIRES_CLEANROOM INTEGER, RECIPE_ID TEXT);
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
        Execute(db, $"INSERT INTO FLUID_CONTAINER VALUES ('fc_water', 1000, '{WaterCell}', '{EmptyCell}', '{Water}')");

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
        Execute(db, $"INSERT INTO FLUID_GROUP_FLUID_STACKS VALUES ('g_water', 1000, '{Water}')");

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
        Execute(db, $"INSERT INTO FLUID_GROUP_FLUID_STACKS VALUES ('g_oxygen', 63, '{Oxygen}')");
        Oredict(db, "oreCopper", "g_copper_ore");
        Oredict(db, "dustCopper", "g_copper_dust");
        Oredict(db, "ingotCopper", "g_copper_ingot");
        Oredict(db, "ingotAnnealedCopper", "g_annealed_ingot");
        Oredict(db, "dustAnnealedCopper", "g_annealed_dust");

        RecipeType(db, "t_shaped", "minecraft", "Crafting (Shaped)");
        RecipeType(db, "t_furnace", "minecraft", "Furnace");
        RecipeType(db, "t_ebf", "gregtech", "Blast Furnace (MV)", handlerIcons: 2);
        RecipeType(db, "t_extruder", "gregtech", "Extruder (MV)");
        RecipeType(db, "t_macerator", "gregtech", "Macerator (ULV)");
        RecipeType(db, "t_solidifier", "gregtech", "Fluid Solidifier (MV)");
        RecipeType(db, "t_fuels", "gregtech", "Large Boiler Fuels (ULV)");
        RecipeType(db, "t_electrolyzer", "gregtech", "Electrolyzer (MV)");
        RecipeType(db, "t_arc", "gregtech", "Arc Furnace (LV)");
        RecipeType(db, "t_alloy", "gregtech", "Alloy Smelter (ULV)");
        RecipeType(db, "t_freezer", "gregtech", "Vacuum Freezer (MV)", handlerIcons: 0, handlerItem: FreezerItem);

        // Ingot <-> block cycle, both directions on the crafting table.
        Recipe(db, "r_block", "t_shaped", inputs: [("g_bronze_ingot9", 0)], outputs: [(BronzeBlock, 1, 1.0)]);
        Recipe(db, "r_unblock", "t_shaped", inputs: [("g_bronze_block", 0)], outputs: [(GtBronze, 9, 1.0)]);
        Recipe(db, "r_alu_block", "t_shaped", inputs: [("g_alu_ingot9", 0)], outputs: [(AluBlock, 1, 1.0)]);
        Recipe(db, "r_alu_unblock", "t_shaped", inputs: [("g_alu_block", 0)], outputs: [(AluIngot, 9, 1.0)]);

        // Smelting bronze dust is the tier-0 route; EBF is aluminium's only real route.
        Recipe(db, "r_smelt", "t_furnace", inputs: [("g_bronze_dust", 0)], outputs: [(GtBronze, 1, 1.0)]);
        Recipe(db, "r_ebf", "t_ebf", inputs: [("g_alu_dust", 0)], outputs: [(AluIngot, 1, 1.0)], voltage: 120, duration: 500, heat: 1700);

        // Chanced byproduct plus a saw catalyst on the grid.
        Recipe(db, "r_macerate", "t_macerator", inputs: [("g_bronze_ingot", 0)], outputs: [(BronzeDust, 1, 1.0), (BronzeDust, 1, 0.9)], voltage: 4, duration: 100);
        Recipe(db, "r_planks", "t_shaped", inputs: [("g_log", 0), ("g_saw", 1)], outputs: [(Plank, 4, 1.0)]);

        // Extruder-only rod with a zero-size shape mold; solidifier gives a pinnable alternative.
        Recipe(db, "r_extrude", "t_extruder", inputs: [("g_alu_ingot", 0), ("g_mold", 1)], outputs: [(AluRod, 2, 1.0)], voltage: 96, duration: 200);
        Recipe(db, "r_solidify", "t_solidifier", inputs: [("g_alu_ingot", 0)], outputs: [(AluRod, 1, 1.0)], voltage: 24, duration: 100, fluidInputs: [("g_water", 0)]);

        // Fuel tabs are pseudo-recipes and must be dropped.
        Recipe(db, "r_fuel", "t_fuels", inputs: [("g_bronze_dust", 0)], outputs: [(BronzeDust, 1, 1.0)], voltage: 32, duration: 1);

        // Distinct materials share the wildcard ingotAnyIron group but must stay separate.
        Recipe(db, "r_iron_use", "t_shaped", inputs: [("g_iron", 0)], outputs: [(Plank, 1, 1.0)]);
        Recipe(db, "r_cast_use", "t_shaped", inputs: [("g_cast_iron", 0)], outputs: [(Plank, 1, 1.0)]);

        // Annealed copper: real era is the LV arc route; the dust-smelting loop
        // must inherit it rather than grant era 0.
        Recipe(db, "r_cu_macerate", "t_macerator", inputs: [("g_copper_ore", 0)], outputs: [(CopperDust, 2, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_cu_hammer", "t_shaped", inputs: [("g_copper_ore", 0)], outputs: [(CopperDust, 1, 1.0)]);
        Recipe(db, "r_alu_macerate", "t_macerator", inputs: [("g_alu_ore", 0)], outputs: [(AluDust, 2, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_naq_macerate", "t_macerator", inputs: [("g_naq_ore", 0)], outputs: [(NaqDust, 2, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_naq_smelt", "t_alloy", inputs: [("g_naq_dust", 0)], outputs: [(NaqIngot, 1, 1.0)], voltage: 16, duration: 100);

        // The freezer itself is naquadah-era, gating its recipes regardless of voltage.
        Recipe(db, "r_freezer_craft", "t_shaped", inputs: [("g_naq_ingot", 0)], outputs: [(FreezerItem, 1, 1.0)]);
        Recipe(db, "r_freeze", "t_freezer", inputs: [("g_alu_ingot", 0)], outputs: [(ColdIngot, 1, 1.0)], voltage: 30, duration: 100);
        Recipe(db, "r_cu_smelt", "t_furnace", inputs: [("g_copper_dust", 0)], outputs: [(CopperIngot, 1, 1.0)]);
        Recipe(db, "r_oxygen", "t_electrolyzer", inputs: [], outputs: [], voltage: 30, duration: 100, fluidInputs: [("g_water", 0)]);
        Recipe(db, "r_anneal", "t_arc", inputs: [("g_copper_ingot", 0)], outputs: [(AnnealedIngot, 1, 1.0)], voltage: 30, duration: 100, fluidInputs: [("g_oxygen", 0)]);
        Recipe(db, "r_ann_macerate", "t_macerator", inputs: [("g_annealed_ingot", 0)], outputs: [(AnnealedDust, 1, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_ann_smelt", "t_furnace", inputs: [("g_annealed_dust", 0)], outputs: [(AnnealedIngot, 1, 1.0)]);
        Execute(db, $"INSERT INTO RECIPE_FLUID_OUTPUTS VALUES ('r_oxygen', 500, '{Oxygen}', NULL, 0)");

        // Cell-based recipe: decomposition plus netting must leave only fluids.
        Group(db, "g_water_cell", (WaterCell, 1));
        Recipe(db, "r_electrolyze", "t_electrolyzer", inputs: [("g_water_cell", 0)], outputs: [(EmptyCell, 1, 1.0)], voltage: 30, duration: 300);
        Execute(db, $"INSERT INTO RECIPE_FLUID_OUTPUTS VALUES ('r_electrolyze', 1000, '{Hydrogen}', NULL, 0)");

        Execute(db, "INSERT INTO METADATA VALUES (0, 1754900000000, 'fixture')");
        return path;
    }

    private static void Item(SqliteConnection db, string id, string name, string mod) =>
        Execute(db, $"INSERT INTO ITEM VALUES ('{id}', 'item/x.png', '{name}', 0, 1, '{name}', 0, 64, '{mod}', '', '{name}')");

    private static void Fluid(SqliteConnection db, string id, string internalName, string name) =>
        Execute(db, $"INSERT INTO FLUID VALUES ('{id}', 1000, 1, 0, 'fluid/x.png', '{internalName}', '{name}', 0, 'minecraft', '', 300, '{name}', 1000)");

    private static void Group(SqliteConnection db, string id, params (string ItemId, long Size)[] stacks)
    {
        foreach (var (itemId, size) in stacks)
            Execute(db, $"INSERT INTO ITEM_GROUP_ITEM_STACKS VALUES ('{id}', '{itemId}', {size})");
    }

    private static void Oredict(SqliteConnection db, string name, string groupId) =>
        Execute(db, $"INSERT INTO ORE_DICTIONARY VALUES ('od_{name}', '{name}', '{groupId}')");

    /// <summary>Single-block maps list a tiered machine family; multiblocks list few controllers.</summary>
    private static void RecipeType(
        SqliteConnection db, string id, string category, string type, int handlerIcons = 12, string? handlerItem = null)
    {
        Execute(db, $"INSERT INTO RECIPE_TYPE VALUES ('{id}', '{category}', '{type}')");
        for (var i = 0; i < handlerIcons; i++)
            Execute(db, $"INSERT INTO RECIPE_TYPE_ITEM VALUES ('{id}', 'icon~{id}~{i}')");
        if (handlerItem is not null)
            Execute(db, $"INSERT INTO RECIPE_TYPE_ITEM VALUES ('{id}', '{handlerItem}')");
    }

    private static void Recipe(
        SqliteConnection db, string id, string typeId,
        (string GroupId, int Slot)[] inputs,
        (string ItemId, long Amount, double Chance)[] outputs,
        long? voltage = null, long duration = 0, int? heat = null,
        (string GroupId, int Slot)[]? fluidInputs = null)
    {
        Execute(db, $"INSERT INTO RECIPE VALUES ('{id}', '{typeId}')");
        foreach (var (groupId, slot) in inputs)
            Execute(db, $"INSERT INTO RECIPE_ITEM_GROUP VALUES ('{id}', '{groupId}', {slot})");
        foreach (var (itemId, amount, chance) in outputs)
            Execute(db, $"INSERT INTO RECIPE_ITEM_OUTPUTS VALUES ('{id}', '{itemId}', {chance}, {amount}, 0)");
        foreach (var (groupId, slot) in fluidInputs ?? [])
            Execute(db, $"INSERT INTO RECIPE_FLUID_GROUP VALUES ('{id}', '{groupId}', {slot})");
        if (voltage is not null)
        {
            var label = voltage <= 32 ? "LV" : voltage <= 128 ? "MV" : "HV";
            Execute(db, $"INSERT INTO GREG_TECH_RECIPE VALUES ('gtr~{id}', 1, {duration}, {voltage}, '{label}', 0, '{id}')");
            if (heat is not null)
                Execute(db, $"INSERT INTO GREG_TECH_RECIPE_METADATA VALUES ('gtr~{id}', 'coil_heat', {heat})");
        }
    }

    private static void Execute(SqliteConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}