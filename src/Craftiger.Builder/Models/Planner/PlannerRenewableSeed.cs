namespace Craftiger.Builder.Models.Planner;

/// <summary>An auto-infinite primitive of kind WORLD (curated), FARM (farm-product leaves) or MOB (auto mob-farm drops).</summary>
public sealed record PlannerRenewableSeed(string ItemId, string Kind);
