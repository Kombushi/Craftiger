namespace Craftiger.Api.Models;

/// <summary>A producing recipe in the item detail with its candidate cost, the alternative each slot resolves to, its catalyst slots, and the shaped grid as nine cells indexing Slots then Catalysts (null without a shape).</summary>
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
    IReadOnlyList<OutputDto> Outputs,
    IReadOnlyList<int?>? Grid);
