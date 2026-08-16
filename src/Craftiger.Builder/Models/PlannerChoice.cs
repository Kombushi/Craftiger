namespace Craftiger.Builder.Models;

/// <summary>One input slot a recipe will accept any of several stacks for, each at its own
/// amount. The cheapest alternative decides both the slot's price and the era it can first
/// be filled at.</summary>
public sealed record PlannerChoice(IReadOnlyList<(string ItemId, long Amount)> Alternatives);
