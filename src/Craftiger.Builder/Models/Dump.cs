namespace Craftiger.Builder.Models;

/// <summary>Raw dump content loaded up front; all later stages are pure transforms.</summary>
public sealed class Dump
{
    public required Dictionary<string, DumpItem> Items { get; init; }
    public required Dictionary<string, DumpFluid> Fluids { get; init; }
    public required List<DumpRecipe> Recipes { get; init; }
    public required Dictionary<string, DumpGtData> GtByRecipeId { get; init; }
    public required Dictionary<string, List<DumpItemStack>> GroupStacks { get; init; }
    public required List<(string OredictName, string GroupId)> Oredict { get; init; }

    /// <summary>Oredict names GT unifies, each with the item GT substitutes for the name.</summary>
    public required Dictionary<string, string> UnifiedOredictTargets { get; init; }

    /// <summary>Items GT excludes from unification even inside a unified oredict.</summary>
    public required HashSet<string> UnificationBlacklist { get; init; }

    /// <summary>GT's ore-prefix registry behind a longest-prefix matcher.</summary>
    public required OrePrefixIndex OrePrefixes { get; init; }

    /// <summary>Forge container items: what using this item in a craft leaves behind.</summary>
    public required Dictionary<string, string> ItemContainers { get; init; }

    /// <summary>GT's item composition records behind an id-segment matcher.</summary>
    public required ItemDataIndex ItemData { get; init; }
    public required Dictionary<string, List<(long Slot, string GroupId)>> ItemInputsByRecipe { get; init; }
    public required Dictionary<string, List<DumpItemOutput>> ItemOutputsByRecipe { get; init; }
    public required Dictionary<string, List<DumpFluidInput>> FluidInputsByRecipe { get; init; }
    public required Dictionary<string, List<DumpFluidOutput>> FluidOutputsByRecipe { get; init; }
    public required Dictionary<string, DumpContainer> ContainersByItemId { get; init; }
    public required Dictionary<string, List<string>> HandlerItemsByRecipeTypeId { get; init; }
    public required List<DumpWorldgenOre> WorldgenOres { get; init; }

    /// <summary>GregTech recipe map per recipe type; non-GregTech types have none.</summary>
    public required Dictionary<string, DumpRecipeMap> RecipeMapByTypeId { get; init; }
    public required List<DumpBlockDrop> BlockDrops { get; init; }
    public required List<DumpCrop> Crops { get; init; }
    public required List<DumpUndergroundFluid> UndergroundFluids { get; init; }

    /// <summary>Input-voltage tier per machine item, parsed from its "Voltage IN" tooltip.</summary>
    public required Dictionary<string, int> MachineVoltageTiers { get; init; }

    public required List<DumpGenerator> Generators { get; init; }
    public required List<DumpDynamo> Dynamos { get; init; }
    public required List<DumpBoiler> Boilers { get; init; }
    public required List<DumpMultiblockMachine> MultiblockMachines { get; init; }
    public required List<DumpTurbineRotor> TurbineRotors { get; init; }
    public required string ExporterVersion { get; init; }
    public required DateTimeOffset ExportedAt { get; init; }

    public string NameOf(string id)
    {
        return Fluids.TryGetValue(id, out var fluid) ? fluid.Name
            : Items.TryGetValue(id, out var item) ? item.Name : id;
    }

    public string ImagePathOf(string id)
    {
        return Fluids.TryGetValue(id, out var fluid) ? fluid.ImagePath
            : Items.TryGetValue(id, out var item) ? item.ImagePath : "";
    }

    public IEnumerable<string> ItemIdsNamed(string name) =>
        Items.Values.Where(i => i.Name == name).Select(i => i.Id);
}
