using Craftiger.Solver.Models.Costs;

namespace Craftiger.Solver.Models.Factory;

/// <summary>Machine blocks per map (keyed by the map name recipes carry), the coil ladder, the burnable fuels per generator map, rotor stats and dynamo hatches; a map without blocks runs on an anonymous flagged block.</summary>
public sealed record FactoryMachineData(
    IReadOnlyDictionary<string, IReadOnlyList<FactoryMachineBlock>> BlocksByMap,
    IReadOnlyList<FactoryCoil> Coils,
    IReadOnlyList<FactoryFuel> Fuels,
    IReadOnlyList<FactoryRotorStats> Rotors,
    IReadOnlyList<FactoryDynamo> Dynamos)
{
    public static readonly FactoryMachineData Empty =
        new(new Dictionary<string, IReadOnlyList<FactoryMachineBlock>>(), [], [], [], []);

    public IReadOnlyList<FactoryMachineBlock>? BlocksOf(string map) => BlocksByMap.GetValueOrDefault(map);

    /// <summary>The installed coil's tier on a map, zero without coils.</summary>
    public int CoilTier(Garage garage, string machine)
    {
        var heat = garage.CoilHeatOf(machine);
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

    /// <summary>Whether the garage can build any steam block at all.</summary>
    public bool HasBuildableSteamBlock(Garage garage) =>
        BlocksByMap.Values.Any(blocks => blocks.Any(block => block.Steam && block.IsBuildable(garage)));

    /// <summary>The hatches the garage can build that the block accepts, in id order.</summary>
    public IEnumerable<FactoryDynamo> UsableHatches(Garage garage, FactoryMachineBlock block) =>
        Dynamos
            .Where(hatch => hatch.IsBuildable(garage) && block.AcceptsHatch(hatch))
            .OrderBy(hatch => hatch.ItemId, StringComparer.Ordinal);

    /// <summary>The largest voltage-times-amps a usable hatch offers the block.</summary>
    public double HatchCapacity(Garage garage, FactoryMachineBlock block) =>
        UsableHatches(garage, block).Select(hatch => hatch.Capacity).DefaultIfEmpty(0).Max();

    /// <summary>The hatch that nets the most from a raw output: capacity caps with voiding while the Enet loss rises with tier; null without a usable hatch.</summary>
    public HatchChoice? BestHatch(Garage garage, FactoryMachineBlock block, double rawEuT)
    {
        HatchChoice? best = null;
        foreach (var hatch in UsableHatches(garage, block))
        {
            var choice = hatch.Emit(rawEuT);
            if (best is null || choice.NetEuT > best.Value.NetEuT)
            {
                best = choice;
            }
        }
        return best;
    }
}
