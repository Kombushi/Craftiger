namespace Craftiger.Api.Models;

/// <summary><paramref name="BestRecipeId"/> is the solver's current pick — what the BOM walk
/// expands unless a pin overrides it; null for leaves priced at their weight and for
/// unreachable items.</summary>
public sealed record ItemDetailResponse(
    string ItemId, string Name, long AtlasIdx, string? LeafClass, double? Cost,
    string? BestRecipeId, IReadOnlyList<RecipeDto> Recipes);
