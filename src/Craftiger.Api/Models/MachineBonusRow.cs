namespace Craftiger.Api.Models;

/// <summary>A machine_bonuses row of planner.sqlite as read at load.</summary>
internal sealed record MachineBonusRow(string ItemId, string Kind, double Bonus, long Multiplicative, string? TierAxis);
