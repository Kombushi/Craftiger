namespace Craftiger.Builder.Models;

/// <summary>Everything the planner.sqlite writer persists, assembled by the pipeline.</summary>
public sealed record PlannerData(
    Dump Dump,
    UnifiedItems Unified,
    List<PlannerRecipe> Recipes,
    IReadOnlyList<string> OrderedItemIds,
    Dictionary<string, string> LeafClasses,
    Dictionary<string, int> MaterialTiers,
    IReadOnlyDictionary<string, double> LeafWeights,
    IReadOnlyDictionary<string, string> Meta);
