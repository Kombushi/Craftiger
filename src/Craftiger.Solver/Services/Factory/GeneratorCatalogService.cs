using Craftiger.Solver.Interfaces.Factory;
using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Options;
using Microsoft.Extensions.Options;

namespace Craftiger.Solver.Services.Factory;

public sealed class GeneratorCatalogService(IOptions<FactorySolverOptions> options) : IGeneratorCatalogService
{
    private const string SteamRotorClass = "STEAM";

    private readonly FactorySolverOptions _options = options.Value;

    /// <summary>Every (buildable generator block, priced fuel) pair in map, block, fuel order; turbines spin each frontier rotor at both fits; pairs far off the cheapest weight per net EU/t are pruned per band — except for a pipeline, whose hand-picked line must survive.</summary>
    public IReadOnlyList<GeneratorLine> Eligible(FactoryContext context, IReadOnlyList<EnergyBand> bands, bool prune = true)
    {
        var index = context.Index;
        var lines = new List<GeneratorLine>();
        var frontiers = new Dictionary<(string Fuel, RotorFit Fit, double Cap), IReadOnlyList<FactoryRotorStats>>();
        foreach (var fuel in context.Machines.Fuels
            .OrderBy(fuel => fuel.Map, StringComparer.Ordinal)
            .ThenBy(fuel => fuel.ItemId, StringComparer.Ordinal))
        {
            if (!index.TryGetItem(fuel.ItemId, out var fuelItem) || context.Machines.BlocksOf(fuel.Map) is not { } blocks)
            {
                continue;
            }
            foreach (var block in blocks.OrderBy(block => block.ItemId, StringComparer.Ordinal))
            {
                if (block.Steam || !block.IsBuildable(context.Garage))
                {
                    continue;
                }
                if (block.Multiblock)
                {
                    if (block.RotorFuel is { } rotorClass
                        && fuel.EuPerUnit is { } perUnit && perUnit > 0
                        && context.Costs.IsPriced(fuelItem))
                    {
                        AddTurbineLines(lines, context, frontiers, fuel, fuelItem, block, rotorClass, perUnit);
                    }
                    else if (context.Costs.IsPriced(fuelItem)
                        && context.Machines.ModesOf(block.ItemId) is { Count: > 0 } modes)
                    {
                        if (CombustionEngine.Of(block, modes) is { } engine
                            && fuel.EuPerUnit is { } engineFuelValue && engineFuelValue > 0)
                        {
                            AddEngineLines(lines, context, fuel, fuelItem, block, engine, engineFuelValue);
                        }
                        else if (NaquadahReactor.Of(modes) is { } reactor && fuel.IsTimed)
                        {
                            AddReactorLines(lines, context, fuel, fuelItem, block, reactor);
                        }
                    }
                    continue;
                }
                if (block.Tier is not { } tier || fuel.Burn(block) is not { } burn)
                {
                    continue;
                }
                var netEuT = burn.RawEuT - block.EnetLoss;
                if (netEuT <= 0 || !context.Costs.IsPriced(fuelItem))
                {
                    continue;
                }
                lines.Add(new GeneratorLine(fuel.Map, block.ItemId, tier, fuelItem, burn.UnitsPerSecond, netEuT));
            }
        }
        return prune ? Prune(lines, context, bands) : lines;
    }

    /// <summary>Base and boosted burns through the engine's single dynamo hatch, which voids beyond its capacity; each consumable must price for its variant to run.</summary>
    private static void AddEngineLines(
        List<GeneratorLine> lines,
        FactoryContext context,
        FactoryFuel fuel,
        int fuelItem,
        FactoryMachineBlock block,
        CombustionEngine engine,
        double fuelValue)
    {
        foreach (var (burn, variant) in new[] { (engine.Base(fuelValue), (string?)null), (engine.Boosted(fuelValue), "boost") })
        {
            if (burn is null || context.Machines.BestHatch(context.Garage, block, burn.RawEuT) is not { } hatch
                || hatch.NetEuT <= 0)
            {
                continue;
            }
            var extras = new List<GeneratorFlow>();
            if (!TryAddFlow(context, extras, engine.Lubricant.FluidId, burn.LubricantPerSecond)
                || (burn.BoosterPerSecond > 0 && !TryAddFlow(context, extras, engine.Booster.FluidId, burn.BoosterPerSecond)))
            {
                continue;
            }
            lines.Add(new GeneratorLine(
                fuel.Map, block.ItemId, hatch.Tier, fuelItem, burn.FuelPerSecond, hatch.NetEuT,
                Variant: variant, ExtraInputs: extras));
        }
    }

    /// <summary>Every coolant-excited combination whose full output a garage-legal hatch can cover — the reactor stops rather than voids — with the spent fuel returned.</summary>
    private static void AddReactorLines(
        List<GeneratorLine> lines,
        FactoryContext context,
        FactoryFuel fuel,
        int fuelItem,
        FactoryMachineBlock block,
        NaquadahReactor reactor)
    {
        var returned = fuel.ReturnItemId is { } returnId && context.Index.TryGetItem(returnId, out var item)
            ? (int?)item
            : null;
        foreach (var run in reactor.Runs(fuel))
        {
            if (context.Machines.CoveringHatch(context.Garage, run.RawEuT) is not { } hatch || hatch.NetEuT <= 0)
            {
                continue;
            }
            var extras = new List<GeneratorFlow>();
            if (run.Consumes.Any(consume => !TryAddFlow(context, extras, consume.FluidId, consume.PerSecond)))
            {
                continue;
            }
            var outputs = returned is { } spent && run.ReturnPerSecond > 0
                ? new[] { new GeneratorFlow(spent, run.ReturnPerSecond) }
                : null;
            lines.Add(new GeneratorLine(
                fuel.Map, block.ItemId, hatch.Tier, fuelItem, run.FuelPerSecond, hatch.NetEuT,
                Variant: run.Variant, ExtraInputs: extras, ExtraOutputs: outputs));
        }
    }

    /// <summary>Resolves one extra consumable; only a fluid unknown to the graph kills the variant — an unpriced one may still be producible from seeds, and the LP zeroes the line otherwise.</summary>
    private static bool TryAddFlow(FactoryContext context, List<GeneratorFlow> extras, string fluidId, double perSecond)
    {
        if (!context.Index.TryGetItem(fluidId, out var item))
        {
            return false;
        }
        extras.Add(new GeneratorFlow(item, perSecond));
        return true;
    }

    /// <summary>One line per frontier rotor and fit at that rotor's optimal flow — off-optimal is strictly worse — with the block's parallel factor folded in.</summary>
    private static void AddTurbineLines(
        List<GeneratorLine> lines,
        FactoryContext context,
        Dictionary<(string Fuel, RotorFit Fit, double Cap), IReadOnlyList<FactoryRotorStats>> frontiers,
        FactoryFuel fuel,
        int fuelItem,
        FactoryMachineBlock block,
        string rotorClass,
        double perUnit)
    {
        var parallels = block.BaseParallels;
        var capPerRotor = context.Machines.HatchCapacity(context.Garage, block) / parallels;
        if (capPerRotor <= 0)
        {
            return;
        }
        foreach (var fit in new[] { RotorFit.Tight, RotorFit.Loose })
        {
            if (!frontiers.TryGetValue((rotorClass, fit, capPerRotor), out var frontier))
            {
                frontier = RotorFrontier(context, rotorClass, fit, capPerRotor);
                frontiers[(rotorClass, fit, capPerRotor)] = frontier;
            }
            foreach (var stats in frontier)
            {
                var point = stats.At(fit);
                if (context.Machines.BestHatch(context.Garage, block, point.Eut * parallels) is not { } hatch
                    || hatch.NetEuT <= 0)
                {
                    continue;
                }
                var condensate = rotorClass == SteamRotorClass && context.DistilledWaterItem() is { } water
                    ? (Item: (int?)water, PerUnit: context.Steam.CondensatePerLiter)
                    : (Item: null, PerUnit: 0.0);
                lines.Add(new GeneratorLine(
                    fuel.Map, block.ItemId, hatch.Tier, fuelItem,
                    point.Flow / perUnit * Ticks.PerSecond * parallels, hatch.NetEuT,
                    stats.ItemId, fit, condensate.Item, condensate.PerUnit));
            }
        }
    }

    /// <summary>Priced rotors not dominated on (efficiency, output) under the hatch cap: raw stats let monster rotors dominate everything while netting worst-in-pool fuel economy.</summary>
    private static IReadOnlyList<FactoryRotorStats> RotorFrontier(FactoryContext context, string rotorClass, RotorFit fit, double cap)
    {
        var craftable = context.Machines.Rotors
            .Where(rotor => rotor.Fuel == rotorClass
                && context.Index.TryGetItem(rotor.ItemId, out var item) && context.Costs.IsPriced(item))
            .OrderBy(rotor => rotor.ItemId, StringComparer.Ordinal)
            .ToList();
        return craftable
            .Where(rotor => !craftable.Any(other => other != rotor && rotor.IsDominatedBy(other, fit, cap)))
            .ToList();
    }

    /// <summary>Keeps lines within the band of the cheapest weight per net EU/t — overall, and per quality band among the lines satisfying it, so a tier demand never starves.</summary>
    private List<GeneratorLine> Prune(List<GeneratorLine> lines, FactoryContext context, IReadOnlyList<EnergyBand> bands)
    {
        if (lines.Count == 0)
        {
            return lines;
        }
        var cheapest = lines.Min(line => line.WeightPerEu(context.Costs));
        var cheapestPerBand = bands
            .Select(band => (Band: band, Best: lines
                .Where(line => line.Satisfies(band))
                .Select(line => line.WeightPerEu(context.Costs))
                .DefaultIfEmpty(double.PositiveInfinity)
                .Min()))
            .ToList();
        return lines
            .Where(line =>
            {
                var weight = line.WeightPerEu(context.Costs);
                return weight <= _options.PruneFactor * cheapest + _options.GeneratorPruneFloor
                    || cheapestPerBand.Any(band =>
                        line.Satisfies(band.Band) && weight <= _options.PruneFactor * band.Best + _options.GeneratorPruneFloor);
            })
            .ToList();
    }
}
