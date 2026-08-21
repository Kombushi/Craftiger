namespace Craftiger.Builder.Models;

/// <summary>One member of a catalyst slot. <paramref name="Tool"/> marks a wearing tool — an
/// item that crafts into its own worn self — as opposed to a circuit, mold, shape or lens
/// that merely has to be in place.</summary>
public sealed record PlannerCatalyst(string ItemId, long Amount, bool Tool);
