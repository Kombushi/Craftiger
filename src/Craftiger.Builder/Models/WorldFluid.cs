namespace Craftiger.Builder.Models;

/// <summary>A fluid the world hands over, and what a millibucket of it costs.</summary>
public sealed record WorldFluid
{
    public required double Weight { get; init; }

    /// <summary>The era it is free at. Left unset for a drilled fluid, whose own pump
    /// recipe decides when it arrives.</summary>
    public int? Era { get; init; }
}
