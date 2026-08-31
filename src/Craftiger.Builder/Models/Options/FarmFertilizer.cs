namespace Craftiger.Builder.Models.Options;

/// <summary>One registered fertilizer item and the potency a single unit carries.</summary>
public sealed record FarmFertilizer
{
    public required string ItemId { get; init; }

    public required int Potency { get; init; }
}
