namespace Craftiger.Solver.Models;

/// <summary>One lexicographic layer: sparse column coefficients, direction, and the slack the
/// next layer may trade within (<c>max(AbsTolerance, RelTolerance · |optimum|)</c>).</summary>
public sealed record LpObjective(
    bool Maximize,
    IReadOnlyList<LpEntry> Coefficients,
    double AbsTolerance = 1e-9,
    double RelTolerance = 1e-9);
