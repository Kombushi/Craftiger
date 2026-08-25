namespace Craftiger.Builder.Models.Planner;

/// <summary>One parallel/speed/EU bonus of a multiblock, as displayed in its tooltip.</summary>
public sealed record PlannerMachineBonus(
    string ItemId, string Kind, double Bonus, bool Multiplicative, string? TierAxis);
