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

            var gt = dump.GtByRecipeId.GetValueOrDefault(recipe.Id);
            var tier = gt is null || gt.Voltage <= 0 ? 0 : TierLadder.LabelTier(gt.TierLabel) ?? TierLadder.VoltageTier(gt.Voltage);

            var inputs = new Dictionary<string, long>();
            var slots = new List<IReadOnlyList<string>>();
            foreach (var (_, groupId) in dump.ItemInputsByRecipe.GetValueOrDefault(recipe.Id) ?? [])
            {
                var stack = ResolveSlot(dump, unified, groupId);
                if (stack is null)
                {
                    continue;
                }
                foreach (var (partId, partAmount) in Decompose(dump, stack.Value.ItemId, stack.Value.Amount))
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
                    slots.Add(stacks.Select(s => unified.Canonical(s.ItemId)).Distinct().ToList());
                }
            }
            foreach (var fluid in dump.FluidInputsByRecipe.GetValueOrDefault(recipe.Id) ?? [])
            {
                if (fluid.Amount <= 0)
                {
                    continue;
                }
                inputs[fluid.FluidId] = inputs.GetValueOrDefault(fluid.FluidId) + fluid.Amount;
                slots.Add([fluid.FluidId]);
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
            if (inputs.Keys.Any(id => _config.ExcludedInputItems.Contains(dump.NameOf(id))))
            {
                continue;
            }

            var slotTiers = _config.ByproductSlotTiers.GetValueOrDefault(machine);
            foreach (var (variantId, variantTier, variantOutputs) in Variants(recipe.Id, tier, outputs, slotTiers))
            {
                var variantInputs = new Dictionary<string, long>(inputs);
                var merged = Merge(variantOutputs);
                Net(variantInputs, merged);
                if (merged.Count == 0 || variantInputs.Count == 0)
                {
                    continue;
                }

                result.Add(new PlannerRecipe(
                    variantId, machine, variantTier, gt?.Heat,
                    gt?.Duration ?? 0, gt?.Voltage ?? 0,
                    variantInputs, merged,
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

    private bool IsExcluded(string machine) =>
        _config.ExcludedMachines.Contains(machine) ||
        _config.ExcludedMachineSuffixes.Any(s => machine.EndsWith(s, StringComparison.Ordinal)) ||
        _config.ExcludedMachinePrefixes.Any(p => machine.StartsWith(p, StringComparison.Ordinal));

    private (string ItemId, long Amount)? ResolveSlot(Dump dump, UnifiedItems unified, string groupId)
    {
        if (!dump.GroupStacks.TryGetValue(groupId, out var stacks))
        {
            return null;
        }

        (string ItemId, long Amount)? best = null;
        foreach (var stack in stacks)
        {
            if (stack.Size <= 0)
            {
                return null;
            }
            var canonical = unified.Canonical(stack.ItemId);
            if (IsCatalyst(stack.ItemId) || IsCatalyst(canonical))
            {
                return null;
            }
            if (best is null || string.CompareOrdinal(canonical, best.Value.ItemId) < 0)
            {
                best = (canonical, stack.Size);
            }
        }
        return best;
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
