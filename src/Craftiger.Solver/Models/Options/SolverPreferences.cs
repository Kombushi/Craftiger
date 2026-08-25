namespace Craftiger.Solver.Models.Options;

/// <summary>Form preference between routes that tie exactly: leaf classes best to worst; unlisted classes and non-leaves rank best.</summary>
public sealed record SolverPreferences
{
    public IReadOnlyList<string> LeafClassPriority { get; init; } = [];

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
