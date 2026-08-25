namespace Craftiger.Builder.Models.Options;

/// <summary>Names for the machines behind builder-synthesized recipes, and the rig items able to perform them.</summary>
public sealed record SynthesizedMachinesConfiguration
{
    /// <summary>Machine name for breaking a block by hand, owned from the start.</summary>
    public required string BlockBreakMachine { get; init; }

    /// <summary>Machine name for harvesting a crop.</summary>
    public required string CropHarvestMachine { get; init; }

    /// <summary>Machine name for pumping a fluid out of the ground.</summary>
    public required string PumpMachine { get; init; }

    /// <summary>Machines that pump underground fluids, by item name.</summary>
    public required IReadOnlyList<string> PumpMachineItemNames { get; init; }
}
