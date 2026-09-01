namespace Craftiger.Builder.Models.Dump;

/// <summary>One machine serving a recipe map. Multiblocks report no tier: they are not tiered blocks.</summary>
public sealed record DumpRecipeMapMachine(string ItemId, bool Multiblock, int? Tier, bool Steam, int? OutputSlots);
