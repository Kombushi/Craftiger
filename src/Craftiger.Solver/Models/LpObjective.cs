namespace Craftiger.Solver.Models;

/// <summary>One lexicographic layer: sparse column coefficients, direction, and the slack the
/// next layer may trade within (<c>max(AbsTolerance, RelTolerance · |optimum|)</c>).
/// <paramref name="SupportRestricted"/> solves the layer with every column currently at zero
/// fixed to zero — for canonicalization-style tie-breaking layers, where cleaning the standing
/// solution is the point and reopening the full column space is prohibitively slow.
public sealed record LpObjective(
    bool Maximize,
    IReadOnlyList<LpEntry> Coefficients,
    double AbsTolerance = 1e-6,
    double RelTolerance = 1e-6,
    bool SupportRestricted = false);
