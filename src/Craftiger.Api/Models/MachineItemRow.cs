namespace Craftiger.Api.Models;

/// <summary>A machine_items row of planner.sqlite as read at load.</summary>
internal sealed record MachineItemRow(string Map, string ItemId, long? Tier, long Multiblock, long Steam, long? Era);
