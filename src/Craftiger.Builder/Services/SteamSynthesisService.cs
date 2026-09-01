using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

/// <summary>The extracted burn seconds already carry GT's long-burn bonus, applied when the fake fuel recipes were generated.</summary>
public sealed class SteamSynthesisService(IOptions<SteamConfiguration> options, ILogger<SteamSynthesisService> logger) : ISteamSynthesisService
{
    private const double TicksPerSecond = 20.0;

    private readonly SteamConfiguration _config = options.Value;

    public SteamSynthesis Run(Dump dump, UnifiedItems unified, IReadOnlyList<PlannerBoilerFuel> boilerFuels)
    {
        var recipes = new List<PlannerRecipe>();
        var machines = new List<PlannerMachineItem>();
        var fuels = new List<PlannerFuel>();

        var waterPerSteam = dump.Constant("STEAM_PER_WATER");
        var fluidsKnown = dump.IsFluid(_config.WaterFluidId) && dump.IsFluid(_config.SteamOutputFluidId);
        if (!fluidsKnown)
        {
            logger.LogWarning("steam config's water or steam fluid is unknown to this dump; no boiler recipes");
        }

        // Every non-deprecated boiler class names its generation, which the fuel tabs' prose
        // abbreviates ("Tungstenst."), so a fuel matches where its name prefixes the generation.
        var classes = new Dictionary<string, string>();
        foreach (var machine in dump.Machines)
        {
            classes.TryAdd(machine.ItemId, machine.MachineClass);
        }
        var boilers = fluidsKnown
            ? dump.Boilers
                .Select(boiler => (Boiler: boiler,
                    Generation: MachineClasses.BoilerGenerationOf(classes.GetValueOrDefault(boiler.ItemId) ?? "")))
                .Where(pair => pair.Generation is not null && !dump.DeprecatedItems.Contains(pair.Boiler.ItemId))
                .OrderBy(pair => pair.Generation, StringComparer.Ordinal)
                .ToList()
            : [];
        foreach (var (boiler, generation) in boilers)
        {
            var controllerId = unified.Canonical(boiler.ItemId);
            var map = dump.NameOf(controllerId);
            machines.Add(new PlannerMachineItem(map, controllerId, null, Multiblock: true, Steam: false, Era: null));

            foreach (var fuel in boilerFuels
                .Where(f => generation!.StartsWith(f.Boiler.TrimEnd('.'), StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.ItemId, StringComparer.Ordinal))
            {
                var ticks = (long)Math.Round(fuel.BurnSeconds * TicksPerSecond);
                if (ticks <= 0)
                {
                    continue;
                }
                var steam = 2L * boiler.EuT * ticks;
                var water = (long)Math.Ceiling(steam / (double)waterPerSteam);
                recipes.Add(new PlannerRecipe(
                    $"gtboil~{controllerId}~{fuel.ItemId}", map, Tier: 0, Heat: null,
                    DurationTicks: ticks, EuT: 0, Amps: 1,
                    Inputs: new Dictionary<string, long> { [fuel.ItemId] = 1, [_config.WaterFluidId] = water },
                    Choices: [],
                    Outputs: [new PlannerOutput(_config.SteamOutputFluidId, steam, 1.0)],
                    Machines: [new RecipeMachine(controllerId, Multiblock: true, Tier: null, Steam: false)],
                    InputSlotAlternatives: [],
                    RequiresCleanroom: false,
                    RequiresLowGravity: false));
            }
        }

        var singles = 0;
        var larges = 0;
        foreach (var machine in dump.Machines.OrderBy(m => m.ItemId, StringComparer.Ordinal))
        {
            if (dump.DeprecatedItems.Contains(machine.ItemId))
            {
                continue;
            }
            var itemId = unified.Canonical(machine.ItemId);
            if (machine.MachineClass.EndsWith(MachineClasses.SteamTurbine, StringComparison.Ordinal))
            {
                var generator = dump.Generators.FirstOrDefault(g => unified.Canonical(g.ItemId) == itemId)
                    ?? throw new InvalidOperationException($"steam turbine {itemId} has no generator row");
                machines.Add(new PlannerMachineItem(
                    _config.TurbineMap, itemId, TierLadder.VoltageTier(generator.MaxEuOutput),
                    Multiblock: false, Steam: false, Era: null));
                singles++;
            }
            else if (MachineClasses.RotorFuelOf(machine.MachineClass) == "STEAM")
            {
                machines.Add(new PlannerMachineItem(_config.LargeTurbineMap, itemId, null, Multiblock: true, Steam: false, Era: null));
                larges++;
            }
        }

        var steamFluids = _config.SteamFluidIds.Where(dump.IsFluid).ToList();
        foreach (var (map, present) in new[] { (_config.TurbineMap, singles > 0), (_config.LargeTurbineMap, larges > 0) })
        {
            if (!present)
            {
                continue;
            }

            fuels.AddRange(steamFluids.Select(steamId => new PlannerFuel(map, steamId, 1, _config.EuPerLiter, null, null)));
        }

        var carrier = new SteamCarrier(
            steamFluids,
            dump.IsFluid(_config.DistilledWaterId) ? _config.DistilledWaterId : null,
            _config.EuPerLiter,
            waterPerSteam);

        logger.LogInformation(
            "  {Recipes:N0} boiler recipes, {Machines:N0} steam machine rows, {Fuels:N0} steam fuels",
            recipes.Count, machines.Count, fuels.Count);

        return new SteamSynthesis(recipes, machines, fuels, carrier);
    }
}
