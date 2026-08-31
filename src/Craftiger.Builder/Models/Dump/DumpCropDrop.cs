namespace Craftiger.Builder.Models.Dump;

/// <summary>One entry of a crop's drop table: an independent per-10000 chance rolled once per harvest round.</summary>
public sealed record DumpCropDrop(string ItemId, int Weight);
