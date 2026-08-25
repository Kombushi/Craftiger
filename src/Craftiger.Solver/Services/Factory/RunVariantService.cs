using Craftiger.Solver.Interfaces.Costs;
using Craftiger.Solver.Interfaces.Factory;
using Craftiger.Solver.Models.Factory;

namespace Craftiger.Solver.Services.Factory;

public sealed class RunVariantService(IGarageLegalityService legality) : IRunVariantService
{
    /// <summary>Excess coil heat turns one overclock perfect per this much.</summary>
    private const int HeatPerPerfectStep = 1800;

    /// <summary>Excess coil heat discounts energy by 5 % per this much.</summary>
    private const int HeatPerDiscountStep = 900;

    private const double HeatDiscount = 0.95;

    /// <summary>Steam blocks serve LV-and-below recipes only.</summary>
    private const int MaxSteamTier = 1;

    /// <summary>Buildable blocks of the map at each overclock up to their tier, else one anonymous flagged block at the map tier; steam blocks run on steam instead of EU; durationless recipes get one free variant.</summary>
    public IReadOnlyList<RunVariant> Variants(FactoryContext context, int recipe)
    {
        var index = context.Index;
        var garage = context.Garage;
        var map = index.Machine[recipe];
        var mapTier = legality.EffectiveTier(map, garage) ?? 0;
        var durationTicks = context.Recipes.DurationTicks[recipe];
        if (durationTicks == 0)
        {
            return [RunVariant.Durationless(recipe)];
        }

        var multiBuilt = index.MultiTierOf(recipe) is not null && garage.HasBuilt(map);
        var singleRequired = index.Tier[recipe];
        var multiRequired = index.MultiTierOf(recipe) ?? singleRequired;

        var perfectSteps = 0;
        var heatEuFactor = 1.0;
        if (index.HeatOf(recipe) is { } heat)
        {
            var excess = legality.HeatCapacity(map, garage) - heat;
            if (excess > 0)
            {
                perfectSteps = Math.DivRem(excess, HeatPerPerfectStep).Quotient;
                var discountSteps = Math.DivRem(excess, HeatPerDiscountStep).Quotient;
                heatEuFactor = Math.Pow(HeatDiscount, discountSteps);
            }
        }

        var variants = new List<RunVariant>();
        var coilTier = context.Machines.CoilTier(garage, map);
        var blocks = context.Machines.BlocksOf(map);
        var euPerTick = context.Recipes.DrawPerTick(recipe);
        var mode = context.Recipes.OverclockOf(recipe);
        if (blocks is { Count: > 0 })
        {
            var allMulti = blocks.All(block => block.Multiblock);
            foreach (var block in blocks.OrderBy(block => block.ItemId, StringComparer.Ordinal))
            {
                if (block.Steam || !block.IsBuildable(garage))
                {
                    continue;
                }
                int voltageTier;
                int required;
                if (block.Multiblock)
                {
                    if (!allMulti && !multiBuilt)
                    {
                        continue;
                    }
                    voltageTier = mapTier;
                    required = multiRequired;
                }
                else
                {
                    if (block.Tier is not { } tier || tier < singleRequired || tier > mapTier)
                    {
                        continue;
                    }
                    voltageTier = tier;
                    required = singleRequired;
                }
                AddOverclocks(
                    variants, mode, recipe, block.ItemId, durationTicks, euPerTick,
                    required, voltageTier - required, perfectSteps, heatEuFactor, block.Effects(coilTier, voltageTier));
            }
        }

        if (variants.Count == 0)
        {
            var required = multiBuilt ? multiRequired : singleRequired;
            AddOverclocks(
                variants, mode, recipe, null, durationTicks, euPerTick,
                required, mapTier - required, perfectSteps, heatEuFactor, BlockEffects.Anonymous);
        }

        if (blocks is not null && index.Tier[recipe] <= MaxSteamTier && index.HeatOf(recipe) is null
            && context.Recipes.EuT[recipe] > 0)
        {
            foreach (var block in blocks.OrderBy(block => block.ItemId, StringComparer.Ordinal))
            {
                if (!block.Steam || !block.IsBuildable(garage))
                {
                    continue;
                }
                var effects = block.Effects(coilTier, voltageTier: 0);
                var steamSeconds = durationTicks / Ticks.PerSecond
                    * FactorySteamRules.DurationFactor(block) * effects.DurationFactor;
                var steamPerRun = context.Steam.LitersPerRecipeEu * euPerTick * durationTicks * effects.EuFactor;
                foreach (var steamItem in context.SteamItems())
                {
                    variants.Add(new RunVariant(
                        recipe, block.ItemId, 0, effects.Parallels, steamSeconds, 0,
                        effects.Estimated, steamItem, steamPerRun));
                }
            }
        }
        return variants;
    }

    /// <summary>The standard ladder trades quadrupled power for halved duration; a tree farm keeps its duration and multiplies its yield by the tier's gain.</summary>
    private static void AddOverclocks(
        List<RunVariant> variants,
        OverclockMode mode,
        int recipe,
        string? machineItemId,
        long durationTicks,
        long euPerTick,
        int requiredTier,
        int maxSteps,
        int perfectSteps,
        double heatEuFactor,
        BlockEffects effects)
    {
        var baseSeconds = durationTicks / Ticks.PerSecond * effects.DurationFactor;
        var baseEu = durationTicks * (double)euPerTick * heatEuFactor * effects.EuFactor * effects.DurationFactor;
        var treeFarm = mode == OverclockMode.TreeFarm;
        foreach (var overclock in Overclock.Ladder(maxSteps, perfectSteps, drawsPower: euPerTick > 0))
        {
            variants.Add(treeFarm
                ? new RunVariant(
                    recipe, machineItemId, overclock.Steps, effects.Parallels,
                    baseSeconds, baseEu * overclock.PowerMultiplier, effects.Estimated,
                    OutputFactor: TreeFarmYield.Gain(requiredTier, requiredTier + overclock.Steps))
                : new RunVariant(
                    recipe, machineItemId, overclock.Steps, effects.Parallels,
                    baseSeconds / overclock.DurationDivisor, baseEu * overclock.EuMultiplier, effects.Estimated));
        }
    }
}
