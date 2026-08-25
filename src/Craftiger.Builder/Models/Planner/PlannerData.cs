using Craftiger.Builder.Models.Dump;

namespace Craftiger.Builder.Models.Planner;

/// <summary>Everything the planner.sqlite writer persists, assembled by the pipeline.</summary>
public sealed record PlannerData(
    Models.Dump.Dump Dump,
    UnifiedItems Unified,
    IReadOnlyList<PlannerRecipe> Recipes,
    IReadOnlyList<string> OrderedItemIds,
    IReadOnlyDictionary<string, string> LeafClasses,
    IReadOnlyDictionary<string, int> MaterialTiers,
    IReadOnlyDictionary<string, ItemParent> ItemParents,
    IReadOnlyDictionary<string, double> LeafWeights,
    IReadOnlyDictionary<string, int?> MachineEras,
    IReadOnlySet<string> MultiblockMachines,
    FuelData Fuels,
    MachinePropsData MachineProps,
    IReadOnlyList<PlannerRenewableSeed> RenewableSeeds,
    IReadOnlyDictionary<string, string> Meta);
