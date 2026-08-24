namespace Craftiger.Builder.Models;

/// <summary>One machine block serving a recipe map; the flip side of machine_eras.</summary>
public sealed record PlannerMachineItem(
    string Map, string ItemId, int? Tier, bool Multiblock, bool Steam);
