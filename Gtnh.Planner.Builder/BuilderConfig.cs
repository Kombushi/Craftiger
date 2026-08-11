namespace Gtnh.Planner.Builder;

/// <summary>Editable heuristics and pack-specific lists used by the pipeline.</summary>
public sealed record BuilderConfig
{
    /// <summary>Input item-id prefixes stripped as non-consumed catalysts.</summary>
    public required IReadOnlyList<string> CatalystItemIdPrefixes { get; init; }

    /// <summary>Machine-name suffixes of informational pseudo-recipe tabs.</summary>
    public required IReadOnlyList<string> ExcludedMachineSuffixes { get; init; }

    /// <summary>Exact machine names dropped as pseudo-recipe sources.</summary>
    public required IReadOnlyList<string> ExcludedMachines { get; init; }

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

    /// <summary>Era seeds for ores that spawn only in later-dimension worlds
    /// (Moon = HV, Mars = EV, ...); ores not listed seed at era 0.</summary>
    public required IReadOnlyDictionary<string, int> OreEras { get; init; }

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
        ExcludedMachines = ["Radio Hatch Material List"],
        ExcludedInputItems = ["Electrical Engine"],
        SteamMachinePrefixes = ["Steam ", "High Pressure "],
        GroupingOredictPrefixes = ["listAll", "crafting"],
        GroupingOredictInfixes = ["Any"],
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
        OreEras = new Dictionary<string, int>
        {
            ["Ilmenite"] = 3,
            ["Rutile"] = 3,
            ["Naquadah"] = 4,
            ["Mytryl"] = 4,
            ["Quantium"] = 4,
            ["Ledox"] = 5,
            ["CallistoIce"] = 5,
            ["Oriharukon"] = 5,
            ["MysteriousCrystal"] = 5,
            ["BlackPlutonium"] = 5,
            ["Trinium"] = 6
        },
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

public sealed record CoilSpec(string Name, int MaxHeat, int Tier);