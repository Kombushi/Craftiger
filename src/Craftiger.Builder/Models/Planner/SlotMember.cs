namespace Craftiger.Builder.Models.Planner;

/// <summary>One member of an input slot after unification; Tool marks a wearing tool.</summary>
public sealed record SlotMember(string ItemId, long Amount, bool Tool);
