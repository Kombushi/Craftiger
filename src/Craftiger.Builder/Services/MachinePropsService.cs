using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;

namespace Craftiger.Builder.Services;

/// <summary>Only rows that carry signal ship: a bonus-less multiblock at one parallel is the model's default and needs no row.</summary>
public sealed class MachinePropsService(
    ILogger<MachinePropsService> logger) : IMachinePropsService
{
    // Engine burn mechanics that hold for every pack: the booster gas burns at 2 L/t and
    // lubricant at 1 L per 72 ticks, each times the engine's additive factor.
    private const double TicksPerSecond = 20.0;
    private const double BoosterLitersPerTick = 2;
    private const double LubricantTicksPerLiter = 72;

    public MachinePropsData Run(
        Dump dump, UnifiedItems unified, IReadOnlyDictionary<string, int> era,
        IReadOnlyList<PlannerMachineItem> synthesized)
    {
        var machineItems = new Dictionary<(string, string), PlannerMachineItem>();
        foreach (var extra in synthesized)
        {
            machineItems.TryAdd(
                (extra.Map, extra.ItemId),
                extra with { Era = era.TryGetValue(extra.ItemId, out var extraEra) ? extraEra : null });
        }
        var deprecated = 0;
        foreach (var map in dump.RecipeMapByTypeId.Values.Distinct())
        {
            foreach (var machine in map.Machines)
            {
                var itemId = unified.Canonical(machine.ItemId);
                if (dump.DeprecatedItems.Contains(machine.ItemId) || dump.DeprecatedItems.Contains(itemId))
                {
                    deprecated++;
                    continue;
                }
                machineItems.TryAdd(
                    (map.Name, itemId),
                    new PlannerMachineItem(
                        map.Name, itemId, machine.Tier, machine.Multiblock, machine.Steam,
                        era.TryGetValue(itemId, out var itemEra) ? itemEra : null));
            }
        }

        var props = new Dictionary<string, PlannerMachineProps>();

        foreach (var generator in dump.Generators)
        {
            props[unified.Canonical(generator.ItemId)] = Row(generator.ItemId) with
            {
                GeneratorEfficiency = generator.Efficiency,
                GeneratorEuT = generator.MaxEuOutput,
                GeneratorAmps = generator.AmpsOut,
            };
        }
        foreach (var dynamo in dump.Dynamos)
        {
            props[unified.Canonical(dynamo.ItemId)] = Row(dynamo.ItemId) with
            {
                DynamoEuT = dynamo.MaxEuOutput,
                DynamoAmps = dynamo.AmpsOut,
            };
        }
        foreach (var boiler in dump.Boilers)
        {
            props[unified.Canonical(boiler.ItemId)] = Row(boiler.ItemId) with
            {
                BoilerEuT = boiler.EuT,
            };
        }

        // A controller listed twice in the dump must not double its bonus rows.
        var bonuses = new HashSet<PlannerMachineBonus>();
        foreach (var multiblock in dump.MultiblockMachines)
        {
            var itemId = unified.Canonical(multiblock.ItemId);
            foreach (var bonus in multiblock.Bonuses)
            {
                // A steam multiblock's power is its steam draw, so its steam discount is the model's EU discount.
                var kind = bonus.Kind == "STEAM_DISCOUNT" ? "EU_DISCOUNT" : bonus.Kind;
                bonuses.Add(new PlannerMachineBonus(
                    itemId, kind, bonus.Value, bonus.Multiplicative, bonus.TierAxis));
            }
            if (multiblock.Bonuses.Count > 0 || multiblock.MaxParallel is > 1)
            {
                props[itemId] = Row(multiblock.ItemId) with
                {
                    MaxParallel = multiblock.MaxParallel,
                };
            }
        }

        // Turbine kinds and the XL slot count come off the machines' classes, after the
        // multiblock rows so the constant overrides the prototype's structureless parallels.
        foreach (var machine in dump.Machines.OrderBy(m => m.ItemId, StringComparer.Ordinal))
        {
            if (MachineClasses.RotorFuelOf(machine.MachineClass) is { } fuel)
            {
                props[unified.Canonical(machine.ItemId)] = Row(machine.ItemId) with { RotorFuel = fuel };
            }
            if (machine.MachineClass.Contains(MachineClasses.XlTurbines, StringComparison.Ordinal))
            {
                props[unified.Canonical(machine.ItemId)] = Row(machine.ItemId) with
                {
                    MaxParallel = (int)dump.Constant("XL_TURBINE_SLOTS"),
                };
            }
        }

        var modes = new List<PlannerGeneratorMode>();
        void AddMode(string itemId, string kind, string fluidId, double perSecond, double factor)
        {
            if (!dump.IsFluid(fluidId))
            {
                logger.LogWarning("machine overlay names fluid {FluidId}, unknown to this dump; mode skipped", fluidId);
                return;
            }
            modes.Add(new PlannerGeneratorMode(itemId, kind, fluidId, perSecond, factor));
        }
        foreach (var engine in dump.Engines.OrderBy(e => e.ItemId, StringComparer.Ordinal))
        {
            var itemId = unified.Canonical(engine.ItemId);
            props[itemId] = Row(engine.ItemId) with { GeneratorEuT = engine.NominalOutput };
            AddMode(
                itemId, "BOOSTER", engine.BoosterFluidId,
                TicksPerSecond * BoosterLitersPerTick * engine.AdditiveFactor,
                (double)engine.EfficiencyBoosted / engine.EfficiencyUnboosted);
            AddMode(
                itemId, "LUBRICANT", engine.LubricantFluidId,
                TicksPerSecond * engine.AdditiveFactor / LubricantTicksPerLiter, 1);
        }
        foreach (var mode in dump.ReactorModes
            .OrderBy(m => m.MachineItemId, StringComparer.Ordinal)
            .ThenBy(m => m.Kind, StringComparer.Ordinal)
            .ThenBy(m => m.FluidId, StringComparer.Ordinal))
        {
            var factor = mode.Kind == "COOLANT" ? mode.Factor!.Value / 100.0 : mode.Factor ?? 1;
            AddMode(unified.Canonical(mode.MachineItemId), mode.Kind, mode.FluidId, mode.AmountPerSecond, factor);
        }

        var rotors = new List<PlannerTurbineRotor>();
        var rotorStats = new List<PlannerRotorFuelStats>();
        foreach (var rotor in dump.TurbineRotors)
        {
            rotors.Add(new PlannerTurbineRotor(
                rotor.ItemId, rotor.Size, rotor.Material, rotor.Durability,
                rotor.BaseEfficiency, rotor.OverflowTier));
            foreach (var stats in rotor.FuelStats)
            {
                rotorStats.Add(new PlannerRotorFuelStats(
                    rotor.ItemId, stats.Fuel, stats.Efficiency, stats.LooseEfficiency,
                    stats.OptimalFlow, stats.LooseOptimalFlow, stats.OptimalEut,
                    stats.LooseOptimalEut));
            }
        }

        logger.LogInformation(
            "  {Props:N0} machine props, {Bonuses:N0} bonuses, {Rotors:N0} rotors, {Modes:N0} generator modes, "
            + "{Deprecated:N0} deprecated blocks dropped",
            props.Count, bonuses.Count, rotors.Count, modes.Count, deprecated);

        return new MachinePropsData([.. machineItems.Values], [.. props.Values], [.. bonuses], rotors, rotorStats, modes);

        PlannerMachineProps Row(string rawItemId)
        {
            var itemId = unified.Canonical(rawItemId);
            if (!props.TryGetValue(itemId, out var row))
            {
                row = props[itemId] = new PlannerMachineProps(
                    itemId, era.TryGetValue(itemId, out var itemEra) ? itemEra : null,
                    null, null, null, null, null, null, null);
            }
            return row;
        }
    }
}
