namespace Craftiger.Api.Models;

/// <summary>A producing recipe in the item detail view with its candidate cost for the item.
/// <paramref name="Chosen"/> names the alternative each slot resolves to for this item.
/// Catalyst slots list the tools the recipe needs in place but never consumes.</summary>
public sealed record RecipeDto(
    string RecipeId,
    string Machine,
    int Tier,
    int? MultiTier,
    int? Heat,
    long DurationTicks,
    long EuT,
    double? CandidateCost,
    IReadOnlyList<IReadOnlyList<SlotAlternativeDto>> Slots,
    IReadOnlyList<string> Chosen,
    IReadOnlyList<IReadOnlyList<SlotAlternativeDto>> Catalysts,
    IReadOnlyList<OutputDto> Outputs);
