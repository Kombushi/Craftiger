namespace Craftiger.Builder.Models.Options;

/// <summary>A reactor multiblock whose timed fuels are multiplied by coolant and excited-liquid flows on top of a fixed upkeep.</summary>
public sealed record ReactorOverlay
{
    public required string ItemId { get; init; }

    public required string UpkeepFluidId { get; init; }

    public required double UpkeepPerSecond { get; init; }

    /// <summary>Coolant choices: each multiplies output alone.</summary>
    public IReadOnlyList<ReactorMode> Coolants { get; init; } = [];

    /// <summary>Excited-liquid choices: each multiplies output and fuel together.</summary>
    public IReadOnlyList<ReactorMode> ExcitedLiquids { get; init; } = [];
}
