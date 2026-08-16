namespace Craftiger.Solver.Models;

/// <summary>Form preference between routes that genuinely tie: recycling shape-shuffles enter
/// the economy as dust, locking every form of a material to the same price, so ties are
/// resolved after the solve by rerouting toward the best-ranked form (§5). The priority
/// list runs best to worst; unlisted classes and non-leaf inputs rank best.</summary>
public sealed record SolverPreferences(IReadOnlyList<string> LeafClassPriority)
{
    public int Rank(string? leafClass)
    {
        if (leafClass is null)
        {
            return 0;
        }
        for (var i = 0; i < LeafClassPriority.Count; i++)
        {
            if (LeafClassPriority[i] == leafClass)
            {
                return i;
            }
        }
        return 0;
    }
}
