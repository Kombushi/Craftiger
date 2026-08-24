namespace Craftiger.Builder.Models;

/// <summary>One machine block serving a recipe map; the flip side of machine_eras.
/// <paramref name="Era"/> is the block item's own era solve — when the garage can build it.</summary>
public sealed record PlannerMachineItem(
    string Map, string ItemId, int? Tier, bool Multiblock, bool Steam, int? Era);
