using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Craftiger.Builder.Interfaces.Recipes;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services.Recipes;

public sealed partial class RecipeTransformService(
    IOptions<RecipesConfiguration> options,
    IOptions<TreeFarmConfiguration> treeFarm,
    IRecipeMachineListService machineLists,
    IRecipeSlotResolver slotResolver,
    IRecipeVariantService variants,
    ICraftingGridService grids) : IRecipeTransformService
{
    private readonly RecipesConfiguration _config = options.Value;
    private readonly TreeFarmConfiguration _treeFarm = treeFarm.Value;
    private readonly FrozenSet<string> _excludedMachines = options.Value.ExcludedMachines.ToFrozenSet();
    private readonly FrozenSet<string> _eraOnlyMachines = options.Value.EraOnlyMachines.ToFrozenSet();

    [GeneratedRegex(@" \((ULV|LV|MV|HV|EV|IV|LuV|ZPM|UV|UHV|UEV|UIV|UMV|UXV|MAX)\)$")]
    private static partial Regex TierSuffix();

    public List<PlannerRecipe> Run(Dump dump, UnifiedItems unified)
    {
        var result = new List<PlannerRecipe>();
        var tools = new ToolIndex(dump.Items, dump.ItemContainers);
        var machinesByTypeId = machineLists.Run(dump, unified);
        var ungatedTypeIds = new HashSet<string>(dump.Recipes.Where(r => r.Category == "minecraft").Select(r => r.RecipeTypeId));
        var slotTiersByMap = new Dictionary<string, IReadOnlyList<int>?>();

        foreach (var recipe in dump.Recipes)
        {
            var machine = NormalizeMachine(recipe.Machine);
            if (IsExcluded(machine))
            {
                continue;
            }

            // Fuel maps burn their inputs for EU; their tabs are recipes only to NEI. Tree farm rows are synthesized with their catalysts.
            var map = dump.RecipeMapByTypeId.GetValueOrDefault(recipe.RecipeTypeId);
            if (map is not null && (map.IsFuel || map.UnlocalizedName == _treeFarm.Map))
            {
                continue;
            }
            if (_config.PhantomRecipeIds.ContainsKey(recipe.Id))
            {
                continue;
            }

            var gt = dump.GtByRecipeId.GetValueOrDefault(recipe.Id);
            var tier = gt?.Voltage is not > 0 ? 0 : TierLadder.LabelTier(gt.TierLabel) ?? TierLadder.VoltageTier(gt.Voltage.Value);

            var inputs = new Dictionary<string, long>();
            var choices = new List<PlannerChoice>();
            var catalysts = new List<PlannerCatalystSlot>();
            var slots = new List<IReadOnlyList<string>>();
            // A shaped crafting type keys its inputs by grid cell; each cell remembers what it became.
            var cellRefs = recipe is { Category: "minecraft", Shapeless: false } ? new List<GridCellRef>() : null;
            foreach (var (cell, groupId) in dump.ItemInputsOf(recipe.Id))
            {
                var slot = slotResolver.Resolve(dump, unified, tools, groupId);
                if (slot.Members.Count == 0)
                {
                    continue;
                }

                var alternatives = slot.Alternatives;
                if (slot.Catalyst)
                {
                    cellRefs?.Add(new GridCellRef((int)cell, null, null, catalysts.Count));
                    catalysts.Add(new PlannerCatalystSlot(
                        [.. alternatives.Select(member => new PlannerCatalyst(member.ItemId, member.Amount, member.Tool))]));
                    continue;
                }

                if (alternatives.Count > 1)
                {
                    // A real choice of ingredient ships every option at its own amount for the solver to pick from.
                    cellRefs?.Add(new GridCellRef((int)cell, null, choices.Count, null));
                    choices.Add(new PlannerChoice([.. alternatives.Select(member => (member.ItemId, member.Amount))]));
                    slots.Add([.. alternatives.Select(member => member.ItemId)]);
                    continue;
                }

                string? cellItem = null;
                foreach (var (partId, partAmount) in dump.Decompose(slot.Members[0].ItemId, slot.Members[0].Amount))
                {
                    var canonical = unified.Canonical(partId);
                    // A filled container splits into its empty form and the fluid; the cell shows the container.
                    cellItem ??= canonical;
                    inputs[canonical] = inputs.GetValueOrDefault(canonical) + partAmount;
                }
                cellRefs?.Add(new GridCellRef((int)cell, cellItem, null, null));

                // Single-alternative slots decompose like the flat inputs; genuine alternative lists stay whole.
                var stacks = dump.GroupStacks[groupId];
                if (stacks.Count == 1)
                {
                    foreach (var (partId, _) in dump.Decompose(stacks[0].ItemId, 1))
                    {
                        slots.Add([unified.Canonical(partId)]);
                    }
                }
                else
                {
                    slots.Add([.. alternatives.Select(member => member.ItemId)]);
                }
            }
            foreach (var fluid in dump.FluidInputsOf(recipe.Id))
            {
                var members = fluid.Members
                    .Where(member => member.Amount > 0)
                    .DistinctBy(member => member.FluidId)
                    .ToList();
                if (members.Count == 0)
                {
                    continue;
                }
                if (members.Count > 1)
                {
                    choices.Add(new PlannerChoice([.. members.Select(member => (member.FluidId, member.Amount))]));
                    slots.Add([.. members.Select(member => member.FluidId)]);
                    continue;
                }
                inputs[members[0].FluidId] = inputs.GetValueOrDefault(members[0].FluidId) + members[0].Amount;
                slots.Add([members[0].FluidId]);
            }

            var outputs = new List<SlotOutput>();
            foreach (var o in dump.ItemOutputsOf(recipe.Id))
            {
                if (o.Size <= 0 || o.Chance <= 0)
                {
                    continue;
                }
                foreach (var (partId, partAmount) in dump.Decompose(o.ItemId, o.Size))
                {
                    outputs.Add(new SlotOutput(new PlannerOutput(unified.Canonical(partId), partAmount, Math.Min(o.Chance, 1.0)), o.Slot));
                }
            }
            foreach (var o in dump.FluidOutputsOf(recipe.Id))
            {
                if (o.Amount <= 0 || o.Chance <= 0)
                {
                    continue;
                }
                outputs.Add(new SlotOutput(new PlannerOutput(o.FluidId, o.Amount, Math.Min(o.Chance, 1.0)), 0));
            }
            var ingredients = inputs.Keys
                .Concat(choices.SelectMany(choice => choice.Alternatives.Select(a => a.ItemId)))
                .ToList();
            var recycling = gt is not null && IsRecycling(gt.Category);
            if (recycling && !ingredients.All(id => IsMaterialShape(id, dump, unified)))
            {
                continue;
            }

            var slotTiers = map is null
                ? null
                : slotTiersByMap.TryGetValue(map.UnlocalizedName, out var known)
                    ? known
                    : slotTiersByMap[map.UnlocalizedName] = map.ByproductSlotTiers();
            foreach (var variant in variants.Variants(recipe.Id, tier, outputs, slotTiers))
            {
                var (variantInputs, merged) = PlannerOutputs.Net(inputs, PlannerOutputs.Merge(variant.Outputs));
                if (merged.Count == 0 || (variantInputs.Count == 0 && choices.Count == 0))
                {
                    continue;
                }

                result.Add(new PlannerRecipe(
                    variant.Id, machine, variant.Tier, gt?.Heat,
                    gt?.Duration ?? 0, gt?.Voltage ?? 0, gt?.Amperage ?? 1,
                    variantInputs, choices, merged,
                    ungatedTypeIds.Contains(recipe.RecipeTypeId)
                        ? []
                        : machinesByTypeId.GetValueOrDefault(recipe.RecipeTypeId) ?? [],
                    slots,
                    gt?.RequiresCleanroom ?? false,
                    gt?.RequiresLowGravity ?? false,
                    _eraOnlyMachines.Contains(machine))
                {
                    Catalysts = catalysts,
                    Grid = cellRefs is null ? null : grids.GridOf(cellRefs, variantInputs, choices.Count),
                });
            }
        }

        return result;
    }

    /// <summary>Voltage suffix stripped first, so one configured rename covers every tiered variant of a map.</summary>
    private string NormalizeMachine(string type)
    {
        var stripped = TierSuffix().Replace(type, "");
        return _config.MachineRenames.GetValueOrDefault(stripped, stripped);
    }

    private bool IsRecycling(string category) =>
        _config.RecyclingCategorySuffixes.Any(suffix => category.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether an ingredient is one shape of a single material by GT's own prefix flags; a molten metal qualifies.</summary>
    private static bool IsMaterialShape(string id, Dump dump, UnifiedItems unified) =>
        dump.IsFluid(id) || unified.OredictsOf(id).Any(dump.OrePrefixes.IsMaterialShape);

    private bool IsExcluded(string machine) => _excludedMachines.Contains(machine);
}
