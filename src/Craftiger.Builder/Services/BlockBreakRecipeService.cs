using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Craftiger.Builder.Models.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

public sealed class BlockBreakRecipeService(
    IOptions<SynthesizedMachinesConfiguration> options,
    ILogger<BlockBreakRecipeService> logger) : IBlockBreakRecipeService
{
    private readonly SynthesizedMachinesConfiguration _config = options.Value;

    public List<PlannerRecipe> Run(Dump dump, UnifiedItems unified)
    {
        var recipes = new List<PlannerRecipe>();
        var seen = new HashSet<string>();

        foreach (var drop in dump.BlockDrops)
        {
            if (!dump.Items.ContainsKey(drop.BlockItemId) || !dump.Items.ContainsKey(drop.DropItemId))
            {
                continue;
            }

            var blockId = unified.Canonical(drop.BlockItemId);
            var dropId = unified.Canonical(drop.DropItemId);
            // A block dropping itself is no conversion, just picking it back up.
            if (blockId == dropId || !seen.Add(drop.Id))
            {
                continue;
            }

            recipes.Add(new PlannerRecipe(
                drop.Id, _config.BlockBreakMachine, Tier: 0, Heat: null, DurationTicks: 0, EuT: 0,
                Inputs: new Dictionary<string, long> { [blockId] = 1 },
                Choices: [],
                Outputs: [new PlannerOutput(dropId, drop.Quantity, 1.0)],
                Machines: [],
                InputSlotAlternatives: [new[] { blockId }],
                RequiresCleanroom: false));
        }

        logger.LogInformation("  {Count:N0} block-break recipes", recipes.Count);
        return recipes;
    }
}
