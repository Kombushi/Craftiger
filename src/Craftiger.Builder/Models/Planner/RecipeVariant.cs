namespace Craftiger.Builder.Models.Planner;

/// <summary>One tier-unlocked variant of a recipe: the byproduct slots open at that tier, under a derived id.</summary>
public sealed record RecipeVariant(string Id, int Tier, IReadOnlyList<PlannerOutput> Outputs);
