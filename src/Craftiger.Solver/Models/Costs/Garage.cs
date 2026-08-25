namespace Craftiger.Solver.Models.Costs;

/// <summary>The user's machine garage: a tier override of null means the machine is not owned; BuiltMultiblocks unlocks the lower recipe tier of mixed maps; CoilHeat is the installed coil's max heat per heat-gated map.</summary>
public sealed record Garage(
    int DefaultTier,
    IReadOnlyDictionary<string, int?> MachineTiers,
    IReadOnlySet<string> BuiltMultiblocks,
    IReadOnlyDictionary<string, int> CoilHeat)
{
    public bool TryGetOverride(string machine, out int? tier) => MachineTiers.TryGetValue(machine, out tier);

    public bool HasBuilt(string machine) => BuiltMultiblocks.Contains(machine);

    /// <summary>The installed coil's heat on a map, zero without coils.</summary>
    public int CoilHeatOf(string machine) => CoilHeat.GetValueOrDefault(machine);

    /// <summary>Whether a block of the given craftability era can be built at all.</summary>
    public bool Reaches(int? era) => era is { } known && known <= DefaultTier;
}
