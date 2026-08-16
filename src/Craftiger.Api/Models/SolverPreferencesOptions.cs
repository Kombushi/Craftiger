using Craftiger.Solver.Models;

namespace Craftiger.Api.Models;

/// <summary>Configured form preference: leaf classes that lose exact-cost ties (spec §5).</summary>
public sealed class SolverPreferencesOptions
{
    public IReadOnlyList<string> DeprioritizedLeafClasses { get; init; } = [];

    public SolverPreferences ToPreferences() => new(DeprioritizedLeafClasses.ToHashSet());
}
