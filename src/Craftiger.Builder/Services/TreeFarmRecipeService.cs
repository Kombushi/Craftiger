using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Interfaces.Recipes;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;

namespace Craftiger.Builder.Services;

/// <summary>The dump's tree rows carry the mode multipliers already; the sapling sits in the controller and the tools in the bus, none consumed, and every mode with a tool harvests in the same fixed-length run.</summary>
public sealed class TreeFarmRecipeService(
    IRecipeMachineListService machineLists,
    ILogger<TreeFarmRecipeService> logger) : ITreeFarmRecipeService
{
    public List<PlannerRecipe> Run(Dump dump, UnifiedItems unified)
    {
        var recipes = new List<PlannerRecipe>();
        var map = dump.MapServedBy(MachineClasses.TreeFarm);
        var typeIds = map is null
            ? []
            : dump.RecipeMapByTypeId
                .Where(pair => pair.Value.UnlocalizedName == map.UnlocalizedName)
                .Select(pair => pair.Key)
                .ToHashSet();
        if (typeIds.Count == 0)
        {
            logger.LogWarning("this dump grows no tree farm machine; no tree farm recipes");
            return recipes;
        }
        var machinesByTypeId = machineLists.Run(dump, unified);
        var tools = BestTools(dump, unified);
        var saplingless = 0;

        foreach (var recipe in dump.Recipes.Where(r => typeIds.Contains(r.RecipeTypeId)))
        {
            var gt = dump.GtByRecipeId.GetValueOrDefault(recipe.Id);
            if (gt?.SpecialItemId is not { } rawSapling || !dump.Items.ContainsKey(rawSapling))
            {
                saplingless++;
                continue;
            }
            var catalysts = new List<PlannerCatalystSlot>
            {
                new([new PlannerCatalyst(unified.Canonical(rawSapling), 1, Tool: false)]),
            };
            var outputs = new List<PlannerOutput>();
            foreach (var output in dump.ItemOutputsOf(recipe.Id).OrderBy(o => o.Slot))
            {
                if (output.Size <= 0 || !Enum.IsDefined((TreeFarmMode)output.Slot)
                    || !tools.TryGetValue((TreeFarmMode)output.Slot, out var mode))
                {
                    continue;
                }
                var amount = output.Size * TreeFarmYield.TierMultiplier(TreeFarmYield.BaseTier) * mode.Multiplier;
                outputs.Add(new PlannerOutput(unified.Canonical(output.ItemId), amount, 1.0));
                catalysts.Add(mode.Slot);
            }
            if (outputs.Count == 0)
            {
                continue;
            }

            recipes.Add(new PlannerRecipe(
                recipe.Id, dump.RecipeMapByTypeId[recipe.RecipeTypeId].Name,
                Tier: TreeFarmYield.BaseTier, Heat: null,
                DurationTicks: gt.Duration, EuT: TierLadder.PracticalVoltage(TreeFarmYield.BaseTier), Amps: 1,
                Inputs: new Dictionary<string, long>(),
                Choices: [],
                Outputs: outputs,
                Machines: machinesByTypeId.GetValueOrDefault(recipe.RecipeTypeId) ?? [],
                InputSlotAlternatives: [],
                RequiresCleanroom: false,
                RequiresLowGravity: false)
            {
                Catalysts = catalysts,
                Overclock = OverclockMode.TreeFarm,
            });
        }

        if (saplingless > 0)
        {
            logger.LogWarning("{Count:N0} tree farm rows name no sapling the dump knows; skipped", saplingless);
        }
        logger.LogInformation("  {Count:N0} tree farm recipes", recipes.Count);
        return recipes;
    }

    /// <summary>Per mode, the tools sharing the best multiplier, as one catalyst slot of wearing tools; lesser tools never price and stay out.</summary>
    private static Dictionary<TreeFarmMode, (PlannerCatalystSlot Slot, int Multiplier)> BestTools(Dump dump, UnifiedItems unified)
    {
        var best = new Dictionary<TreeFarmMode, (PlannerCatalystSlot, int)>();
        foreach (var tools in dump.TreeFarmTools.GroupBy(tool => tool.Mode))
        {
            var multiplier = tools.Max(tool => tool.Multiplier);
            var alternatives = tools
                .Where(tool => tool.Multiplier == multiplier)
                .Select(tool => unified.Canonical(tool.ItemId))
                .Distinct()
                .Order(StringComparer.Ordinal)
                .Select(itemId => new PlannerCatalyst(itemId, 1, Tool: true))
                .ToList();
            best[tools.Key] = (new PlannerCatalystSlot(alternatives), multiplier);
        }
        return best;
    }
}
