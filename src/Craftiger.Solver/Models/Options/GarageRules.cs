namespace Craftiger.Solver.Models.Options;

/// <summary>Product facts about machines the recipe graph cannot express.</summary>
public sealed record GarageRules
{
    /// <summary>Machines every garage owns at tier 0; a None override cannot disown them.</summary>
    public IReadOnlyList<string> AlwaysOwnedMachines { get; init; } = [];

    /// <summary>Maps whose heat requirement is waived once the machine is owned.</summary>
    public IReadOnlyList<string> HeatExemptMachines { get; init; } = [];

    /// <summary>Maps whose heat capacity grows with the energy hatch tier.</summary>
    public IReadOnlyList<string> HeatBonusMachines { get; init; } = [];

    public bool IsAlwaysOwned(string machine) => AlwaysOwnedMachines.Contains(machine);

    public bool IsHeatExempt(string machine) => HeatExemptMachines.Contains(machine);

    public bool HasHeatBonus(string machine) => HeatBonusMachines.Contains(machine);
}
