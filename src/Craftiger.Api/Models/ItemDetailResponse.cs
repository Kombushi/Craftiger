namespace Craftiger.Api.Models;

/// <summary>BestRecipeId is the solver's pick the BOM walk expands unless pinned; null for leaves priced at their weight and for unreachable items.</summary>
public sealed record ItemDetailResponse(
    string ItemId,
    string Name,
    long AtlasIdx,
    string? LeafClass,
    double? Cost,
    bool Uncraftable,
    string? BestRecipeId,
    IReadOnlyList<RecipeDto> Recipes,
    IReadOnlyDictionary<string, ItemRefDto> Items);
