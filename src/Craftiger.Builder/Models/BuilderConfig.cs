namespace Craftiger.Builder.Models;

/// <summary>Editable heuristics and pack-specific lists used by the pipeline.</summary>
public sealed record BuilderConfig
{
    /// <summary>Input item-id prefixes stripped as non-consumed catalysts.</summary>
    public required IReadOnlyList<string> CatalystItemIdPrefixes { get; init; }

    /// <summary>Machine-name suffixes of informational pseudo-recipe tabs.</summary>
    public required IReadOnlyList<string> ExcludedMachineSuffixes { get; init; }
    public required IReadOnlyList<string> ExcludedMachinePrefixes { get; init; }

    /// <summary>Exact machine names dropped as pseudo-recipe sources.</summary>
    public required IReadOnlyList<string> ExcludedMachines { get; init; }

    /// <summary>Machines whose recipes gate eras but never price: real mechanics that would amplify matter.</summary>
    public required IReadOnlyList<string> EraOnlyMachines { get; init; }

    /// <summary>Machines whose output slots 2+ open by tier; the value lists the tier per byproduct slot.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<int>> ByproductSlotTiers { get; init; }

    /// <summary>Recipes consuming these items are dropped; composition-based
    /// recycling of composed machines conjures materials their crafting never used.</summary>
    public required IReadOnlyList<string> ExcludedInputItems { get; init; }

    /// <summary>Machine item name prefixes marking steam machines; they run their
    /// map's LV-and-below recipes in the steam era.</summary>
    public required IReadOnlyList<string> SteamMachinePrefixes { get; init; }

    /// <summary>Cleanroom-flagged recipes inherit this machine item's era.</summary>
    public required string CleanroomItemName { get; init; }

    /// <summary>The cleanroom is the pack's HV progression wall; its era never resolves lower.</summary>
    public required int CleanroomMinEra { get; init; }

    /// <summary>Wildcard oredict name patterns that group distinct materials and must not unify them.</summary>
    public required IReadOnlyList<string> GroupingOredictPrefixes { get; init; }
    public required IReadOnlyList<string> GroupingOredictInfixes { get; init; }
    public required IReadOnlyList<string> GroupingOredictNames { get; init; }

    /// <summary>Era needed to reach each GT dimension tier (1-8 rockets, 9 mothership, 10 Deep Dark).</summary>
    public required IReadOnlyDictionary<int, int> DimensionTierEras { get; init; }

    /// <summary>Era by dimension abbreviation for tier-0 worlds reached without a rocket.</summary>
    public required IReadOnlyDictionary<string, int> DimensionEraOverrides { get; init; }

    /// <summary>Ore materials whose blocks exist as items but never world-generate; they get no era seed.</summary>
    public required IReadOnlyList<string> NonSpawningOres { get; init; }

    /// <summary>World-minable leaf blocks by oredict name or, where the dump gives none,
    /// by item id — each at the era of the cheapest world it can be mined in.</summary>
    public required IReadOnlyDictionary<string, int> MinableBlockEras { get; init; }

    /// <summary>Fluids the world hands over freely, by internal name, mapped to the era they are
    /// free at. A null era means the dump decides it: the fluid is pumped, and its era is the
    /// cheapest rig that can drill it. Pumpable fluids left off this list are not free — they
    /// price through their own chemistry, and pumping only gates when they become available.</summary>
    public required IReadOnlyDictionary<string, int?> WorldFluids { get; init; }

    /// <summary>Machines that pump underground fluids, by item name.</summary>
    public required IReadOnlyList<string> PumpMachineItemNames { get; init; }

    /// <summary>Machine name for pumping a fluid out of the ground.</summary>
    public required string PumpMachine { get; init; }

    /// <summary>Machine name for harvesting a crop.</summary>
    public required string CropHarvestMachine { get; init; }

    /// <summary>Oredict prefixes of farmable leaves.</summary>
    public required IReadOnlyList<string> FarmableOredictPrefixes { get; init; }

    /// <summary>Machine name for breaking a block by hand, owned from the start.</summary>
    public required string BlockBreakMachine { get; init; }

    /// <summary>EBF coil ladder; tier is the coil's voltage-tier equivalent.</summary>
    public required IReadOnlyList<CoilSpec> Coils { get; init; }

    /// <summary>Recipe-type names mapped to canonical machine names.</summary>
    public required IReadOnlyDictionary<string, string> MachineRenames { get; init; }
}
