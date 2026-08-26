namespace Craftiger.Solver.Models.Options;

/// <summary>Form preference between routes that tie exactly: leaf classes best to worst; unlisted classes and non-leaves rank best.</summary>
public sealed record SolverPreferences
{
    public IReadOnlyList<string> LeafClassPriority { get; init; } = [];
}
