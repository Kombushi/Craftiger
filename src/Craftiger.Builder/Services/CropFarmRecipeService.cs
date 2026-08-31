using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

/// <summary>One run is one maturation wave of the whole field, so machine count follows from runs in flight.</summary>
public sealed class CropFarmRecipeService(
    IOptions<FarmsConfiguration> options,
    ILogger<CropFarmRecipeService> logger) : ICropFarmRecipeService
{
    /// <summary>The additive share of a bonus drop each round carries at gain zero.</summary>
    private const double DropCountIncrease = 0.01;

    private readonly FarmsConfiguration _config = options.Value;

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
            var fertilized = crop.Tier >= CropGrowth.FertilizerTier;
            if (fertilized && fertilizers.Count == 0)
            {
                skipped++;
                continue;
            }
            var maturation = CropGrowth.MaturationTicks(crop.GrowthDuration, crop.Tier, fertilized);
            if (maturation == 0)
            {
                skipped++;
                continue;
            }

            if (crop.MinSeedBedTier < 0)
            {
                foreach (var (tier, machine) in managers)
                {
                    recipes.Add(Row(
                        dump, crop, unified, $"farm~{crop.Id}~cm{tier}", _config.CropManagerMap, machine, tier,
                        euT: 0, CropGrowth.RoundBonus(tier, 0.05), maturation, fertilized, drops, waterId, fertilizers));
                }
            }
            if (farm is { } farmMachine)
            {
                var minTier = Math.Max(_config.IndustrialFarmMinTier, crop.MinSeedBedTier);
                for (var tier = minTier; tier <= _config.IndustrialFarmMaxTier; tier++)
                {
                    recipes.Add(Row(
                        dump, crop, unified, $"farm~{crop.Id}~if{tier}", _config.IndustrialFarmMap, farmMachine, tier,
                        TierLadder.PracticalVoltage(tier), CropGrowth.RoundBonus(tier, 0.2), maturation, fertilized,
                        drops, waterId, fertilizers));
                }
            }
        }

        logger.LogInformation("  {Rows:N0} crop farm rows, {Skipped:N0} crops unfarmable", recipes.Count, skipped);
        return new CropFarms(recipes, machines);
    }

    private PlannerRecipe Row(
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
        bool fertilized,
        IReadOnlyList<DumpCropDrop> drops,
        string? waterId,
        IReadOnlyList<(string ItemId, int Potency)> fertilizers)
    {
        var field = CropGrowth.FieldSize(tier);
        var rounds = crop.DropChance * roundBonus * field;
        var outputs = new List<PlannerOutput>();
        foreach (var group in drops.GroupBy(drop => unified.Canonical(drop.ItemId)))
        {
            var expected = group.Sum(drop => rounds * (drop.Weight / 10_000.0) * (1 + DropCountIncrease));
            if (expected <= 0)
            {
                continue;
            }
            var amount = (long)Math.Ceiling(expected);
            outputs.Add(new PlannerOutput(group.Key, amount, expected / amount));
        }

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
            Overclock = OverclockMode.Fixed,
            Scope = RecipeScope.Factory,
        };
    }
}
