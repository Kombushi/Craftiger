namespace Craftiger.Builder.Models.Planner;

/// <summary>One member of a catalyst slot; Tool marks a wearing tool, unlike a circuit, mold or lens merely held in place.</summary>
public sealed record PlannerCatalyst(string ItemId, long Amount, bool Tool);
