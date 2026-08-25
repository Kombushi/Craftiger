using System.Text.Json;
using Craftiger.Api.Interfaces;
using Craftiger.Api.Models;
using Craftiger.Solver.Models.Factory;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Craftiger.Api.Repositories;

public sealed class FactoryArtifactReader : IFactoryArtifactReader
{
    public FactoryArtifactData Read(SqliteConnection db, IReadOnlyDictionary<string, string> meta, FactoryRecipeData recipes)
    {
        var props = db.Query<MachinePropsRow>(
                """
                SELECT item_id AS ItemId, era, generator_efficiency AS GeneratorEfficiency,
                    generator_eu_t AS GeneratorEuT, generator_amps AS GeneratorAmps,
                    dynamo_eu_t AS DynamoEuT, dynamo_amps AS DynamoAmps, max_parallel AS MaxParallel,
                    rotor_fuel AS RotorFuel
                FROM machine_props
                """)
            .ToDictionary(row => row.ItemId);
        var bonuses = db.Query<MachineBonusRow>(
                "SELECT item_id AS ItemId, kind, bonus, multiplicative, tier_axis AS TierAxis FROM machine_bonuses")
            .GroupBy(row => row.ItemId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<FactoryMachineBonus>)group
                    .Select(row => new FactoryMachineBonus(row.Kind, row.Bonus, row.Multiplicative != 0, row.TierAxis))
                    .ToList());
        var blocksByMap = db.Query<MachineItemRow>(
                "SELECT map, item_id AS ItemId, tier, multiblock, steam, era FROM machine_items")
            .GroupBy(row => row.Map)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<FactoryMachineBlock>)group.Select(row =>
                {
                    var prop = props.GetValueOrDefault(row.ItemId);
                    return new FactoryMachineBlock(
                        row.ItemId, (int?)row.Tier, row.Multiblock != 0, row.Steam != 0, (int?)row.Era,
                        prop?.MaxParallel ?? 1,
                        bonuses.GetValueOrDefault(row.ItemId, []),
                        prop?.GeneratorEfficiency, prop?.GeneratorEuT, prop?.GeneratorAmps, prop?.RotorFuel);
                }).ToList());
        var coils = JsonSerializer.Deserialize<List<CoilMeta>>(meta.GetValueOrDefault("coils") ?? "[]") ?? [];
        var fuels = db.Query<FuelRow>(
                "SELECT map, item_id AS ItemId, amount, eu_per_unit AS EuPerUnit, eu_t AS EuT, duration_ticks AS DurationTicks FROM fuels")
            .Select(row => new FactoryFuel(row.Map, row.ItemId, row.Amount, row.EuPerUnit, row.EuT, row.DurationTicks))
            .ToList();
        var rotors = db.Query<RotorStatsRow>(
                """
                SELECT item_id AS ItemId, fuel, efficiency, loose_efficiency AS LooseEfficiency,
                    optimal_flow AS OptimalFlow, loose_optimal_flow AS LooseOptimalFlow,
                    optimal_eut AS OptimalEut, loose_optimal_eut AS LooseOptimalEut
                FROM rotor_fuel_stats
                """)
            .Select(row => new FactoryRotorStats(
                row.ItemId, row.Fuel, row.Efficiency, row.LooseEfficiency,
                row.OptimalFlow, row.LooseOptimalFlow, row.OptimalEut, row.LooseOptimalEut))
            .ToList();
        var dynamos = props.Values
            .Where(row => row.DynamoEuT is not null)
            .Select(row => new FactoryDynamo(row.ItemId, (int?)row.Era, row.DynamoEuT!.Value, row.DynamoAmps ?? 1))
            .ToList();
        var machines = new FactoryMachineData(
            blocksByMap, coils.Select(coil => new FactoryCoil(coil.Tier, coil.MaxHeat)).ToList(), fuels, rotors, dynamos);

        var seeds = new FactorySeedData(
            db.Query<SeedRow>("SELECT item_id AS ItemId, kind FROM renewable_seeds")
                .ToDictionary(row => row.ItemId, row => Enum.Parse<SeedKind>(row.Kind, ignoreCase: true)));

        var steamMeta = JsonSerializer.Deserialize<SteamMeta>(
            meta.GetValueOrDefault("steam")
            ?? throw new InvalidOperationException("planner.sqlite carries no steam meta; rebuild it with the current builder"))
            ?? throw new InvalidOperationException("planner.sqlite carries an unreadable steam meta");
        var steam = new FactorySteamRules(
            steamMeta.SteamFluidIds, steamMeta.DistilledWaterId, steamMeta.EuPerLiter, steamMeta.WaterPerSteam);

        return new FactoryArtifactData(recipes, machines, seeds, steam);
    }
}
