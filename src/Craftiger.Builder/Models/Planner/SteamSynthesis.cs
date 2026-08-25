namespace Craftiger.Builder.Models.Planner;

/// <summary>Everything the steam carrier adds: boiler recipes, machine rows no map lists, the steam pseudo-fuels, and the carrier's constants.</summary>
public sealed record SteamSynthesis(
    IReadOnlyList<PlannerRecipe> Recipes,
    IReadOnlyList<PlannerMachineItem> Machines,
    IReadOnlyList<PlannerFuel> Fuels,
    SteamCarrier Carrier);
