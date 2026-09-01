namespace Craftiger.Builder.Models.Dump;

/// <summary>Raw dump content loaded up front; every later stage is a pure transform over it.</summary>
public sealed record Dump
{
    public required IReadOnlyDictionary<string, DumpItem> Items { get; init; }

    public required IReadOnlyDictionary<string, DumpFluid> Fluids { get; init; }

    public required IReadOnlyList<DumpRecipe> Recipes { get; init; }

    public required IReadOnlyDictionary<string, DumpGtData> GtByRecipeId { get; init; }

    public required IReadOnlyDictionary<string, IReadOnlyList<DumpItemStack>> GroupStacks { get; init; }

    public required IReadOnlyList<DumpOredictEntry> Oredict { get; init; }

    /// <summary>Oredict names GT unifies, each with the item GT substitutes for the name.</summary>
    public required IReadOnlyDictionary<string, string> UnifiedOredictTargets { get; init; }

    /// <summary>Items GT excludes from unification even inside a unified oredict.</summary>
    public required IReadOnlySet<string> UnificationBlacklist { get; init; }

    public required OrePrefixIndex OrePrefixes { get; init; }

    /// <summary>Forge container items: what using this item in a craft leaves behind.</summary>
    public required IReadOnlyDictionary<string, string> ItemContainers { get; init; }

    public required ItemDataIndex ItemData { get; init; }

    public required IReadOnlyDictionary<string, IReadOnlyList<DumpItemInput>> ItemInputsByRecipe { get; init; }

    public required IReadOnlyDictionary<string, IReadOnlyList<DumpItemOutput>> ItemOutputsByRecipe { get; init; }

    public required IReadOnlyDictionary<string, IReadOnlyList<DumpFluidInput>> FluidInputsByRecipe { get; init; }

    public required IReadOnlyDictionary<string, IReadOnlyList<DumpFluidOutput>> FluidOutputsByRecipe { get; init; }

    public required IReadOnlyDictionary<string, DumpContainer> ContainersByItemId { get; init; }

    public required IReadOnlyDictionary<string, IReadOnlyList<string>> HandlerItemsByRecipeTypeId { get; init; }

    public required IReadOnlyList<DumpWorldgenOre> WorldgenOres { get; init; }

    /// <summary>GregTech recipe map per recipe type; non-GregTech types have none.</summary>
    public required IReadOnlyDictionary<string, DumpRecipeMap> RecipeMapByTypeId { get; init; }

    /// <summary>Every registered machine with its Java class, recipe-map-serving or not.</summary>
    public required IReadOnlyList<DumpMachine> Machines { get; init; }

    /// <summary>The map a machine of the given class serves; null when no such machine or map exists.</summary>
    public DumpRecipeMap? MapServedBy(string classSuffix)
    {
        var items = Machines
            .Where(machine => machine.MachineClass.EndsWith(classSuffix, StringComparison.Ordinal))
            .Select(machine => machine.ItemId)
            .ToHashSet();
        return items.Count == 0
            ? null
            : RecipeMapByTypeId.Values
                .Distinct()
                .FirstOrDefault(map => map.Machines.Any(machine => items.Contains(machine.ItemId)));
    }

    public required IReadOnlyList<DumpBlockDrop> BlockDrops { get; init; }

    public required IReadOnlyList<DumpCrop> Crops { get; init; }

    public required IReadOnlyList<DumpUndergroundFluid> UndergroundFluids { get; init; }

    /// <summary>Input-voltage tier per machine item, parsed from its "Voltage IN" tooltip.</summary>
    public required IReadOnlyDictionary<string, int> MachineVoltageTiers { get; init; }

    public required IReadOnlyList<DumpGenerator> Generators { get; init; }

    public required IReadOnlyList<DumpDynamo> Dynamos { get; init; }

    public required IReadOnlyList<DumpBoiler> Boilers { get; init; }

    public required IReadOnlyList<DumpMultiblockMachine> MultiblockMachines { get; init; }

    public required IReadOnlyList<DumpTurbineRotor> TurbineRotors { get; init; }

    public required IReadOnlyList<DumpTreeFarmTool> TreeFarmTools { get; init; }

    public required IReadOnlyList<DumpCoil> Coils { get; init; }

    public required IReadOnlyList<DumpEngine> Engines { get; init; }

    public required IReadOnlyList<DumpReactorMode> ReactorModes { get; init; }

    /// <summary>Mechanics constants read off GregTech code at export time, by name.</summary>
    public required IReadOnlyDictionary<string, long> Constants { get; init; }

    public long Constant(string name) =>
        Constants.TryGetValue(name, out var value)
            ? value
            : throw new InvalidOperationException($"dump exports no constant {name}");

    /// <summary>Distinct drops of soul-vial-capturable mobs: what an auto mob farm yields.</summary>
    public required IReadOnlyList<DumpMob> Mobs { get; init; }

    public required IReadOnlyList<DumpFertilizer> Fertilizers { get; init; }

    public required IReadOnlyList<DumpFluidFertilizer> FluidFertilizers { get; init; }

    public required IReadOnlyList<DumpFarmComponent> FarmComponents { get; init; }

    /// <summary>Every mob's drop item ids by mob id, capturable or not.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> MobDropsByMob { get; init; }

    /// <summary>Items wearing GT's rigid deprecation banner; superseded controllers never ship as machine blocks.</summary>
    public required IReadOnlySet<string> DeprecatedItems { get; init; }

    public required string ExporterVersion { get; init; }

    public required DateTimeOffset ExportedAt { get; init; }

    public bool IsFluid(string id) => Fluids.ContainsKey(id);

    public string NameOf(string id) =>
        Fluids.TryGetValue(id, out var fluid) ? fluid.Name
            : Items.TryGetValue(id, out var item) ? item.Name : id;

    public string ImagePathOf(string id) =>
        Fluids.TryGetValue(id, out var fluid) ? fluid.ImagePath
            : Items.TryGetValue(id, out var item) ? item.ImagePath : "";

    public IEnumerable<string> ItemIdsNamed(string name) =>
        Items.Values.Where(item => item.Name == name).Select(item => item.Id);

    public IEnumerable<string> FluidIdsNamed(string name) =>
        Fluids.Values.Where(fluid => fluid.Name == name).Select(fluid => fluid.Id);

    public IReadOnlyList<DumpItemInput> ItemInputsOf(string recipeId) =>
        ItemInputsByRecipe.GetValueOrDefault(recipeId) ?? [];

    public IReadOnlyList<DumpItemOutput> ItemOutputsOf(string recipeId) =>
        ItemOutputsByRecipe.GetValueOrDefault(recipeId) ?? [];

    public IReadOnlyList<DumpFluidInput> FluidInputsOf(string recipeId) =>
        FluidInputsByRecipe.GetValueOrDefault(recipeId) ?? [];

    public IReadOnlyList<DumpFluidOutput> FluidOutputsOf(string recipeId) =>
        FluidOutputsByRecipe.GetValueOrDefault(recipeId) ?? [];

    public IReadOnlyList<DumpItemStack> StacksOf(string groupId) =>
        GroupStacks.GetValueOrDefault(groupId) ?? [];

    /// <summary>A filled container splits into its empty form and the fluid it held; anything else is itself.</summary>
    public IEnumerable<(string ItemId, long Amount)> Decompose(string itemId, long amount)
    {
        if (ContainersByItemId.TryGetValue(itemId, out var container))
        {
            yield return (container.EmptyItemId, amount);
            yield return (container.FluidId, amount * container.Amount);
        }
        else
        {
            yield return (itemId, amount);
        }
    }
}
