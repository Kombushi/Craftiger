using System.Text.RegularExpressions;
using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

public sealed partial class RecipeTransformService(IOptions<BuilderConfig> options) : IRecipeTransformService
{
    private readonly BuilderConfig _config = options.Value;

    [GeneratedRegex(@" \((ULV|LV|MV|HV|EV|IV|LuV|ZPM|UV|UHV|UEV|UIV|UMV|UXV|MAX)\)$")]
    private static partial Regex TierSuffix();

    public List<PlannerRecipe> Run(Dump dump, UnifiedItems unified)
    {
        var result = new List<PlannerRecipe>();

        // Machine items gate an era only when they exist as real craftable items.
        var machinesByTypeId = new Dictionary<string, IReadOnlyList<RecipeMachine>>();
        foreach (var (typeId, icons) in dump.HandlerItemsByRecipeTypeId)
        {
            machinesByTypeId[typeId] = icons
                .Where(dump.Items.ContainsKey)
                .Select(unified.Canonical)
                .Distinct()
                .Select(id => new RecipeMachine(id, Multiblock: false, Tier: null))
                .ToList();
        }
        // GregTech maps name their real machines, which the NEI handler icons only approximate.
        foreach (var (typeId, map) in dump.RecipeMapByTypeId)
        {
            var machines = map.Machines
                .Where(m => dump.Items.ContainsKey(m.ItemId))
                .GroupBy(m => unified.Canonical(m.ItemId))
                .Select(g => new RecipeMachine(g.Key, g.Any(m => m.Multiblock), g.Min(m => m.Tier)))
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
            if (_config.PhantomRecipeIds.ContainsKey(recipe.Id))
            {
                continue;
            }

            var gt = dump.GtByRecipeId.GetValueOrDefault(recipe.Id);
            var tier = gt is null || gt.Voltage <= 0 || gt.Voltage == _config.WirelessSentinelVoltage
                ? 0
                : TierLadder.LabelTier(gt.TierLabel) ?? TierLadder.VoltageTier(gt.Voltage);

            var inputs = new Dictionary<string, long>();
            var choices = new List<PlannerChoice>();
            var slots = new List<IReadOnlyList<string>>();
            foreach (var (_, groupId) in dump.ItemInputsByRecipe.GetValueOrDefault(recipe.Id) ?? [])
            {
                var members = ResolveSlot(dump, unified, groupId);
                if (members.Count == 0)
                {
                    continue;
                }

                var alternatives = members.DistinctBy(member => member.ItemId).ToList();
                if (alternatives.Count > 1)
                {
                    // A real choice of ingredient: ship every option at its own amount so
                    // the solver can take the cheapest, rather than freezing one at build time.
                    choices.Add(new PlannerChoice(alternatives));
                    slots.Add(alternatives.Select(member => member.ItemId).ToList());
                    continue;
                }

                foreach (var (partId, partAmount) in Decompose(dump, members[0].ItemId, members[0].Amount))
                {
                    inputs[unified.Canonical(partId)] = inputs.GetValueOrDefault(unified.Canonical(partId)) + partAmount;
                }

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
            if (ingredients.Any(id => _config.ExcludedInputItems.Contains(dump.NameOf(id))))
            {
                continue;
            }
            if (gt is not null && IsRecycling(gt.Category) && !ingredients.All(id => IsMaterialShape(id, dump, unified)))
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
                    _config.EraOnlyMachines.Contains(machine)));
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

    private string NormalizeMachine(string type) =>
        _config.MachineRenames.TryGetValue(type, out var renamed) ? renamed : TierSuffix().Replace(type, "");

    private bool IsRecycling(string category) =>
        _config.RecyclingCategorySuffixes.Any(suffix => category.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether an ingredient is one shape of a single material rather than something
    /// manufactured. Fluids qualify: a molten metal is already just its material.</summary>
    private bool IsMaterialShape(string id, Dump dump, UnifiedItems unified)
    {
        if (dump.Fluids.ContainsKey(id))
        {
            return true;
        }

        var oredicts = unified.OredictsByCanonical.GetValueOrDefault(id);
        return oredicts is not null && oredicts
            .Any(oredict => _config.MaterialShapeOredictPrefixes
                .Any(prefix => oredict.StartsWith(prefix, StringComparison.Ordinal)));
    }

    private bool IsExcluded(string machine) =>
        _config.ExcludedMachines.Contains(machine) ||
        _config.ExcludedMachineSuffixes.Any(s => machine.EndsWith(s, StringComparison.Ordinal)) ||
        _config.ExcludedMachinePrefixes.Any(p => machine.StartsWith(p, StringComparison.Ordinal));

    /// <summary>Every member of an input slot the recipe really consumes, or nothing when the
    /// slot is a catalyst. One catalyst condemns the whole slot on purpose: a tool slot lists
    /// every mod's version of that tool, and the prefix list only recognises GregTech's, so
    /// judging members one by one would leave the third-party tools priced as ingredients.</summary>
    private List<(string ItemId, long Amount)> ResolveSlot(Dump dump, UnifiedItems unified, string groupId)
    {
        if (!dump.GroupStacks.TryGetValue(groupId, out var stacks))
        {
            return [];
        }

        var members = new List<(string ItemId, long Amount)>();
        foreach (var stack in stacks.OrderBy(stack => unified.Canonical(stack.ItemId), StringComparer.Ordinal))
        {
            var canonical = unified.Canonical(stack.ItemId);
            if (stack.Size <= 0 || IsCatalyst(stack.ItemId) || IsCatalyst(canonical))
            {
                return [];
            }
            members.Add((canonical, stack.Size));
        }
        return members;
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

    private bool IsCatalyst(string itemId) =>
        _config.CatalystItemIdPrefixes.Any(p => itemId.StartsWith(p, StringComparison.Ordinal));

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
