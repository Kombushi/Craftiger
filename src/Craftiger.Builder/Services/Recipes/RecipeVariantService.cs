using Craftiger.Builder.Interfaces.Recipes;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Services.Recipes;

/// <summary>Byproduct slots open by machine tier, so a recipe becomes one variant per unlocked tier.</summary>
public sealed class RecipeVariantService : IRecipeVariantService
{
    public IEnumerable<RecipeVariant> Variants(
        string id, int tier, IReadOnlyList<SlotOutput> outputs, IReadOnlyList<int>? slotTiers)
    {
        if (slotTiers is null || outputs.All(o => o.Slot == 0))
        {
            yield return new RecipeVariant(id, tier, outputs.Select(o => o.Output).ToList());
            yield break;
        }

        int SlotTier(long slot) => slot == 0 ? 0 : slotTiers[(int)Math.Min(slot, slotTiers.Count) - 1];

        var thresholds = outputs.Select(o => SlotTier(o.Slot)).Distinct().Order().ToList();
        foreach (var threshold in thresholds)
        {
            var unlocked = outputs.Where(o => SlotTier(o.Slot) <= threshold).Select(o => o.Output).ToList();
            yield return new RecipeVariant(
                threshold == thresholds[0] ? id : $"{id}~b{threshold}",
                Math.Max(tier, threshold),
                unlocked);
        }
    }
}
