namespace Craftiger.Builder.Models.Dump;

/// <summary>Every recipe with its GregTech data, input groups, outputs, fluid containers and handler icons.</summary>
public sealed record DumpRecipeSet(
    IReadOnlyList<DumpRecipe> Recipes,
    IReadOnlyDictionary<string, DumpGtData> GtByRecipeId,
    IReadOnlyDictionary<string, IReadOnlyList<DumpItemStack>> GroupStacks,
    IReadOnlyDictionary<string, IReadOnlyList<DumpItemInput>> ItemInputsByRecipe,
    IReadOnlyDictionary<string, IReadOnlyList<DumpItemOutput>> ItemOutputsByRecipe,
    IReadOnlyDictionary<string, IReadOnlyList<DumpFluidInput>> FluidInputsByRecipe,
    IReadOnlyDictionary<string, IReadOnlyList<DumpFluidOutput>> FluidOutputsByRecipe,
    IReadOnlyDictionary<string, DumpContainer> ContainersByItemId,
    IReadOnlyDictionary<string, IReadOnlyList<string>> HandlerItemsByRecipeTypeId);
