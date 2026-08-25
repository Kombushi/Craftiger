using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Interfaces.Recipes;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

/// <summary>The dump's tree rows carry the mode multipliers already; the sapling sits in the controller and the tools in the bus, none consumed, and every mode with a tool harvests in the same fixed-length run.</summary>
public sealed class TreeFarmRecipeService(
    IOptions<TreeFarmConfiguration> options,
    IRecipeMachineListService machineLists,
    ILogger<TreeFarmRecipeService> logger) : ITreeFarmRecipeService
{
    private readonly TreeFarmConfiguration _config = options.Value;

    public List<PlannerRecipe> Run(Dump dump, UnifiedItems unified)
    {
        var recipes = new List<PlannerRecipe>();
        var typeIds = dump.RecipeMapByTypeId
            .Where(pair => pair.Value.UnlocalizedName == _config.Map)
            .Select(pair => pair.Key)
            .ToHashSet();
        if (typeIds.Count == 0)
        {
            logger.LogWarning("tree farm map {Map} is unknown to this dump; no tree farm recipes", _config.Map);
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

    /// <summary>Per mode, the tools sharing the best multiplier the dump knows, as one catalyst slot of wearing tools; a tool the dump lacks is another pack's.</summary>
    private Dictionary<TreeFarmMode, (PlannerCatalystSlot Slot, int Multiplier)> BestTools(Dump dump, UnifiedItems unified)
    {
        var best = new Dictionary<TreeFarmMode, (PlannerCatalystSlot, int)>();
        var unknown = 0;
        foreach (var (mode, tools) in _config.Tools)
        {
            var known = tools.Where(tool => dump.Items.ContainsKey(tool.ItemId)).ToList();
            unknown += tools.Count - known.Count;
            if (known.Count == 0)
            {
                continue;
            }
            var multiplier = known.Max(tool => tool.Multiplier);
            var alternatives = known
                .Where(tool => tool.Multiplier == multiplier)
                .Select(tool => unified.Canonical(tool.ItemId))
                .Distinct()
                .Order(StringComparer.Ordinal)
                .Select(itemId => new PlannerCatalyst(itemId, 1, Tool: true))
                .ToList();
            best[mode] = (new PlannerCatalystSlot(alternatives), multiplier);
        }
        if (unknown > 0)
        {
            logger.LogWarning("{Count} tree farm tools are unknown to this dump; skipped", unknown);
        }
        return best;
    }
}
