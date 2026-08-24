namespace Craftiger.Builder.Models;

/// <summary>Everything the steam carrier adds to the artifact: boiler recipes, the machine
/// rows for controllers no recipe map lists, and the steam pseudo-fuels turbines burn.</summary>
public sealed record SteamSynthesis(
    List<PlannerRecipe> Recipes,
    List<PlannerMachineItem> Machines,
    List<PlannerFuel> Fuels);
