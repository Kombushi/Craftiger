using Dapper;
using Microsoft.Data.Sqlite;

namespace Craftiger.Builder.UnitTests;

/// <summary>Fuels of every family and the machine stat tables: generators, dynamos, boilers, multiblocks, rotors.</summary>
public static partial class FixtureDump
{
    private static void AddMachines(SqliteConnection db)
    {
        // Fuel tabs are pseudo-recipes and must be dropped.
        Recipe(db, "r_fuel", "rt~gregtech~gt.recipe.largeboilerfakefuels~ULV", inputs: [("g_bronze_dust", 0)], outputs: [(BronzeDust, 1, 1.0)], voltage: 32, duration: 1,
            additionalInfo: "Burn time in seconds:\nBronze Boiler: 2\nSteel Boiler: 1\nTitanium Boiler: Not allowed\nTungstenst. Boiler: Not allowed");

        // One fuel per family trap: the benzene anchor, a small-volume cell whose special value
        // still reads per mB, a bare solid worth 1000 mB, a lifetime pellet, and a timed fluid.
        Fluid(db, "f~benzene", "benzene", "Benzene");
        Item(db, "i~fixture~benzene_cell", "Benzene Cell", "fixture");
        FluidContainer(db, "fc_benzene", 1000, "i~fixture~benzene_cell", EmptyCell, "f~benzene");
        Group(db, "g_benzene_cell", ("i~fixture~benzene_cell", 1));
        Recipe(db, "r_fuel_benzene", "rt~gregtech~gt.recipe.gasturbinefuel~ULV", inputs: [("g_benzene_cell", 0)], outputs: [], label: "ULV", specialValue: 360);
        Fluid(db, "f~fixture_plasma", "fixture_plasma", "Fixture Plasma");
        Item(db, "i~fixture~plasma_cell", "Fixture Plasma Cell", "fixture");
        FluidContainer(db, "fc_fixture_plasma", 144, "i~fixture~plasma_cell", EmptyCell, "f~fixture_plasma");
        Group(db, "g_fixture_plasma_cell", ("i~fixture~plasma_cell", 1));
        Recipe(db, "r_fuel_plasma", "rt~gregtech~gt.recipe.gasturbinefuel~ULV", inputs: [("g_fixture_plasma_cell", 0)], outputs: [], label: "ULV", specialValue: 999);
        Item(db, "i~fixture~solid_fuel", "Fixture Solid Fuel", "fixture");
        Group(db, "g_solid_fuel", ("i~fixture~solid_fuel", 1));
        Recipe(db, "r_fuel_solid", "rt~gregtech~gt.recipe.gasturbinefuel~ULV", inputs: [("g_solid_fuel", 0)], outputs: [], label: "ULV", specialValue: 20);
        Item(db, "i~fixture~rtg_pellet", "Fixture Pellet", "fixture");
        Group(db, "g_rtg_pellet", ("i~fixture~rtg_pellet", 1));
        Recipe(db, "r_fuel_rtg", "rt~gregtech~gtpp.recipe.RTGgenerators~ULV", inputs: [("g_rtg_pellet", 0)], outputs: [], voltage: 480, specialValue: 1);
        Fluid(db, "f~fixture_naq_fuel", "fixture_naq_fuel", "Fixture Naquadah Fuel");
        FluidStack(db, "g_naq_fuel", 1, "f~fixture_naq_fuel");
        Fluid(db, "f~fixture_naq_depleted", "fixture_naq_depleted", "Fixture Depleted Fuel");
        Recipe(db, "r_fuel_timed", "rt~gregtech~gg.recipe.naquadah_reactor~ULV", inputs: [], outputs: [], label: "ULV", specialValue: 1600000, duration: 160, fluidInputs: [("g_naq_fuel", 0)]);
        FluidOutput(db, "r_fuel_timed", 1, "f~fixture_naq_depleted");

        // The boosted-generator blocks with their dump-exported constants and mode tables.
        Item(db, LargeNaquadahReactor, "Large Naquadah Reactor", "gregtech");
        Item(db, LargeCombustionEngine, "Large Combustion Engine", "gregtech");
        db.Execute(
            $"INSERT INTO GREG_TECH_COMBUSTION_ENGINE VALUES ('gteng~1', 1, 2, 30000, 10000, 2048, "
            + $"'f~gregtech~oxygen', 'f~gregtech~lubricant', '{LargeCombustionEngine}')");
        ReactorMode(db, LargeNaquadahReactor, "UPKEEP", "f~gregtech~liquidair", 2400, null);
        ReactorMode(db, LargeNaquadahReactor, "COOLANT", "f~IC2~ic2coolant", 1000, 105);
        ReactorMode(db, LargeNaquadahReactor, "COOLANT", "f~gregtech~supercoolant", 1000, 150);
        ReactorMode(db, LargeNaquadahReactor, "COOLANT", "f~miscutils~cryotheum", 1000, 275);
        ReactorMode(db, LargeNaquadahReactor, "COOLANT", "f~gregtech~temporalfluid", 20, 500);
        ReactorMode(db, LargeNaquadahReactor, "EXCITED", "f~gregtech~molten.caesium", 180, 2);
        ReactorMode(db, LargeNaquadahReactor, "EXCITED", "f~gregtech~molten.uranium235", 180, 3);
        ReactorMode(db, LargeNaquadahReactor, "EXCITED", "f~gregtech~molten.naquadah", 20, 4);
        ReactorMode(db, LargeNaquadahReactor, "EXCITED", "f~bartworks~molten.atomic separation catalyst", 20, 16);
        ReactorMode(db, LargeNaquadahReactor, "EXCITED", "f~gregtech~spatialfluid", 20, 64);
        Fluid(db, "f~gregtech~oxygen", "oxygen", "Oxygen");
        Fluid(db, "f~gregtech~lubricant", "lubricant", "Lubricant");
        Fluid(db, "f~gregtech~liquidair", "liquidair", "Liquid Air");
        Fluid(db, "f~IC2~ic2coolant", "ic2coolant", "IC2 Coolant");
        Fluid(db, "f~gregtech~supercoolant", "supercoolant", "Super Coolant");
        Fluid(db, "f~miscutils~cryotheum", "cryotheum", "Gelid Cryotheum");
        Fluid(db, "f~gregtech~temporalfluid", "temporalfluid", "Tachyon Rich Temporal Fluid");
        Fluid(db, "f~gregtech~molten.caesium", "molten.caesium", "Molten Caesium");
        Fluid(db, "f~gregtech~molten.uranium235", "molten.uranium235", "Molten Uranium 235");
        Fluid(db, "f~gregtech~molten.naquadah", "molten.naquadah", "Molten Naquadah");
        Fluid(db, "f~bartworks~molten.atomic separation catalyst", "molten.asc", "Molten Atomic Separation Catalyst");
        Fluid(db, "f~gregtech~spatialfluid", "spatialfluid", "Spatially Enlarged Fluid");

        // Machine stat tables: a generator, a dynamo, a boiler, the EBF's bonuses, one rotor.
        Item(db, "i~fixture~gas_turbine", "Fixture Gas Turbine", "fixture");
        db.Execute("INSERT INTO GREG_TECH_GENERATOR VALUES ('gtgen~1', 1, 95, 32, 'i~fixture~gas_turbine')");
        Item(db, DeadTurbine, "Large Gas Turbine", "gregtech");
        Tooltip(db, DeadTurbine, "§4DEPRECATED - Controller will be removed in next major update!", 1);
        Item(db, LiveTurbine, "Large Gas Turbine", "gregtech");
        Item(db, XlTurbine, "XL Turbo Gas Turbine", "gregtech");
        Item(db, "i~fixture~dynamo", "Fixture Dynamo Hatch", "fixture");
        db.Execute("INSERT INTO GREG_TECH_DYNAMO VALUES ('gtdyn~1', 4, 512, 8192, 'i~fixture~dynamo')");
        Item(db, "i~fixture~boiler", "Fixture Large Boiler", "fixture");
        db.Execute("INSERT INTO GREG_TECH_LARGE_BOILER VALUES ('gtlb~1', 16, 480, 'i~fixture~boiler')");
        Item(db, BronzeBoiler, "Large Bronze Boiler", "gregtech");
        db.Execute($"INSERT INTO GREG_TECH_LARGE_BOILER VALUES ('gtlb~2', 16, 480, '{BronzeBoiler}')");
        Machine(db, BronzeBoiler, "gregtech.common.tileentities.machines.multi.MTELargeBoilerBronze", multiblock: true);
        Item(db, SteamTurbine, "Basic Steam Turbine", "gregtech");
        db.Execute($"INSERT INTO GREG_TECH_GENERATOR VALUES ('gtgen~2', 1, 85.714285714285708, 32, '{SteamTurbine}')");
        Machine(db, SteamTurbine, "gregtech.common.tileentities.generators.MTESteamTurbine", tier: 1);
        db.Execute($"INSERT INTO GREG_TECH_MULTIBLOCK_MACHINE VALUES ('gtmb~1', 8, '{EbfController}')");
        db.Execute("INSERT INTO GREG_TECH_MULTIBLOCK_MACHINE_BONUSES VALUES ('gtmb~1', 8, 'PARALLEL', 0, '8 Parallels', NULL)");
        db.Execute("INSERT INTO GREG_TECH_MULTIBLOCK_MACHINE_BONUSES VALUES ('gtmb~1', 2, 'PARALLEL_PER_TIER', 1, '2x Parallels per Heating Coil Tier', 'COIL')");

        // Machine classes: the tree farm map is found through its controller's class, the XL
        // turbine runs its exported slot count as parallels, and the class names its rotor fuel.
        Machine(db, TreeFarm, "gregtech.common.tileentities.machines.multi.MTETreeFarm", multiblock: true);
        Machine(db, CropManagerLv, "com.gtnewhorizon.cropsnh.tileentity.singleblock.MTECropManager", tier: 1);
        Machine(db, IndustrialFarm, "com.gtnewhorizon.cropsnh.tileentity.multi.MTEIndustrialFarm", multiblock: true);
        Machine(db, Eec, "kubatech.tileentity.gregtech.multiblock.MTEExtremeEntityCrusher", multiblock: true);
        Fertilizer(db, CropFertilizer, 100);
        FluidFertilizer(db, "f~cropsnh~cropsnh.fertilizer", 1);
        FluidFertilizer(db, "f~cropsnh~cropsnh.enrichedfertilizer", 10);
        FarmComponent(db, "com.gtnewhorizon.cropsnh.blocks.BlockSeedBed", 2);
        FarmComponent(db, "com.gtnewhorizon.cropsnh.blocks.BlockSeedBed", 13);
        Machine(db, XlTurbine, "gregtech.common.tileentities.machines.multi.xlturbines.MTEXLTurbineGas", multiblock: true);
        Constant(db, "XL_TURBINE_SLOTS", 12);
        Constant(db, "STEAM_PER_WATER", 160);
        Constant(db, "EEC_XP_JUICE_PER_OPERATION", 120);

        // A steam multiblock's dump bonuses carry a steam discount the artifact ships as an EU discount.
        Item(db, SteamMulti, "Steam Grinding Stack", "gregtech");
        db.Execute($"INSERT INTO GREG_TECH_MULTIBLOCK_MACHINE VALUES ('gtmb~2', 8, '{SteamMulti}')");
        db.Execute("INSERT INTO GREG_TECH_MULTIBLOCK_MACHINE_BONUSES VALUES ('gtmb~2', 8, 'PARALLEL', 0, '8 Parallels', NULL)");
        db.Execute("INSERT INTO GREG_TECH_MULTIBLOCK_MACHINE_BONUSES VALUES ('gtmb~2', 125, 'SPEED', 0, '125% Speed', NULL)");
        db.Execute("INSERT INTO GREG_TECH_MULTIBLOCK_MACHINE_BONUSES VALUES ('gtmb~2', 62.5, 'STEAM_DISCOUNT', 0, '62.5% Steam Usage', NULL)");
        TreeFarmTool(db, Saw, "LOG", 1);
        TreeFarmTool(db, Chainsaw, "LOG", 4);
        TreeFarmTool(db, BranchCutter, "SAPLING", 1);
        TreeFarmTool(db, WireCutterLv, "LEAVES", 4);

        Item(db, "i~fixture~rotor", "Fixture Rotor", "fixture", maxDamage: 12800);
        db.Execute("INSERT INTO GREG_TECH_TURBINE_ROTOR VALUES ('gtrot~170~Fixture', 0.85, 'Fixture', 12800, 2, 'SMALL', 'i~fixture~rotor')");
        db.Execute("INSERT INTO GREG_TECH_TURBINE_ROTOR_FUEL_STATS VALUES ('gtrot~170~Fixture', 0.85, 'STEAM', 0.468, 386.1, 1650, 212.5, 500)");
        db.Execute("INSERT INTO GREG_TECH_TURBINE_ROTOR_FUEL_STATS VALUES ('gtrot~170~Fixture', 0.85, 'GAS', 0.494, 518.7, 1050, 425, 500)");
        db.Execute("INSERT INTO GREG_TECH_TURBINE_ROTOR_FUEL_STATS VALUES ('gtrot~170~Fixture', 0.85, 'PLASMA', 0.52, 22495.2, 43260, 17850, 21000)");
    }
}
