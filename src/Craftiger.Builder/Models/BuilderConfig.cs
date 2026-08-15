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
    public string PumpMachine { get; init; } = "Fluid Drilling";

    /// <summary>Oredict prefixes of farmable leaves.</summary>
    public required IReadOnlyList<string> FarmableOredictPrefixes { get; init; }

    /// <summary>Machine name for breaking a block by hand, owned from the start.</summary>
    public string BlockBreakMachine { get; init; } = "Mining";

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
        MinableBlockEras = new Dictionary<string, int>
        {
            ["stone"] = 0, ["cobblestone"] = 0, ["sand"] = 0, ["gravel"] = 0,
            ["dirt"] = 0, ["netherrack"] = 0, ["obsidian"] = 0, ["glowstone"] = 0,
            ["ice"] = 0, ["sandstone"] = 0, ["mycelium"] = 0, ["soulsand"] = 0,
            ["blockClay"] = 0, ["i~minecraft~clay~0"] = 0,
            // The End opens with the first rocket.
            ["endstone"] = 3
        },
        FarmableOredictPrefixes =
        [
            "seed", "crop", "treeSapling", "sugarcane", "blockCactus",
            "treeLeaves", "reed"
        ],
        WorldFluids = new Dictionary<string, int?>
        {
            ["water"] = 0,
            ["lava"] = 0,
            ["oil"] = null,
            ["gas_natural_gas"] = null,
            ["liquid_light_oil"] = null,
            ["liquid_medium_oil"] = null,
            ["liquid_heavy_oil"] = null,
            ["liquid_extra_heavy_oil"] = null
        },
        PumpMachineItemNames =
        [
            "Fluid Drilling Rig", "Fluid Drilling Rig II", "Fluid Drilling Rig III",
            "Fluid Drilling Rig IV", "Infinite Fluid Drilling Rig"
        ],
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
