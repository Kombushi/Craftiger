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
    public string CleanroomItemName { get; init; } = "Cleanroom Controller";

    /// <summary>The cleanroom is the pack's HV progression wall; its era never resolves lower.</summary>
    public int CleanroomMinEra { get; init; } = 3;

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

    /// <summary>Oredict names of world-minable leaf blocks.</summary>
    public required IReadOnlyList<string> MinableBlockOredicts { get; init; }

    /// <summary>Items dropped by mining blocks (no recipe edge exists), seeded at era 0.</summary>
    public required IReadOnlyList<string> WorldDropItemIds { get; init; }

    /// <summary>World-pumped fluids by internal name, seeded at the era of reaching them.</summary>
    public required IReadOnlyDictionary<string, int> WorldFluidEras { get; init; }

    /// <summary>Oredict prefixes of farmable leaves.</summary>
    public required IReadOnlyList<string> FarmableOredictPrefixes { get; init; }

    /// <summary>Fluid internal names priced at zero.</summary>
    public required IReadOnlyList<string> FreeFluids { get; init; }

    /// <summary>Multiblocks are detected from the dump (few map handlers vs a tiered
    /// single-block family); these lists override that classification per machine.</summary>
    public required IReadOnlyList<string> ForceSingleAmp { get; init; }
    public required IReadOnlyList<string> ForceMultiAmp { get; init; }

    /// <summary>Maps with at most this many handler machines classify as multiblocks.</summary>
    public int MultiblockMaxHandlers { get; init; } = 8;

    /// <summary>EBF coil ladder; tier is the coil's voltage-tier equivalent.</summary>
    public required IReadOnlyList<CoilSpec> Coils { get; init; }

    /// <summary>Recipe-type names mapped to canonical machine names.</summary>
    public required IReadOnlyDictionary<string, string> MachineRenames { get; init; }

    public static BuilderConfig Default { get; } = new()
    {
        CatalystItemIdPrefixes =
        [
            "i~gregtech~gt.metatool",
            "i~gregtech~gt.integrated_circuit"
        ],
        ExcludedMachineSuffixes = [" Fuels"],
        ExcludedMachinePrefixes = ["Naquadah Reactor Mk"],
        ExcludedMachines =
        [
            "Radio Hatch Material List",
            "High Temperature Gas Reactor", "Liquid Fluoride Thorium Reactor",
            "Large Naquadah Reactor"
        ],
        EraOnlyMachines = ["Space Mining"],
        ByproductSlotTiers = new Dictionary<string, IReadOnlyList<int>>
        {
            // Second slot opens at HV (Universal Macerator), third at EV, fourth at IV.
            ["Macerator"] = [3, 4, 5]
        },
        ExcludedInputItems = ["Electrical Engine"],
        SteamMachinePrefixes = ["Steam ", "High Pressure "],
        GroupingOredictPrefixes = ["listAll", "crafting"],
        GroupingOredictInfixes = ["Any"],
        GroupingOredictNames = ["glowstone", "stoneGlowstone"],
        MinableBlockOredicts =
        [
            "stone", "cobblestone", "sand", "gravel", "dirt", "netherrack",
            "endstone", "obsidian", "glowstone", "ice", "sandstone", "mycelium",
            "soulsand", "blockClay"
        ],
        FarmableOredictPrefixes =
        [
            "seed", "crop", "treeSapling", "sugarcane", "blockCactus",
            "treeLeaves", "reed"
        ],
        FreeFluids = ["water"],
        WorldDropItemIds = ["i~minecraft~clay_ball~0"],
        WorldFluidEras = new Dictionary<string, int>
        {
            ["lava"] = 0,
            ["oil"] = 1,
            ["gas_natural_gas"] = 1,
            ["liquid_light_oil"] = 1,
            ["liquid_medium_oil"] = 1,
            ["liquid_heavy_oil"] = 1,
            ["liquid_extra_heavy_oil"] = 1
        },
        ForceSingleAmp = [],
        ForceMultiAmp = [],
        DimensionTierEras = new Dictionary<int, int>
        {
            [1] = 3,  // T1 rocket (Moon): HV
            [2] = 4,  // T2 rocket (Mars system): EV
            [3] = 5,
            [4] = 5,  // T3-T4 rockets: IV
            [5] = 6,  // T5 rocket: LuV
            [6] = 7,
            [7] = 7,  // T6-T7 rockets: ZPM
            [8] = 9,
            [9] = 9,  // T8 rocket and mothership: UHV
            [10] = 9  // Deep Dark, reached by mothership
        },
        DimensionEraOverrides = new Dictionary<string, int>
        {
            ["Ow"] = 0,
            ["Ne"] = 0,  // Nether: Steam Age
            ["TF"] = 1,  // Twilight Forest: LV
            ["ED"] = 3,  // The End: HV
            ["EA"] = 3,  // End asteroids, entered from the End
            ["Eg"] = 7   // Everglades portal: ZPM
        },
        NonSpawningOres =
        [
            "AncientGranite", "Comancheite", "Greenockite", "LanthaniteNd",
            "RadioactiveMineralMix", "Yttriaite", "Zircophyllite",
            "Koboldite", "RareEarthI", "RareEarthII", "RareEarthIII"
        ],
        Coils =
        [
            new("Cupronickel", 1800, 1),
            new("Kanthal", 2700, 2),
            new("Nichrome", 3600, 3),
            new("TPV-Alloy", 4500, 4),
            new("HSS-G", 5400, 5),
            new("HSS-S", 6600, 6),
            new("Naquadah", 7200, 6),
            new("Naquadah Alloy", 7800, 7),
            new("Trinium", 9000, 7),
            new("Electrum Flux", 9900, 8),
            new("Awakened Draconium", 10800, 8),
            new("Infinity", 12600, 9),
            new("Hypogen", 13500, 10),
            new("Eternal", 14400, 11)
        ],
        MachineRenames = new Dictionary<string, string>
        {
            ["Crafting (Shaped)"] = "Crafting Table",
            ["Crafting (Shapeless)"] = "Crafting Table",
            ["Extreme Crafting (Shaped)"] = "Extreme Crafting Table",
            ["Extreme Crafting (Shapeless)"] = "Extreme Crafting Table",
            ["Magical Crafting (Shaped)"] = "Arcane Workbench",
            ["Magical Crafting (Shapeless)"] = "Arcane Workbench"
        }
    };
}
