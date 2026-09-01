using Craftiger.Builder.Interfaces.DumpReaders;
using Craftiger.Builder.Models.Dump;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Craftiger.Builder.Repositories.DumpReaders;

public sealed class DumpMachineReader : IDumpMachineReader
{
    public DumpMachineSet Read(SqliteConnection db) =>
        new(
            ReadRecipeMaps(db),
            ReadGenerators(db),
            ReadDynamos(db),
            ReadBoilers(db),
            ReadMultiblockMachines(db),
            ReadTurbineRotors(db));

    /// <summary>A GregTech recipe type is named rt~gregtech~(recipe map)~(voltage).</summary>
    private static IReadOnlyDictionary<string, DumpRecipeMap> ReadRecipeMaps(SqliteConnection db)
    {
        DumpQueries.RequireColumn(db, "GREG_TECH_RECIPE_MAP_MACHINES", "MACHINES_OUTPUT_SLOTS");
        var machinesByMapId = new Dictionary<string, List<DumpRecipeMapMachine>>();
        foreach (var r in db.Query<(string MapId, string ItemId, long Multiblock, int? Tier, long Steam, int? OutputSlots)>("""
            SELECT GREG_TECH_RECIPE_MAP_ID, MACHINES_ITEM_ID, MACHINES_MULTIBLOCK, MACHINES_TIER, MACHINES_STEAM, MACHINES_OUTPUT_SLOTS
            FROM GREG_TECH_RECIPE_MAP_MACHINES
            """))
        {
            DumpQueries.Add(machinesByMapId, r.MapId,
                new DumpRecipeMapMachine(r.ItemId, r.Multiblock != 0, r.Tier, r.Steam != 0, r.OutputSlots));
        }

        var recipeMaps = new Dictionary<string, DumpRecipeMap>();
        foreach (var r in db.Query<(string Id, string Unlocalized, string Name, int Amperage, long Single, long Multi, long Fuel)>("""
            SELECT ID, UNLOCALIZED_NAME, LOCALIZED_NAME, AMPERAGE, HAS_SINGLE_BLOCK, HAS_MULTI_BLOCK, IS_FUEL
            FROM GREG_TECH_RECIPE_MAP
            """))
        {
            recipeMaps[r.Unlocalized] = new DumpRecipeMap(
                r.Unlocalized, r.Name, r.Amperage, r.Single != 0, r.Multi != 0, r.Fuel != 0,
                machinesByMapId.GetValueOrDefault(r.Id) ?? []);
        }

        var recipeMapByTypeId = new Dictionary<string, DumpRecipeMap>();
        foreach (var typeId in db.Query<string>("""SELECT ID FROM RECIPE_TYPE WHERE CATEGORY = 'gregtech'"""))
        {
            var parts = typeId.Split('~');
            if (parts.Length == 4 && recipeMaps.TryGetValue(parts[2], out var map))
            {
                recipeMapByTypeId[typeId] = map;
            }
        }
        return recipeMapByTypeId;
    }

    private static List<DumpGenerator> ReadGenerators(SqliteConnection db)
    {
        DumpQueries.RequireMachineProps(db, "GREG_TECH_GENERATOR");
        // The efficiency column mixes INTEGER and REAL rows; CAST keeps Dapper's row shape stable.
        return [.. db.Query<(string ItemId, double Efficiency, long MaxEuOutput, long AmpsOut)>("""
            SELECT ITEM_ID, CAST(EFFICIENCY AS REAL), MAX_EU_OUTPUT, AMPERES_OUT
            FROM GREG_TECH_GENERATOR
            """).Select(r => new DumpGenerator(r.ItemId, r.Efficiency, r.MaxEuOutput, r.AmpsOut))];
    }

    private static List<DumpDynamo> ReadDynamos(SqliteConnection db)
    {
        DumpQueries.RequireMachineProps(db, "GREG_TECH_DYNAMO");
        return [.. db.Query<(string ItemId, long MaxEuOutput, long AmpsOut)>("""
            SELECT ITEM_ID, MAX_EU_OUTPUT, AMPERES_OUT FROM GREG_TECH_DYNAMO
            """).Select(r => new DumpDynamo(r.ItemId, r.MaxEuOutput, r.AmpsOut))];
    }

    private static List<DumpBoiler> ReadBoilers(SqliteConnection db)
    {
        DumpQueries.RequireMachineProps(db, "GREG_TECH_LARGE_BOILER");
        return [.. db.Query<(string ItemId, long EuT)>("""
            SELECT ITEM_ID, EUT FROM GREG_TECH_LARGE_BOILER
            """).Select(r => new DumpBoiler(r.ItemId, (int)r.EuT))];
    }

    private static List<DumpMultiblockMachine> ReadMultiblockMachines(SqliteConnection db)
    {
        DumpQueries.RequireMachineProps(db, "GREG_TECH_MULTIBLOCK_MACHINE");
        var bonuses = new Dictionary<string, List<DumpMultiblockBonus>>();
        foreach (var r in db.Query<(string Id, string Kind, double Value, long Multiplicative, string? TierAxis)>("""
            SELECT GREG_TECH_MULTIBLOCK_MACHINE_ID, BONUSES_KIND, BONUSES_BONUS_VALUE,
                BONUSES_MULTIPLICATIVE, BONUSES_TIER_AXIS
            FROM GREG_TECH_MULTIBLOCK_MACHINE_BONUSES
            """))
        {
            DumpQueries.Add(bonuses, r.Id, new DumpMultiblockBonus(
                r.Kind, r.Value, r.Multiplicative != 0, r.TierAxis));
        }

        return [.. db.Query<(string Id, string ItemId, long? MaxParallel)>("""
            SELECT ID, ITEM_ID, MAX_PARALLEL_RECIPES FROM GREG_TECH_MULTIBLOCK_MACHINE
            """).Select(r => new DumpMultiblockMachine(
            r.ItemId, (int?)r.MaxParallel, bonuses.GetValueOrDefault(r.Id) ?? []))];
    }

    private static List<DumpTurbineRotor> ReadTurbineRotors(SqliteConnection db)
    {
        DumpQueries.RequireMachineProps(db, "GREG_TECH_TURBINE_ROTOR");
        var stats = new Dictionary<string, List<DumpRotorFuelStats>>();
        foreach (var r in db.Query<(string Id, string Fuel, double Efficiency, double LooseEfficiency, double OptimalFlow, double LooseOptimalFlow, double OptimalEut, double LooseOptimalEut)>("""
            SELECT GREG_TECH_TURBINE_ROTOR_ID, FUEL_STATS_FUEL, FUEL_STATS_EFFICIENCY,
                FUEL_STATS_LOOSE_EFFICIENCY, FUEL_STATS_OPTIMAL_FLOW,
                FUEL_STATS_LOOSE_OPTIMAL_FLOW, FUEL_STATS_OPTIMAL_EUT,
                FUEL_STATS_LOOSE_OPTIMAL_EUT
            FROM GREG_TECH_TURBINE_ROTOR_FUEL_STATS
            """))
        {
            DumpQueries.Add(stats, r.Id, new DumpRotorFuelStats(
                r.Fuel, r.Efficiency, r.LooseEfficiency, r.OptimalFlow, r.LooseOptimalFlow,
                r.OptimalEut, r.LooseOptimalEut));
        }

        return [.. db.Query<(string Id, string ItemId, string Size, string Material, long Durability, double BaseEfficiency, long Overflow)>("""
            SELECT ID, ITEM_ID, SIZE, MATERIAL_NAME, MAX_DURABILITY, BASE_EFFICIENCY,
                OVERFLOW_EFFICIENCY
            FROM GREG_TECH_TURBINE_ROTOR
            """).Select(r => new DumpTurbineRotor(
            r.ItemId, r.Size, r.Material, r.Durability, r.BaseEfficiency, (int)r.Overflow,
            stats.GetValueOrDefault(r.Id) ?? []))];
    }
}
