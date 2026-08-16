using Craftiger.Solver.Models;

namespace Craftiger.Api.Models;

/// <summary>Configured form preference: leaf classes ranked best to worst for resolving
/// exact-cost ties (spec §5).</summary>
public sealed class SolverPreferencesOptions
{
    public IReadOnlyList<string> LeafClassPriority { get; init; } = [];

    public SolverPreferences ToPreferences() => new(LeafClassPriority);
}
