namespace Craftiger.Solver.Models.Lp;

/// <summary>One lexicographic layer; the next layer may trade within max(AbsTolerance, RelTolerance · |optimum|).</summary>
public sealed record LpObjective(
    bool Maximize,
    IReadOnlyList<LpEntry> Coefficients,
    double AbsTolerance = 1e-6,
    double RelTolerance = 1e-6,
    bool SupportRestricted = false)
{
    /// <summary>The slack the layers below this one may spend once it is optimized.</summary>
    public double Slack(double optimum) => Math.Max(AbsTolerance, RelTolerance * Math.Abs(optimum));
}
