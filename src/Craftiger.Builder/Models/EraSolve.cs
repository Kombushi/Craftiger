namespace Craftiger.Builder.Models;

/// <param name="MachineEras">Per map, the era of its cheapest craftable machine block —
/// null where no serving block ever becomes craftable.</param>
public sealed record EraSolve(
    Dictionary<string, int> Tiers,
    Dictionary<string, int> Era,
    Dictionary<string, PlannerRecipe> BestRecipe,
    HashSet<string> Seeds,
    Dictionary<string, int?> MachineEras);
