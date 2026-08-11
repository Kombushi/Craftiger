using System.Text.RegularExpressions;

namespace Gtnh.Planner.Builder;

public sealed record PlannerRecipe(
    string Id,
    string Machine,
    int Tier,
    int? Heat,
    long DurationTicks,
    long EuT,
    Dictionary<string, long> Inputs,
    List<PlannerOutput> Outputs,
    IReadOnlyList<string> MachineItemIds,
    IReadOnlyList<IReadOnlyList<string>> InputSlotAlternatives);

public sealed record PlannerOutput(string ItemId, long Amount, double Chance);

/// <summary>Flattens dump recipes into planner recipes over canonical items.</summary>
public static partial class RecipeTransform
{
    [GeneratedRegex(@" \((ULV|LV|MV|HV|EV|IV|LuV|ZPM|UV|UHV|UEV|UIV|UMV|UXV|MAX)\)$")]
    private static partial Regex TierSuffix();

    public static List<PlannerRecipe> Run(Dump dump, UnifiedItems unified, BuilderConfig config)
    {
        var result = new List<PlannerRecipe>();

        // Machine items gate an era only when they exist as real craftable items.
        var machinesByTypeId = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var (typeId, icons) in dump.HandlerItemsByRecipeTypeId)
            machinesByTypeId[typeId] = icons
                .Where(dump.Items.ContainsKey)
                .Select(unified.Canonical)
                .Distinct()
                .ToList();

        var ungatedTypeIds = new HashSet<string>(
            dump.Recipes.Where(r => r.Category == "minecraft").Select(r => r.RecipeTypeId));

        foreach (var recipe in dump.Recipes)
        {
            var machine = NormalizeMachine(recipe.Machine, config);
            if (IsExcluded(machine, config)) continue;

            var gt = dump.GtByRecipeId.GetValueOrDefault(recipe.Id);
            var tier = gt is null || gt.Voltage <= 0 ? 0 : LabelTier(gt.TierLabel) ?? VoltageTier(gt.Voltage);
            if (tier > 0 && IsMultiAmp(recipe, machine, config)) tier = Math.Max(1, tier - 1);

            var inputs = new Dictionary<string, long>();
            var slots = new List<IReadOnlyList<string>>();
            foreach (var (_, groupId) in dump.ItemInputsByRecipe.GetValueOrDefault(recipe.Id) ?? [])
            {
                var stack = ResolveSlot(dump, unified, config, groupId);
                if (stack is null) continue;
                foreach (var (partId, partAmount) in Decompose(dump, stack.Value.ItemId, stack.Value.Amount))
                    inputs[unified.Canonical(partId)] = inputs.GetValueOrDefault(unified.Canonical(partId)) + partAmount;

                // Single-alternative slots decompose like the flat inputs; genuine
                // alternative lists keep their members whole.
                var stacks = dump.GroupStacks[groupId];
                if (stacks.Count == 1)
                {
                    foreach (var (partId, _) in Decompose(dump, stacks[0].ItemId, 1))
                        slots.Add([unified.Canonical(partId)]);
                }
                else
                {
                    slots.Add(stacks.Select(s => unified.Canonical(s.ItemId)).Distinct().ToList());
                }
            }
            foreach (var fluid in dump.FluidInputsByRecipe.GetValueOrDefault(recipe.Id) ?? [])
            {
                if (fluid.Amount <= 0) continue;
                inputs[fluid.FluidId] = inputs.GetValueOrDefault(fluid.FluidId) + fluid.Amount;
                slots.Add([fluid.FluidId]);
            }

            var outputs = new List<PlannerOutput>();
            foreach (var o in dump.ItemOutputsByRecipe.GetValueOrDefault(recipe.Id) ?? [])
            {
                if (o.Size <= 0 || o.Chance <= 0) continue;
                foreach (var (partId, partAmount) in Decompose(dump, o.ItemId, o.Size))
                    outputs.Add(new PlannerOutput(unified.Canonical(partId), partAmount, Math.Min(o.Chance, 1.0)));
            }
            foreach (var o in dump.FluidOutputsByRecipe.GetValueOrDefault(recipe.Id) ?? [])
            {
                if (o.Amount <= 0 || o.Chance <= 0) continue;
                outputs.Add(new PlannerOutput(o.FluidId, o.Amount, Math.Min(o.Chance, 1.0)));
            }
            outputs = Merge(outputs);

            Net(inputs, outputs);
            if (outputs.Count == 0 || inputs.Count == 0) continue;

            result.Add(new PlannerRecipe(
                recipe.Id, machine, tier, gt?.Heat,
                gt?.Duration ?? 0, gt?.Voltage ?? 0,
                inputs, outputs,
                ungatedTypeIds.Contains(recipe.RecipeTypeId)
                    ? []
                    : machinesByTypeId.GetValueOrDefault(recipe.RecipeTypeId) ?? [],
                slots));
        }

        return result;
    }

    /// <summary>Two 2A hatches run recipes one tier above themselves on any multiblock.</summary>
    private static bool IsMultiAmp(DumpRecipe recipe, string machine, BuilderConfig config)
    {
        if (config.ForceSingleAmp.Contains(machine)) return false;
        if (config.ForceMultiAmp.Contains(machine)) return true;
        return recipe.Category == "gregtech" && recipe.HandlerIcons <= config.MultiblockMaxHandlers;
    }

    private static readonly Dictionary<string, int> LabelTiers = new()
    {
        ["ULV"] = 1, ["LV"] = 1, ["MV"] = 2, ["HV"] = 3, ["EV"] = 4, ["IV"] = 5,
        ["LuV"] = 6, ["ZPM"] = 7, ["UV"] = 8, ["UHV"] = 9, ["UEV"] = 10,
        ["UIV"] = 11, ["UMV"] = 12, ["UXV"] = 13, ["MAX"] = 14
    };

    /// <summary>GT's own per-recipe tier label; it already accounts for machine amperage.</summary>
    public static int? LabelTier(string? label) =>
        label is not null && LabelTiers.TryGetValue(label, out var tier) ? tier : null;

    /// <summary>Fallback when the dump carries no tier label.</summary>
    public static int VoltageTier(long euT)
    {
        if (euT <= 0) return 0;
        var tier = 1;
        long cap = 32;
        while (euT > cap)
        {
            tier++;
            cap *= 4;
        }
        return tier;
    }

    public static string NormalizeMachine(string type, BuilderConfig config) =>
        config.MachineRenames.TryGetValue(type, out var renamed) ? renamed : TierSuffix().Replace(type, "");

    private static bool IsExcluded(string machine, BuilderConfig config) =>
        config.ExcludedMachines.Contains(machine) ||
        config.ExcludedMachineSuffixes.Any(s => machine.EndsWith(s, StringComparison.Ordinal));

    private static (string ItemId, long Amount)? ResolveSlot(
        Dump dump, UnifiedItems unified, BuilderConfig config, string groupId)
    {
        if (!dump.GroupStacks.TryGetValue(groupId, out var stacks)) return null;

        (string ItemId, long Amount)? best = null;
        foreach (var stack in stacks)
        {
            if (stack.Size <= 0) return null;
            var canonical = unified.Canonical(stack.ItemId);
            if (IsCatalyst(stack.ItemId, config) || IsCatalyst(canonical, config)) return null;
            if (best is null || string.CompareOrdinal(canonical, best.Value.ItemId) < 0)
                best = (canonical, stack.Size);
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

    private static bool IsCatalyst(string itemId, BuilderConfig config) =>
        config.CatalystItemIdPrefixes.Any(p => itemId.StartsWith(p, StringComparison.Ordinal));

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
            if (o.Chance < 1.0) continue;
            if (!inputs.TryGetValue(o.ItemId, out var inAmount)) continue;

            var netted = Math.Min(inAmount, o.Amount);
            if (inAmount == netted) inputs.Remove(o.ItemId);
            else inputs[o.ItemId] = inAmount - netted;

            if (o.Amount == netted) outputs.RemoveAt(i);
            else outputs[i] = o with { Amount = o.Amount - netted };
        }
    }
}
