using Craftiger.Solver.Models;

namespace Craftiger.Api.Models;

/// <summary>Configured product facts the recipe graph cannot express (spec §2, §9).</summary>
public sealed class GarageRulesOptions
{
    public IReadOnlyList<string> AlwaysOwnedMachines { get; init; } = [];

    public IReadOnlyList<string> HeatExemptMachines { get; init; } = [];

    public IReadOnlyList<string> HeatBonusMachines { get; init; } = [];

    public GarageRules ToRules() => new(
        AlwaysOwnedMachines.ToHashSet(),
        HeatExemptMachines.ToHashSet(),
        HeatBonusMachines.ToHashSet());
}
