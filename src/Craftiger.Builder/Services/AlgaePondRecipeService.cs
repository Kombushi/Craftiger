using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Interfaces.Recipes;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

/// <summary>The dump's pond rows are keyed by hatch tier and list no inputs: water is structure, power follows the hatch, and compost is the only thing a run can consume.</summary>
public sealed class AlgaePondRecipeService(
    IRecipeMachineListService machineLists,
    IOptions<FarmsConfiguration> options,
    ILogger<AlgaePondRecipeService> logger) : IAlgaePondRecipeService
{
    private readonly FarmsConfiguration _config = options.Value;

    public List<PlannerRecipe> Run(Dump dump, UnifiedItems unified)
    {
        var recipes = new List<PlannerRecipe>();
        var map = dump.MapServedBy(MachineClasses.AlgaePond);
        if (map is null)
        {
            logger.LogWarning("this dump grows no algae pond machine; no algae pond recipes");
            return recipes;
        }
        var typeIds = dump.TypeIdsOf(map);
        var machinesByTypeId = machineLists.Run(dump, unified);
        string? compostId = unified.Canonical(_config.CompostItemId);
        if (!dump.Items.ContainsKey(compostId))
        {
            logger.LogWarning("no item {ItemId}; algae pond rows ship without compost twins", _config.CompostItemId);
            compostId = null;
        }

        var rows = new SortedDictionary<int, DumpRecipe>();
        foreach (var recipe in dump.Recipes.Where(r => typeIds.Contains(r.RecipeTypeId)))
        {
            if (dump.GtByRecipeId.GetValueOrDefault(recipe.Id)?.SpecialValue is { } hatchTier && hatchTier >= 0)
            {
                rows[(int)hatchTier] = recipe;
            }
        }
        foreach (var (hatchTier, recipe) in rows)
        {
            // The ladder ends at MAX; a hatch past it has no garage to run in.
            if (hatchTier >= TierLadder.Names.Count)
            {
                continue;
            }
            // A superseded controller still serves the map in the dump; only the live one ships.
            var machines = (machinesByTypeId.GetValueOrDefault(recipe.RecipeTypeId) ?? [])
                .Where(machine => !dump.DeprecatedItems.Contains(machine.ItemId))
                .ToList();
            recipes.Add(Row(dump, unified, recipe.Id, map.Name, hatchTier, recipe, machines, compost: null));
            if (compostId is not null && rows.TryGetValue(hatchTier + 1, out var boosted))
            {
                recipes.Add(Row(
                    dump, unified, $"{recipe.Id}~c", map.Name, hatchTier, boosted, machines,
                    (compostId, AlgaePondRun.CompostFor(hatchTier))));
            }
        }

        logger.LogInformation("  {Count:N0} algae pond recipes", recipes.Count);
        return recipes;
    }

    private static PlannerRecipe Row(
        Dump dump, UnifiedItems unified, string id, string machine, int hatchTier, DumpRecipe source,
        IReadOnlyList<RecipeMachine> machines, (string ItemId, long Amount)? compost)
    {
        var outputs = new List<PlannerOutput>();
        foreach (var group in dump.ItemOutputsOf(source.Id)
            .Where(o => o.Size > 0 && o.Chance > 0)
            .GroupBy(o => unified.Canonical(o.ItemId)))
        {
            var expected = group.Sum(o => o.Size * Math.Min(o.Chance, 1.0));
            var amount = (long)Math.Ceiling(expected);
            outputs.Add(new PlannerOutput(group.Key, amount, expected / amount));
        }
        var inputs = new Dictionary<string, long>();
        var slots = new List<IReadOnlyList<string>>();
        if (compost is { } boost)
        {
            inputs[boost.ItemId] = boost.Amount;
            slots.Add([boost.ItemId]);
        }
        return new PlannerRecipe(
            id, machine, AlgaePondRun.LadderTier(hatchTier), Heat: null,
            dump.GtByRecipeId[source.Id].Duration, AlgaePondRun.EuT(hatchTier), Amps: 1,
            inputs, [], outputs, machines, slots,
            RequiresCleanroom: false, RequiresLowGravity: false)
        {
            ExactTier = true,
            Overclock = OverclockMode.Fixed,
            // A composted run consumes real matter and would price algae off compost, so only rate planning reads it.
            Scope = compost is null ? RecipeScope.None : RecipeScope.Factory,
        };
    }
}
