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

    /// <summary>Wildcard oredict name patterns that group distinct materials and must not unify them.</summary>
    public required IReadOnlyList<string> GroupingOredictPrefixes { get; init; }
    public required IReadOnlyList<string> GroupingOredictInfixes { get; init; }

    /// <summary>Oredict names of world-minable leaf blocks.</summary>
    public required IReadOnlyList<string> MinableBlockOredicts { get; init; }

    /// <summary>Oredict prefixes of farmable leaves.</summary>
    public required IReadOnlyList<string> FarmableOredictPrefixes { get; init; }

    /// <summary>Fluid internal names priced at zero.</summary>
    public required IReadOnlyList<string> FreeFluids { get; init; }

    /// <summary>Multiblocks that draw two 2A energy hatches, tiering recipes at 4x amperage.</summary>
    public required IReadOnlyList<string> MultiAmpMachines { get; init; }

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
        MultiAmpMachines = ["Blast Furnace"],
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