namespace Craftiger.Solver.Models;

/// <summary>Machine blocks per map, keyed by the map name recipes carry. A map without an
/// entry runs on an anonymous block at the garage tier — the pre-machine-props behavior,
/// flagged per line.</summary>
public sealed record FactoryMachineData(IReadOnlyDictionary<string, IReadOnlyList<FactoryMachineBlock>> BlocksByMap)
{
    public static readonly FactoryMachineData Empty =
        new(new Dictionary<string, IReadOnlyList<FactoryMachineBlock>>());
}
