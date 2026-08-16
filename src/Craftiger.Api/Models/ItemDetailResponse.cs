namespace Craftiger.Api.Models;

public sealed record ItemDetailResponse(
    string ItemId, string Name, long AtlasIdx, string? LeafClass, double? Cost,
    IReadOnlyList<RecipeDto> Recipes);
