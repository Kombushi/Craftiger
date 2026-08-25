using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces.Recipes;

/// <summary>Splits a recipe into one variant per machine tier that unlocks another byproduct slot.</summary>
public interface IRecipeVariantService
{
    IEnumerable<RecipeVariant> Variants(
        string id, int tier, IReadOnlyList<SlotOutput> outputs, IReadOnlyList<int>? slotTiers);
}
