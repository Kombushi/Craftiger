namespace Craftiger.Builder.Models.Dump;

/// <summary>One item input slot of a recipe: the grid cell or slot number and the group it accepts.</summary>
public sealed record DumpItemInput(long Slot, string GroupId);
