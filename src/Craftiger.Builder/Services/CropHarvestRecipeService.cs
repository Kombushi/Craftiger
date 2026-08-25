using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

public sealed class CropHarvestRecipeService(
    IOptions<SynthesizedMachinesConfiguration> options,
    ILogger<CropHarvestRecipeService> logger) : ICropHarvestRecipeService
{
    private readonly SynthesizedMachinesConfiguration _config = options.Value;

    public List<PlannerRecipe> Run(Dump dump, UnifiedItems unified)
    {
        var recipes = new List<PlannerRecipe>();
        foreach (var crop in dump.Crops)
        {
            if (crop.Hidden || crop.SeedId is not { } seedId || !dump.Items.ContainsKey(seedId))
            {
                continue;
            }

            var drops = crop.Drops
                .Where(dump.Items.ContainsKey)
                .Select(unified.Canonical)
                .Distinct()
                .Select(id => new PlannerOutput(id, 1, 1.0))
                .ToList();
            if (drops.Count == 0)
            {
                continue;
            }

            // A crop grows on any one of its accepted blocks, so the cheapest decides its era.
            var slots = new List<IReadOnlyList<string>> { new[] { unified.Canonical(seedId) } };
            var underBlocks = crop.UnderBlocks
                .Where(dump.Items.ContainsKey)
                .Select(unified.Canonical)
                .Distinct()
                .ToList();
            if (underBlocks.Count > 0)
            {
                slots.Add(underBlocks);
            }

            recipes.Add(new PlannerRecipe(
                crop.Id, _config.CropHarvestMachine, Tier: 0, Heat: null, DurationTicks: 0, EuT: 0, Amps: 1,
                Inputs: new Dictionary<string, long>(),
                Choices: [],
                Outputs: drops,
                Machines: [],
                InputSlotAlternatives: slots,
                RequiresCleanroom: false,
                RequiresLowGravity: false,
                EraOnly: true));
        }

        logger.LogInformation("  {Count:N0} crop harvests", recipes.Count);
        return recipes;
    }
}
