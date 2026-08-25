namespace Craftiger.Builder.Models.Planner;

/// <summary>A machine that can run a recipe, by canonical item id; Tier is its input voltage when tiered, and Steam marks a fuel-fired one.</summary>
public sealed record RecipeMachine(string ItemId, bool Multiblock, int? Tier, bool Steam);
