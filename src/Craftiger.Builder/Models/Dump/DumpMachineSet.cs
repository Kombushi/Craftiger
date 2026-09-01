namespace Craftiger.Builder.Models.Dump;

/// <summary>Recipe maps with their machines, and the per-machine stat tables of the machine-props export.</summary>
public sealed record DumpMachineSet(
    IReadOnlyDictionary<string, DumpRecipeMap> RecipeMapByTypeId,
    IReadOnlyList<DumpMachine> Machines,
    IReadOnlyList<DumpGenerator> Generators,
    IReadOnlyList<DumpDynamo> Dynamos,
    IReadOnlyList<DumpBoiler> Boilers,
    IReadOnlyList<DumpMultiblockMachine> MultiblockMachines,
    IReadOnlyList<DumpTurbineRotor> TurbineRotors,
    IReadOnlyList<DumpTreeFarmTool> TreeFarmTools,
    IReadOnlyList<DumpCoil> Coils,
    IReadOnlyList<DumpEngine> Engines,
    IReadOnlyList<DumpReactorMode> ReactorModes);
