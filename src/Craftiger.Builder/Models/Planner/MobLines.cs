namespace Craftiger.Builder.Models.Planner;

/// <summary>The synthesized Extreme Entity Crusher lines and the controller block that runs them.</summary>
public sealed record MobLines(IReadOnlyList<PlannerRecipe> Recipes, IReadOnlyList<PlannerMachineItem> Machines);
