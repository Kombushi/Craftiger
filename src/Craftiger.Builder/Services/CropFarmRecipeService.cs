using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

/// <summary>One run is one maturation wave of the whole field, so machine count follows from runs in flight; variants span fertilizer, farm builds and bred seeds.</summary>
public sealed class CropFarmRecipeService(
    IOptions<FarmsConfiguration> options,
    ILogger<CropFarmRecipeService> logger) : ICropFarmRecipeService
{
    private readonly FarmsConfiguration _config = options.Value;

    private sealed record SeedStats(int Growth, int Gain, string Suffix, RecipeScope Scope);

    private static readonly SeedStats Fresh = new(CropGrowth.MinStat, CropGrowth.MinStat, "", RecipeScope.Factory);
    private static readonly SeedStats Bred = new(CropGrowth.MaxStat, CropGrowth.MaxStat, "~b", RecipeScope.FactoryBred);

    public CropFarms Run(Dump dump, UnifiedItems unified)
    {
        var machines = new List<PlannerMachineItem>();
        var managers = new List<(int Tier, RecipeMachine Machine)>();
        for (var tier = 1; tier <= _config.CropManagerItemIds.Count; tier++)
        {
            var itemId = unified.Canonical(_config.CropManagerItemIds[tier - 1]);
            if (!dump.Items.ContainsKey(itemId))
            {
                logger.LogWarning("crop manager {ItemId} is unknown to this dump; its tier ships no rows", itemId);
                continue;
            }
            machines.Add(new PlannerMachineItem(_config.CropManagerMap, itemId, tier, Multiblock: false, Steam: false, Era: null));
            managers.Add((tier, new RecipeMachine(itemId, Multiblock: false, tier, Steam: false)));
        }
        var farmId = unified.Canonical(_config.IndustrialFarmItemId);
        RecipeMachine? farm = null;
        if (dump.Items.ContainsKey(farmId))
        {
            machines.Add(new PlannerMachineItem(_config.IndustrialFarmMap, farmId, Tier: null, Multiblock: true, Steam: false, Era: null));
            farm = new RecipeMachine(farmId, Multiblock: true, Tier: null, Steam: false);
        }
        else
        {
            logger.LogWarning("industrial farm {ItemId} is unknown to this dump; no farm rows ship", farmId);
        }

        var waterId = dump.FluidIdsNamed(_config.WaterFluidName).Select(unified.Canonical).Order(StringComparer.Ordinal).FirstOrDefault();
        if (waterId is null)
        {
            logger.LogWarning("no fluid named '{Name}'; farm rows ship without water", _config.WaterFluidName);
        }
        var fertilizers = _config.Fertilizers
            .Select(f => (ItemId: unified.Canonical(f.ItemId), f.Potency))
            .Where(f => dump.Items.ContainsKey(f.ItemId))
            .ToList();
        var liquidFertilizer = KnownFluid(dump, _config.LiquidFertilizerFluidId);
        var enrichedFertilizer = KnownFluid(dump, _config.EnrichedFertilizerFluidId);

        var recipes = new List<PlannerRecipe>();
        var skipped = 0;
        foreach (var crop in dump.Crops)
        {
            if (crop.Hidden || crop.SeedId is not { } seedId || !dump.Items.ContainsKey(seedId))
            {
                continue;
            }
            var drops = crop.Drops.Where(drop => dump.Items.ContainsKey(drop.ItemId)).ToList();
            if (drops.Count == 0)
            {
                continue;
            }
            var grown = recipes.Count;
            foreach (var stats in new[] { Fresh, Bred })
            {
                if (crop.MinSeedBedTier < 0)
                {
                    foreach (var (tier, machine) in managers)
                    {
                        foreach (var fertilized in FertilizerAxis(crop))
                        {
                            if (fertilized && fertilizers.Count == 0)
                            {
                                continue;
                            }
                            AddManagerRow(recipes, dump, crop, unified, machine, tier, stats, fertilized, drops, waterId, fertilizers);
                        }
                    }
                }
                if (farm is { } farmMachine && liquidFertilizer is not null && enrichedFertilizer is not null)
                {
                    var minTier = Math.Max(_config.IndustrialFarmMinTier, crop.MinSeedBedTier);
                    for (var tier = minTier; tier <= _config.IndustrialFarmMaxTier; tier++)
                    {
                        foreach (var build in FarmBuild.Of(tier))
                        {
                            foreach (var fertilized in build.Enriched ? new[] { true } : FertilizerAxis(crop))
                            {
                                AddFarmRow(
                                    recipes, dump, crop, unified, farmMachine, tier, build, stats, fertilized,
                                    drops, waterId, build.Enriched ? enrichedFertilizer : liquidFertilizer,
                                    build.Enriched ? _config.EnrichedFertilizerPotency : _config.LiquidFertilizerPotency);
                            }
                        }
                    }
                }
            }
            if (recipes.Count == grown)
            {
                skipped++;
            }
        }

        logger.LogInformation("  {Rows:N0} crop farm rows, {Skipped:N0} crops unfarmable", recipes.Count, skipped);
        return new CropFarms(recipes, machines);
    }

    /// <summary>Below the fertilizer wall both stick states ship as competing rows; from it only fertilized sticks grow.</summary>
    private static IEnumerable<bool> FertilizerAxis(DumpCrop crop) =>
        crop.Tier >= CropGrowth.FertilizerTier ? [true] : [false, true];

    private string? KnownFluid(Dump dump, string fluidId)
    {
        if (dump.IsFluid(fluidId))
        {
            return fluidId;
        }
        logger.LogWarning("farm fertilizer fluid {FluidId} is unknown to this dump; farm rows ship without it", fluidId);
        return null;
    }

    private void AddManagerRow(
        List<PlannerRecipe> recipes, Dump dump, DumpCrop crop, UnifiedItems unified,
        RecipeMachine machine, int tier, SeedStats stats, bool fertilized,
        IReadOnlyList<DumpCropDrop> drops, string? waterId,
        IReadOnlyList<(string ItemId, int Potency)> fertilizers)
    {
        var maturation = CropGrowth.MaturationTicks(crop.GrowthDuration, crop.Tier, fertilized, stats.Growth);
        if (maturation == 0)
        {
            return;
        }
        var field = CropGrowth.FieldSize(tier);
        var potency = field * CropGrowth.WaterPerSeed(maturation);
        var inputs = new Dictionary<string, long>();
        var slots = new List<IReadOnlyList<string>>();
        if (waterId is not null)
        {
            inputs[waterId] = potency;
            slots.Add([waterId]);
        }
        var choices = new List<PlannerChoice>();
        if (fertilized)
        {
            choices.Add(new PlannerChoice(
                [.. fertilizers.Select(f => (f.ItemId, (long)Math.Ceiling(potency / (double)f.Potency)))]));
            slots.Add([.. fertilizers.Select(f => f.ItemId)]);
        }
        var id = $"farm~{crop.Id}~cm{tier}{FertilizerSuffix(crop, fertilized)}{stats.Suffix}";
        recipes.Add(Row(
            dump, crop, unified, id, _config.CropManagerMap, machine, tier, euT: 0,
            CropGrowth.RoundBonus(tier, 0.05), maturation, stats, field, drops, inputs, choices, slots,
            overclocked: false, stats.Scope));
    }

    private void AddFarmRow(
        List<PlannerRecipe> recipes, Dump dump, DumpCrop crop, UnifiedItems unified,
        RecipeMachine machine, int tier, FarmBuild build, SeedStats stats, bool fertilized,
        IReadOnlyList<DumpCropDrop> drops, string? waterId, string fertilizerId, int fertilizerPotency)
    {
        var maturation = CropGrowth.MaturationTicks(crop.GrowthDuration, crop.Tier, fertilized, stats.Growth);
        if (maturation == 0)
        {
            return;
        }
        var scaled = Math.Max(1, (long)Math.Round(maturation / build.SpeedFactor));
        var field = CropGrowth.FieldSize(tier);
        var potency = field * CropGrowth.WaterPerSeed(scaled);
        var inputs = new Dictionary<string, long>();
        var slots = new List<IReadOnlyList<string>>();
        if (waterId is not null)
        {
            inputs[waterId] = potency;
            slots.Add([waterId]);
        }
        if (fertilized)
        {
            inputs[fertilizerId] = (long)Math.Ceiling(potency / (double)fertilizerPotency);
            slots.Add([fertilizerId]);
        }
        var id = $"farm~{crop.Id}~if{tier}{build.Suffix}{FertilizerSuffix(crop, fertilized && !build.Enriched)}{stats.Suffix}";
        recipes.Add(Row(
            dump, crop, unified, id, _config.IndustrialFarmMap, machine, tier,
            build.PowerOf(TierLadder.PracticalVoltage(tier)), build.RoundFactor(tier), scaled, stats, field,
            drops, inputs, [], slots, build.Overclocked, stats.Scope));
    }

    /// <summary>Marks the optional fertilized twin below the wall; at and above it fertilizer is implied.</summary>
    private static string FertilizerSuffix(DumpCrop crop, bool fertilized) =>
        fertilized && crop.Tier < CropGrowth.FertilizerTier ? "~f" : "";

    private static PlannerRecipe Row(
        Dump dump,
        DumpCrop crop,
        UnifiedItems unified,
        string id,
        string map,
        RecipeMachine machine,
        int tier,
        long euT,
        double roundBonus,
        long maturation,
        SeedStats stats,
        int field,
        IReadOnlyList<DumpCropDrop> drops,
        Dictionary<string, long> inputs,
        List<PlannerChoice> choices,
        List<IReadOnlyList<string>> slots,
        bool overclocked,
        RecipeScope scope)
    {
        var rounds = crop.DropChance * CropGrowth.GainRounds(stats.Gain) * roundBonus * field;
        var outputs = new List<PlannerOutput>();
        foreach (var group in drops.GroupBy(drop => unified.Canonical(drop.ItemId)))
        {
            var expected = group.Sum(drop =>
                rounds * (drop.Weight / 10_000.0) * (1 + CropGrowth.GainStackBonus(stats.Gain)));
            if (expected <= 0)
            {
                continue;
            }
            var amount = (long)Math.Ceiling(expected);
            outputs.Add(new PlannerOutput(group.Key, amount, expected / amount));
        }

        var catalysts = new List<PlannerCatalystSlot>
        {
            new([new PlannerCatalyst(unified.Canonical(crop.SeedId!), field, Tool: false)]),
        };
        var underBlocks = crop.UnderBlocks
            .Where(dump.Items.ContainsKey)
            .Select(unified.Canonical)
            .Distinct()
            .ToList();
        if (underBlocks.Count > 0)
        {
            catalysts.Add(new PlannerCatalystSlot([.. underBlocks.Select(b => new PlannerCatalyst(b, field, Tool: false))]));
        }

        return new PlannerRecipe(
            id, map, tier, Heat: null, maturation, euT, Amps: 1,
            inputs, choices, outputs, [machine], slots,
            RequiresCleanroom: false, RequiresLowGravity: false)
        {
            Catalysts = catalysts,
            Overclock = overclocked ? OverclockMode.Standard : OverclockMode.Fixed,
            Scope = scope,
        };
    }
}
