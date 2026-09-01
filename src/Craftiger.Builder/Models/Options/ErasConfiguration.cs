namespace Craftiger.Builder.Models.Options;

/// <summary>Progression knowledge the dump does not carry: machine gates, the cleanroom wall, dimension eras, and the coil ladder.</summary>
public sealed record ErasConfiguration
{
    /// <summary>Era floors for machines whose real gate lives outside the recipe graph, like the Godforge upgrade tree; anchored to the quest book.</summary>
    public required IReadOnlyDictionary<string, int> MachineEraFloors { get; init; }

    /// <summary>Cleanroom-flagged recipes inherit this machine item's era.</summary>
    public required string CleanroomItemName { get; init; }

    /// <summary>The cleanroom is the pack's HV progression wall; its era never resolves lower.</summary>
    public required int CleanroomMinEra { get; init; }

    /// <summary>Era needed to reach each GT dimension tier (1-8 rockets, 9 mothership, 10 Deep Dark).</summary>
    public required IReadOnlyDictionary<int, int> DimensionTierEras { get; init; }

    /// <summary>Era by dimension abbreviation for tier-0 worlds reached without a rocket.</summary>
    public required IReadOnlyDictionary<string, int> DimensionEraOverrides { get; init; }

    /// <summary>Mobs whose drops date from an era, by dump mob id; the dump names no mob's world, so an unlisted mob's drops seed nothing.</summary>
    public required IReadOnlyDictionary<string, int> MobDropEras { get; init; }
}
