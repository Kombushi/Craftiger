namespace Craftiger.Solver.Models;

/// <summary>Product facts about machines the recipe graph cannot express: the machines every
/// garage owns at tier 0, the maps whose heat requirement is waived when owned (§9), and the
/// maps whose heat capacity grows with the energy hatch tier.</summary>
public sealed record GarageRules(
    IReadOnlySet<string> AlwaysOwnedMachines,
    IReadOnlySet<string> HeatExemptMachines,
    IReadOnlySet<string> HeatBonusMachines);
