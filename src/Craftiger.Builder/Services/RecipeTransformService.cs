using System.Text.RegularExpressions;
using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Craftiger.Builder.Models.Options;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

public sealed partial class RecipeTransformService(IOptions<RecipesConfiguration> options) : IRecipeTransformService
{
    private readonly RecipesConfiguration _config = options.Value;

    [GeneratedRegex(@" \((ULV|LV|MV|HV|EV|IV|LuV|ZPM|UV|UHV|UEV|UIV|UMV|UXV|MAX)\)$")]
    private static partial Regex TierSuffix();

    public List<PlannerRecipe> Run(Dump dump, UnifiedItems unified)
    {
        var result = new List<PlannerRecipe>();
        var tools = new ToolIndex(dump);

        // Machine items gate an era only when they exist as real craftable items.
        var machinesByTypeId = new Dictionary<string, IReadOnlyList<RecipeMachine>>();
        foreach (var (typeId, icons) in dump.HandlerItemsByRecipeTypeId)
        {
            machinesByTypeId[typeId] = icons
                .Where(dump.Items.ContainsKey)
                .Select(unified.Canonical)
                .Distinct()
                .Select(id => new RecipeMachine(id, Multiblock: false, Tier: null, Steam: false))
                .ToList();
        }
        // GregTech maps name their real machines, which the NEI handler icons only approximate.
        foreach (var (typeId, map) in dump.RecipeMapByTypeId)
        {
            var machines = map.Machines
                .Where(m => dump.Items.ContainsKey(m.ItemId))
                .GroupBy(m => unified.Canonical(m.ItemId))
                .Select(g => new RecipeMachine(
                    g.Key, g.Any(m => m.Multiblock), g.Min(m => m.Tier), g.Any(m => m.Steam)))
                .ToList();
            if (machines.Count > 0)
            {
                machinesByTypeId[typeId] = machines;
            }
        }

        var ungatedTypeIds = new HashSet<string>(
            dump.Recipes.Where(r => r.Category == "minecraft").Select(r => r.RecipeTypeId));

        foreach (var recipe in dump.Recipes)
        {
            var machine = NormalizeMachine(recipe.Machine);
            if (IsExcluded(machine))
            {
                continue;
            }
            // Fuel maps burn their inputs for EU; their tabs are recipes only to NEI.
            if (dump.RecipeMapByTypeId.GetValueOrDefault(recipe.RecipeTypeId) is { IsFuel: true })
            {
                continue;
            }
            if (_config.PhantomRecipeIds.ContainsKey(recipe.Id))
            {
                continue;
            }

            var gt = dump.GtByRecipeId.GetValueOrDefault(recipe.Id);
            var tier = gt is null || gt.Voltage is not > 0
                ? 0
                : TierLadder.LabelTier(gt.TierLabel) ?? TierLadder.VoltageTier(gt.Voltage.Value);

            var inputs = new Dictionary<string, long>();
            var choices = new List<PlannerChoice>();
            var catalysts = new List<PlannerCatalystSlot>();
            var slots = new List<IReadOnlyList<string>>();
            // A shaped crafting type keys its inputs by grid cell; each cell remembers what it
            // became, so the shape can be rebuilt over the folded slots once they are final.
            var cellRefs = recipe.Category == "minecraft" && !recipe.Shapeless ? new List<CellRef>() : null;
            foreach (var (cell, groupId) in dump.ItemInputsByRecipe.GetValueOrDefault(recipe.Id) ?? [])
            {
                var (members, catalyst) = ResolveSlot(dump, unified, tools, groupId);
                if (members.Count == 0)
                {
                    continue;
                }

                var alternatives = members.DistinctBy(member => member.ItemId).ToList();
                if (catalyst)
                {
                    cellRefs?.Add(new CellRef((int)cell, null, null, catalysts.Count));
                    catalysts.Add(new PlannerCatalystSlot(
                        alternatives.Select(member => new PlannerCatalyst(member.ItemId, member.Amount, member.Tool)).ToList()));
                    continue;
                }

                if (alternatives.Count > 1)
                {
                    // A real choice of ingredient: ship every option at its own amount so
                    // the solver can take the cheapest, rather than freezing one at build time.
                    cellRefs?.Add(new CellRef((int)cell, null, choices.Count, null));
                    choices.Add(new PlannerChoice(alternatives.Select(member => (member.ItemId, member.Amount)).ToList()));
                    slots.Add(alternatives.Select(member => member.ItemId).ToList());
                    continue;
                }

                string? cellItem = null;
                foreach (var (partId, partAmount) in Decompose(dump, members[0].ItemId, members[0].Amount))
                {
                    var canonical = unified.Canonical(partId);
                    // A filled container splits into its empty form and the fluid; the cell shows the container.
                    cellItem ??= canonical;
                    inputs[canonical] = inputs.GetValueOrDefault(canonical) + partAmount;
                }
                cellRefs?.Add(new CellRef((int)cell, cellItem, null, null));

                // Single-alternative slots decompose like the flat inputs; genuine
                // alternative lists keep their members whole.
                var stacks = dump.GroupStacks[groupId];
                if (stacks.Count == 1)
                {
                    foreach (var (partId, _) in Decompose(dump, stacks[0].ItemId, 1))
                    {
                        slots.Add([unified.Canonical(partId)]);
                    }
                }
                else
                {
                    slots.Add(alternatives.Select(member => member.ItemId).ToList());
                }
            }
            foreach (var fluid in dump.FluidInputsByRecipe.GetValueOrDefault(recipe.Id) ?? [])
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
                    choices.Add(new PlannerChoice(
                        members.Select(member => (member.FluidId, member.Amount)).ToList()));
                    slots.Add(members.Select(member => member.FluidId).ToList());
                    continue;
                }
                inputs[members[0].FluidId] = inputs.GetValueOrDefault(members[0].FluidId) + members[0].Amount;
                slots.Add([members[0].FluidId]);
            }

            var outputs = new List<(PlannerOutput Output, long Slot)>();
            foreach (var o in dump.ItemOutputsByRecipe.GetValueOrDefault(recipe.Id) ?? [])
            {
                if (o.Size <= 0 || o.Chance <= 0)
                {
                    continue;
                }
                foreach (var (partId, partAmount) in Decompose(dump, o.ItemId, o.Size))
                {
                    outputs.Add((new PlannerOutput(unified.Canonical(partId), partAmount, Math.Min(o.Chance, 1.0)), o.Slot));
                }
            }
            foreach (var o in dump.FluidOutputsByRecipe.GetValueOrDefault(recipe.Id) ?? [])
            {
                if (o.Amount <= 0 || o.Chance <= 0)
                {
                    continue;
                }
                outputs.Add((new PlannerOutput(o.FluidId, o.Amount, Math.Min(o.Chance, 1.0)), 0));
            }
            var ingredients = inputs.Keys
                .Concat(choices.SelectMany(choice => choice.Alternatives.Select(a => a.ItemId)))
                .ToList();
            var recycling = gt is not null && IsRecycling(gt.Category);
            if (recycling && !ingredients.All(id => IsMaterialShape(id, dump, unified)))
            {
                continue;
            }

            var slotTiers = _config.ByproductSlotTiers.GetValueOrDefault(machine);
            foreach (var (variantId, variantTier, variantOutputs) in Variants(recipe.Id, tier, outputs, slotTiers))
            {
                var variantInputs = new Dictionary<string, long>(inputs);
                var merged = Merge(variantOutputs);
                Net(variantInputs, merged);
                if (merged.Count == 0 || (variantInputs.Count == 0 && choices.Count == 0))
                {
                    continue;
                }

                result.Add(new PlannerRecipe(
                    variantId, machine, variantTier, gt?.Heat,
                    gt?.Duration ?? 0, gt?.Voltage ?? 0,
                    variantInputs, choices, merged,
                    ungatedTypeIds.Contains(recipe.RecipeTypeId)
                        ? []
                        : machinesByTypeId.GetValueOrDefault(recipe.RecipeTypeId) ?? [],
                    slots,
                    gt?.RequiresCleanroom ?? false,
                    _config.EraOnlyMachines.Contains(machine))
                {
                    Catalysts = catalysts,
                    Grid = cellRefs is null ? null : GridOf(cellRefs, variantInputs, choices.Count),
                });
            }
        }

        return result;
    }

    /// <summary>Byproduct slots open by machine tier, so a recipe becomes one variant per unlocked tier.</summary>
    private static IEnumerable<(string Id, int Tier, List<PlannerOutput> Outputs)> Variants(
        string id, int tier, List<(PlannerOutput Output, long Slot)> outputs, IReadOnlyList<int>? slotTiers)
    {
        if (slotTiers is null || outputs.All(o => o.Slot == 0))
        {
            yield return (id, tier, outputs.Select(o => o.Output).ToList());
            yield break;
        }

        int SlotTier(long slot) => slot == 0 ? 0 : slotTiers[(int)Math.Min(slot, slotTiers.Count) - 1];

        var thresholds = outputs.Select(o => SlotTier(o.Slot)).Distinct().Order().ToList();
        foreach (var threshold in thresholds)
        {
            var unlocked = outputs.Where(o => SlotTier(o.Slot) <= threshold).Select(o => o.Output).ToList();
            yield return (
                threshold == thresholds[0] ? id : $"{id}~b{threshold}",
                Math.Max(tier, threshold),
                unlocked);
        }
    }

    /// <summary>Voltage suffix stripped first, so one configured rename covers every tiered
    /// variant of a map.</summary>
    private string NormalizeMachine(string type)
    {
        var stripped = TierSuffix().Replace(type, "");
        return _config.MachineRenames.TryGetValue(stripped, out var renamed) ? renamed : stripped;
    }

    private bool IsRecycling(string category) =>
        _config.RecyclingCategorySuffixes.Any(suffix => category.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether an ingredient is one shape of a single material rather than something
    /// manufactured, by GT's own prefix flags. Fluids qualify: a molten metal is already just
    /// its material.</summary>
    private static bool IsMaterialShape(string id, Dump dump, UnifiedItems unified)
    {
        if (dump.Fluids.ContainsKey(id))
        {
            return true;
        }

        var oredicts = unified.OredictsByCanonical.GetValueOrDefault(id);
        return oredicts is not null && oredicts.Any(dump.OrePrefixes.IsMaterialShape);
    }

    private bool IsExcluded(string machine) =>
        _config.ExcludedMachines.Contains(machine);

    /// <summary>Every member of an input slot, flagged as a catalyst when the recipe does not
    /// consume it: a zero-size stack by the dump's own mark, or a tool that crafts into its
    /// own worn self by Forge's container-item data, whatever mod it comes from. One catalyst
    /// still condemns the whole slot: its members are alternatives for the same role. Each
    /// member also says whether it is such a wearing tool, which the solver reads only to
    /// break exact ties.</summary>
    private static (List<(string ItemId, long Amount, bool Tool)> Members, bool Catalyst) ResolveSlot(
        Dump dump, UnifiedItems unified, ToolIndex tools, string groupId)
    {
        if (!dump.GroupStacks.TryGetValue(groupId, out var stacks))
        {
            return ([], false);
        }

        var catalyst = false;
        var members = new List<(string ItemId, long Amount, bool Tool)>();
        foreach (var stack in stacks.OrderBy(stack => unified.Canonical(stack.ItemId), StringComparer.Ordinal))
        {
            var canonical = unified.Canonical(stack.ItemId);
            var tool = tools.IsTool(stack.ItemId) || tools.IsTool(canonical);
            if (stack.Size <= 0 || tool)
            {
                catalyst = true;
            }
            members.Add((canonical, Math.Max(1, stack.Size), tool));
        }
        return (members, catalyst);
    }

    /// <summary>What one grid cell turned into: a flat ingredient (by canonical id), the n-th
    /// choice slot, or the n-th catalyst slot.</summary>
    private sealed record CellRef(int Cell, string? Item, int? Choice, int? Catalyst);

    /// <summary>The shape over the final slots: flat ingredients take the slot numbers of their
    /// position among the inputs, choices and catalysts follow in order — the same numbering the
    /// artifact writer uses. A cell whose ingredient netting removed has no slot, and the recipe
    /// then ships no shape rather than a wrong one.</summary>
    private static IReadOnlyList<PlannerGridCell>? GridOf(
        List<CellRef> cellRefs, Dictionary<string, long> inputs, int choiceCount)
    {
        var slotOfInput = new Dictionary<string, int>();
        foreach (var key in inputs.Keys)
        {
            slotOfInput[key] = slotOfInput.Count;
        }
        var cells = new List<PlannerGridCell>(cellRefs.Count);
        foreach (var cellRef in cellRefs)
        {
            if (cellRef.Cell is < 0 or > 8)
            {
                continue;
            }
            int slot;
            if (cellRef.Item is not null)
            {
                if (!slotOfInput.TryGetValue(cellRef.Item, out slot))
                {
                    return null;
                }
            }
            else if (cellRef.Choice is { } choice)
            {
                slot = inputs.Count + choice;
            }
            else
            {
                slot = inputs.Count + choiceCount + cellRef.Catalyst!.Value;
            }
            cells.Add(new PlannerGridCell(cellRef.Cell, slot));
        }
        return cells;
    }

    private static IEnumerable<(string ItemId, long Amount)> Decompose(Dump dump, string itemId, long amount)
    {
        if (dump.ContainersByItemId.TryGetValue(itemId, out var container))
        {
            yield return (container.EmptyItemId, amount);
            yield return (container.FluidId, amount * container.Amount);
        }
        else
        {
            yield return (itemId, amount);
        }
    }

    private static List<PlannerOutput> Merge(List<PlannerOutput> outputs) =>
        outputs
            .GroupBy(o => (o.ItemId, o.Chance))
            .Select(g => new PlannerOutput(g.Key.ItemId, g.Sum(o => o.Amount), g.Key.Chance))
            .ToList();

    /// <summary>Nets items appearing on both sides, e.g. returned empty containers.</summary>
    private static void Net(Dictionary<string, long> inputs, List<PlannerOutput> outputs)
    {
        for (var i = outputs.Count - 1; i >= 0; i--)
        {
            var o = outputs[i];
            if (o.Chance < 1.0)
            {
                continue;
            }
            if (!inputs.TryGetValue(o.ItemId, out var inAmount))
            {
                continue;
            }

            var netted = Math.Min(inAmount, o.Amount);
            if (inAmount == netted)
            {
                inputs.Remove(o.ItemId);
            }
            else
            {
                inputs[o.ItemId] = inAmount - netted;
            }

            if (o.Amount == netted)
            {
                outputs.RemoveAt(i);
            }
            else
            {
                outputs[i] = o with { Amount = o.Amount - netted };
            }
        }
    }
}
