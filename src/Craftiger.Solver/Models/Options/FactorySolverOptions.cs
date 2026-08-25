namespace Craftiger.Solver.Models.Options;

/// <summary>Tuning of the factory solve: candidate pruning bands and layer numerics.</summary>
public sealed record FactorySolverOptions
{
    /// <summary>A candidate recipe survives when some output prices within this factor of the item's solved cost.</summary>
    public double PruneFactor { get; init; } = 4.0;

    /// <summary>Added to the band so cheap recipes for items priced near zero survive.</summary>
    public double PruneFloor { get; init; } = 1.0;

    /// <summary>The generator band's floor, in fuel weight per net EU/t; competitive fuel chains price near zero.</summary>
    public double GeneratorPruneFloor { get; init; } = 1e-3;

    /// <summary>Below this a rate is layer-tolerance noise, not flow.</summary>
    public double RateEpsilon { get; init; } = 1e-5;

    /// <summary>Each layer's optimum binds the next within this relative corridor; tighter ones broke the simplex numerics.</summary>
    public double LayerTolerance { get; init; } = 1e-3;
}
