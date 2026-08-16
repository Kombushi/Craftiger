namespace Craftiger.Solver.Models;

/// <summary>Form preference between routes that genuinely tie: recycling shape-shuffles enter
/// the economy as dust, locking every form of a material to the same price, so ties are
/// resolved after the solve by rerouting toward producers that consume solid forms (§5).</summary>
public sealed record SolverPreferences(IReadOnlySet<string> DeprioritizedLeafClasses)
{
    public bool Deprioritizes(string? leafClass) =>
        leafClass is not null && DeprioritizedLeafClasses.Contains(leafClass);
}
