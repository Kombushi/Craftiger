namespace Craftiger.Builder.Models.Options;

/// <summary>One tool the Tree Growth Simulator accepts for a mode, with the factor it multiplies that mode's output by.</summary>
public sealed record TreeFarmTool
{
    public required string ItemId { get; init; }

    public required int Multiplier { get; init; }
}
