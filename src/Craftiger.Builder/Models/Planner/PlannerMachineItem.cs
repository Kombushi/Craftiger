namespace Craftiger.Builder.Models.Planner;

/// <summary>One machine block serving a recipe map; Era is the block item's own era solve.</summary>
public sealed record PlannerMachineItem(
    string Map, string ItemId, int? Tier, bool Multiblock, bool Steam, int? Era);
