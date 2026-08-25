namespace Craftiger.Solver.Highs.Models;

/// <summary>Tuning of the HiGHS adapter's numerics.</summary>
public sealed record HighsOptions
{
    /// <summary>Iterations of geometric-mean equilibration before the model reaches HiGHS.</summary>
    public int EquilibrationPasses { get; init; } = 3;

    /// <summary>A support-restricted layer's box floors every nonzero side here: presolve treats smaller bounds as zero and re-fixes the dust columns.</summary>
    public double DustFloor { get; init; } = 1e-6;

    /// <summary>Relative padding a lock row is widened by to contain the standing point.</summary>
    public double LockPad { get; init; } = 1e-9;

    /// <summary>Streams HiGHS's own log to stdout — a diagnostics aid, never set in production.</summary>
    public bool Verbose { get; init; }
}
