namespace Craftiger.Builder.Models.Options;

/// <summary>A fluid the world hands over, and what a millibucket of it costs.</summary>
public sealed record WorldFluid
{
    public required double Weight { get; init; }

    /// <summary>The era it is free at; unset for a drilled fluid, whose own pump recipe decides.</summary>
    public int? Era { get; init; }
}
