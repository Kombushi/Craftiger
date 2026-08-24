using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Craftiger.Builder.Models.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

/// <summary>Merges the dump's per-machine stat tables into planner rows keyed by the
/// canonical machine item. Only rows that carry signal ship: a bonus-less multiblock at one
/// parallel is the model's default and needs no row.</summary>
public sealed class MachinePropsService(
    IOptions<MachineOverlayConfiguration> overlay,
    ILogger<MachinePropsService> logger) : IMachinePropsService
{
    private readonly MachineOverlayConfiguration _overlay = overlay.Value;

    public MachinePropsData Run(
        Dump dump, UnifiedItems unified, IReadOnlyDictionary<string, int> era)
    {
        var machineItems = new Dictionary<(string, string), PlannerMachineItem>();
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
            // An id absent from the dump altogether is another pack's item (fixture runs);
            // an item that exists but serves no map is a config error worth failing on.
            if (!dump.Items.ContainsKey(itemId))
            {
                logger.LogWarning("machine overlay names {ItemId}, unknown to this dump; skipped", itemId);
                continue;
            }
            if (!machineItems.Values.Any(m => m.ItemId == itemId))
            {
                throw new InvalidOperationException(
                    $"machine overlay names {itemId}, which the dump lists as no machine block");
            }
            props[itemId] = Row(itemId) with { MaxParallel = parallels };
        }
        foreach (var itemId in _overlay.RotorTurbines.Order(StringComparer.Ordinal))
        {
            if (!dump.Items.ContainsKey(itemId))
            {
                logger.LogWarning("machine overlay names {ItemId}, unknown to this dump; skipped", itemId);
                continue;
            }
            if (!machineItems.Values.Any(m => m.ItemId == itemId))
            {
                throw new InvalidOperationException(
                    $"machine overlay names {itemId}, which the dump lists as no machine block");
            }
            props[itemId] = Row(itemId) with { RotorTurbine = true };
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
            "  {Props:N0} machine props, {Bonuses:N0} bonuses, {Rotors:N0} rotors, "
            + "{Deprecated:N0} deprecated blocks dropped",
            props.Count, bonuses.Count, rotors.Count, deprecated);
        return new MachinePropsData(
            [.. machineItems.Values], [.. props.Values], [.. bonuses], rotors, rotorStats);
    }
}
