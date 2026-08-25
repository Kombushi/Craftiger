namespace Craftiger.Builder.Models.Planner;

/// <summary>One slot a recipe needs in place but never consumes; shipped for display and the tool tie-break, never priced.</summary>
public sealed record PlannerCatalystSlot(IReadOnlyList<PlannerCatalyst> Alternatives);
