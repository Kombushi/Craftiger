namespace Craftiger.Builder.Models.Planner;

/// <summary>The synthesized crop-farm lines and the machine blocks that run them.</summary>
public sealed record CropFarms(IReadOnlyList<PlannerRecipe> Recipes, IReadOnlyList<PlannerMachineItem> Machines);
