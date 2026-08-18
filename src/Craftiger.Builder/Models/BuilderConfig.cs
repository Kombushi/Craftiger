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

    /// <summary>Dump ids of recipes the game registers but the machine never performs,
    /// each mapped to the in-game observation that condemned it.</summary>
    public required IReadOnlyDictionary<string, string> PhantomRecipeIds { get; init; }

    /// <summary>GregTech recipe-category suffixes marking reverse-crafting, matched
    /// case-insensitively. The tag alone does not condemn a recipe: GregTech applies it to
    /// melting a rod down as readily as to melting a door down.</summary>
    public required IReadOnlyList<string> RecyclingCategorySuffixes { get; init; }

    /// <summary>Oredict prefixes naming a shape of one material. A reverse-crafting recipe that
    /// consumes only these gives back exactly what went into them, so it prices honestly and
    /// stays. Anything else it consumes is a manufactured item, whose material value can exceed
    /// what crafting it cost — melt those and the loop drives every price to nothing.</summary>
    public required IReadOnlyList<string> MaterialShapeOredictPrefixes { get; init; }

    /// <summary>Machines whose recipes gate eras but never price: real mechanics that would amplify matter.</summary>
    public required IReadOnlyList<string> EraOnlyMachines { get; init; }

    /// <summary>The voltage TecTech stamps on wirelessly star-powered recipes; it marks the
    /// absence of a hatch requirement, not a hatch requirement of everything.</summary>
    public required long WirelessSentinelVoltage { get; init; }

    /// <summary>Era floors for machines whose real gate lives outside the recipe graph,
    /// like the Godforge upgrade tree; anchored to the quest book.</summary>
    public required IReadOnlyDictionary<string, int> MachineEraFloors { get; init; }

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

    /// <summary>Accept-list oredicts over distinct items (any log, any pink flower): they still
    /// register for classification and search, but never merge their members' identities.</summary>
    public required IReadOnlyList<string> AcceptListOredictNames { get; init; }
    public required IReadOnlyList<string> AcceptListOredictPrefixes { get; init; }

    /// <summary>Era needed to reach each GT dimension tier (1-8 rockets, 9 mothership, 10 Deep Dark).</summary>
    public required IReadOnlyDictionary<int, int> DimensionTierEras { get; init; }

    /// <summary>Era by dimension abbreviation for tier-0 worlds reached without a rocket.</summary>
    public required IReadOnlyDictionary<string, int> DimensionEraOverrides { get; init; }

    /// <summary>World-minable leaf blocks by oredict name or, where the dump gives none,
    /// by item id — each at the era of the cheapest world it can be mined in.</summary>
    public required IReadOnlyDictionary<string, int> MinableBlockEras { get; init; }

    /// <summary>Fluids the world hands over, by internal name. Pumpable fluids left off this
    /// list are not world fluids — they price through their own chemistry, and pumping only
    /// gates when they become available.</summary>
    public required IReadOnlyDictionary<string, WorldFluid> WorldFluids { get; init; }

    /// <summary>Machines that pump underground fluids, by item name.</summary>
    public required IReadOnlyList<string> PumpMachineItemNames { get; init; }

    /// <summary>Machine name for pumping a fluid out of the ground.</summary>
    public required string PumpMachine { get; init; }

    /// <summary>Machine name for harvesting a crop.</summary>
    public required string CropHarvestMachine { get; init; }

    /// <summary>Oredict prefixes of ore-processing and smelting intermediates. They are never
    /// leaves: pricing them from a flat weight would cap every material made through them.</summary>
    public required IReadOnlyList<string> IntermediateOredictPrefixes { get; init; }

    /// <summary>Oredict prefixes of farmable leaves.</summary>
    public required IReadOnlyList<string> FarmableOredictPrefixes { get; init; }

    /// <summary>Machine name for breaking a block by hand, owned from the start.</summary>
    public required string BlockBreakMachine { get; init; }

    /// <summary>Ingot price at tier 0, the default of the cost model's B; the app lets the
    /// user retune it, and the build-time price check uses this value.</summary>
    public required double PriceBase { get; init; }

    /// <summary>How far below its own weight a leaf may price before the build reports it.
    /// Undercutting a leaf is normal — its weight is only the price when no route exists, and a
    /// cheap route beats it honestly. Undercutting it by orders of magnitude is not: that is a
    /// recipe loop handing back more material than it consumed.</summary>
    public required double PriceLeakRatio { get; init; }

    /// <summary>EBF coil ladder; tier is the coil's voltage-tier equivalent.</summary>
    public required IReadOnlyList<CoilSpec> Coils { get; init; }

    /// <summary>Recipe-type names mapped to canonical machine names.</summary>
    public required IReadOnlyDictionary<string, string> MachineRenames { get; init; }
}
