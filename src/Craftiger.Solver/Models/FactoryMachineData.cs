namespace Craftiger.Solver.Models;

/// <summary>Machine blocks per map, keyed by the map name recipes carry, the coil ladder for
/// tier-scaled bonuses, and the burnable fuels per generator map. A map without an entry runs
/// on an anonymous block at the garage tier — the pre-machine-props behavior, flagged per
/// line.</summary>
public sealed record FactoryMachineData(
    IReadOnlyDictionary<string, IReadOnlyList<FactoryMachineBlock>> BlocksByMap,
    IReadOnlyList<FactoryCoil> Coils,
    IReadOnlyList<FactoryFuel> Fuels,
    IReadOnlyList<FactoryRotorStats> Rotors,
    IReadOnlyList<FactoryDynamo> Dynamos)
{
    public static readonly FactoryMachineData Empty =
        new(new Dictionary<string, IReadOnlyList<FactoryMachineBlock>>(), [], [], [], []);

    /// <summary>The installed coil's tier on a map, zero without coils.</summary>
    public int CoilTier(Garage garage, string machine)
    {
        if (!garage.CoilHeat.TryGetValue(machine, out var heat))
        {
            return 0;
        }
        var tier = 0;
        foreach (var coil in Coils)
        {
            if (coil.MaxHeat <= heat && coil.Tier > tier)
            {
                tier = coil.Tier;
            }
        }
        return tier;
    }
}
