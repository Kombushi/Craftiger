namespace Craftiger.Builder.Models.Planner;

/// <summary>One input slot accepting any of several stacks; the cheapest decides both its price and its era.</summary>
public sealed record PlannerChoice(IReadOnlyList<(string ItemId, long Amount)> Alternatives);
