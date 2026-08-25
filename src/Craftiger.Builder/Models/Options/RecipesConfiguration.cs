namespace Craftiger.Builder.Models.Options;

/// <summary>Which dumped recipes ship, and under what machine name.</summary>
public sealed record RecipesConfiguration
{
    /// <summary>Exact machine names dropped as pseudo-recipe sources.</summary>
    public required IReadOnlyList<string> ExcludedMachines { get; init; }

    /// <summary>Dump ids of recipes the game registers but the machine never performs, each with the observation that condemned it.</summary>
    public required IReadOnlyDictionary<string, string> PhantomRecipeIds { get; init; }

    /// <summary>GregTech recipe-category suffixes marking reverse-crafting, matched case-insensitively.</summary>
    public required IReadOnlyList<string> RecyclingCategorySuffixes { get; init; }

    /// <summary>Machines whose recipes gate eras but never price: real mechanics that would amplify matter.</summary>
    public required IReadOnlyList<string> EraOnlyMachines { get; init; }

    /// <summary>Machines whose output slots 2+ open by tier; the value lists the tier per byproduct slot.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<int>> ByproductSlotTiers { get; init; }

    /// <summary>Suffix-stripped recipe-type names mapped to canonical machine names.</summary>
    public required IReadOnlyDictionary<string, string> MachineRenames { get; init; }
}
