namespace Craftiger.Solver.Models.Options;

/// <summary>Tuning of the BOM walk.</summary>
public sealed record BomOptions
{
    /// <summary>A loop whose remaining gain is this close to one feeds itself for free and has no finite plan.</summary>
    public double PivotEpsilon { get; init; } = 1e-12;

    /// <summary>The whole-run fixpoint of a loop is monotone and bounded; this only guards a broken system.</summary>
    public int MaxWholeRounds { get; init; } = 100_000;
}
