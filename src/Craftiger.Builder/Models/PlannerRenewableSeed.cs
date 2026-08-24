namespace Craftiger.Builder.Models;

/// <summary>An auto-infinite primitive: obtainable automatically and forever. Kind is
/// WORLD (curated world sources), FARM (farm-product leaves), or MOB (auto mob-farm drops,
/// includable per factory).</summary>
public sealed record PlannerRenewableSeed(string ItemId, string Kind);
