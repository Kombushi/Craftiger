using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

/// <summary>Only rows that carry signal ship: a bonus-less multiblock at one parallel is the model's default and needs no row.</summary>
public sealed class MachinePropsService(
    IOptions<MachineOverlayConfiguration> overlay,
    ILogger<MachinePropsService> logger) : IMachinePropsService
{
    private readonly MachineOverlayConfiguration _overlay = overlay.Value;

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
                bonuses.Add(new PlannerMachineBonus(
                    itemId, bonus.Kind, bonus.Value, bonus.Multiplicative, bonus.TierAxis));
            }
            if (multiblock.Bonuses.Count > 0 || multiblock.MaxParallel is > 1)
            {
                props[itemId] = Row(multiblock.ItemId) with
                {
                    MaxParallel = multiblock.MaxParallel,
                };
            }
        }

        foreach (var (itemId, parallels) in _overlay.Parallels.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (Overlaid(itemId))
            {
                props[itemId] = Row(itemId) with { MaxParallel = parallels };
            }
        }
        foreach (var (itemId, fuel) in _overlay.RotorTurbines.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (Overlaid(itemId))
            {
                props[itemId] = Row(itemId) with { RotorFuel = fuel };
            }
        }
        foreach (var itemId in _overlay.SteamMultiblocks.Order(StringComparer.Ordinal))
        {
            if (!Overlaid(itemId))
            {
                continue;
            }
            // Every GT++ steam multiblock shares the same tooltip triple, verified per item.
            props[itemId] = Row(itemId) with { MaxParallel = 8 };
            bonuses.Add(new PlannerMachineBonus(itemId, "PARALLEL", 8, false, null));
            bonuses.Add(new PlannerMachineBonus(itemId, "SPEED", 125, false, null));
            bonuses.Add(new PlannerMachineBonus(itemId, "EU_DISCOUNT", 62.5, false, null));
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
        foreach (var engine in _overlay.Engines.OrderBy(e => e.ItemId, StringComparer.Ordinal))
        {
            if (!Overlaid(engine.ItemId))
            {
                continue;
            }
            props[engine.ItemId] = Row(engine.ItemId) with { GeneratorEuT = engine.NominalEuT };
            AddMode(engine.ItemId, "BOOSTER", engine.BoosterFluidId, engine.BoosterPerSecond, engine.BoostFactor);
            AddMode(engine.ItemId, "LUBRICANT", engine.LubricantFluidId, engine.LubricantPerSecond, 1);
        }
        foreach (var reactor in _overlay.Reactors.OrderBy(r => r.ItemId, StringComparer.Ordinal))
        {
            if (!Overlaid(reactor.ItemId))
            {
                continue;
            }
            AddMode(reactor.ItemId, "UPKEEP", reactor.UpkeepFluidId, reactor.UpkeepPerSecond, 1);
            foreach (var coolant in reactor.Coolants)
            {
                AddMode(reactor.ItemId, "COOLANT", coolant.FluidId, coolant.PerSecond, coolant.Factor);
            }
            foreach (var excited in reactor.ExcitedLiquids)
            {
                AddMode(reactor.ItemId, "EXCITED", excited.FluidId, excited.PerSecond, excited.Factor);
            }
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

        bool Overlaid(string itemId)
        {
            if (!dump.Items.ContainsKey(itemId))
            {
                logger.LogWarning("machine overlay names {ItemId}, unknown to this dump; skipped", itemId);
                return false;
            }
            if (machineItems.Values.All(m => m.ItemId != itemId))
            {
                throw new InvalidOperationException($"machine overlay names {itemId}, which the dump lists as no machine block");
            }
            return true;
        }
    }
}
