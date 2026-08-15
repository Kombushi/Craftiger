using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Microsoft.Extensions.Logging;

namespace Craftiger.Builder.Services;

public sealed class BlockBreakRecipeService(BuilderConfig config, ILogger<BlockBreakRecipeService> logger)
    : IBlockBreakRecipeService
{
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
                drop.Id, config.BlockBreakMachine, Tier: 0, Heat: null, DurationTicks: 0, EuT: 0,
                Inputs: new Dictionary<string, long> { [blockId] = 1 },
                Outputs: [new PlannerOutput(dropId, drop.Quantity, 1.0)],
                Machines: [],
                InputSlotAlternatives: [new[] { blockId }],
                RequiresCleanroom: false));
        }

        logger.LogInformation("  {Count:N0} block-break recipes", recipes.Count);
        return recipes;
    }
}
