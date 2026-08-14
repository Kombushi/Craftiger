namespace Gtnh.Planner.Builder.Models;

/// <summary>Raw dump content loaded up front; all later stages are pure transforms.</summary>
public sealed class Dump
{
    public required Dictionary<string, DumpItem> Items { get; init; }
    public required Dictionary<string, DumpFluid> Fluids { get; init; }
    public required List<DumpRecipe> Recipes { get; init; }
    public required Dictionary<string, DumpGtData> GtByRecipeId { get; init; }
    public required Dictionary<string, List<DumpItemStack>> GroupStacks { get; init; }
    public required List<(string OredictName, string GroupId)> Oredict { get; init; }
    public required Dictionary<string, List<(long Slot, string GroupId)>> ItemInputsByRecipe { get; init; }
    public required Dictionary<string, List<DumpItemOutput>> ItemOutputsByRecipe { get; init; }
    public required Dictionary<string, List<DumpFluidInput>> FluidInputsByRecipe { get; init; }
    public required Dictionary<string, List<DumpFluidOutput>> FluidOutputsByRecipe { get; init; }
    public required Dictionary<string, DumpContainer> ContainersByItemId { get; init; }
    public required Dictionary<string, List<string>> HandlerItemsByRecipeTypeId { get; init; }
    public required List<DumpWorldgenOre> WorldgenOres { get; init; }

    /// <summary>Input-voltage tier per machine item, parsed from its "Voltage IN" tooltip.</summary>
    public required Dictionary<string, int> MachineVoltageTiers { get; init; }
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
}
