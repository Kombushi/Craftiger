namespace Craftiger.Builder.Models.Options;

/// <summary>A combustion engine multiblock: nominal EU/t, the booster gas tripling it, and the lubricant it sips.</summary>
public sealed record EngineOverlay
{
    public required string ItemId { get; init; }

    public required long NominalEuT { get; init; }

    public required string BoosterFluidId { get; init; }

    /// <summary>Booster liters per second while boosted.</summary>
    public required double BoosterPerSecond { get; init; }

    /// <summary>Output multiplier while boosted.</summary>
    public required double BoostFactor { get; init; }

    public required string LubricantFluidId { get; init; }

    /// <summary>Lubricant liters per second unboosted; boosting doubles it.</summary>
    public required double LubricantPerSecond { get; init; }
}
