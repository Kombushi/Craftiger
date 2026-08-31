namespace Craftiger.Builder.Models.Options;

/// <summary>One reactor consumable: the fluid, its drain per second, and the factor it applies.</summary>
public sealed record ReactorMode
{
    public required string FluidId { get; init; }

    public required double PerSecond { get; init; }

    public required double Factor { get; init; }
}
