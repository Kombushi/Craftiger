namespace Gtnh.Planner.Builder;

public sealed record DumpItem(string Id, string Name, string ModId, string InternalName, string ImagePath);

public sealed record DumpFluid(string Id, string Name, string ModId, string InternalName, string ImagePath);

public sealed record DumpRecipe(string Id, string Machine, string Category);

public sealed record DumpGtData(string RecipeId, long Voltage, long Amperage, long Duration, int? Heat);

public sealed record DumpItemStack(string ItemId, long Size);

public sealed record DumpItemOutput(string RecipeId, string ItemId, long Size, double Chance);

public sealed record DumpFluidInput(string RecipeId, string FluidId, long Amount);

public sealed record DumpFluidOutput(string RecipeId, string FluidId, long Amount, double Chance);

public sealed record DumpContainer(string FluidId, long Amount, string EmptyItemId);

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
    public required string ExporterVersion { get; init; }
    public required DateTimeOffset ExportedAt { get; init; }
}
