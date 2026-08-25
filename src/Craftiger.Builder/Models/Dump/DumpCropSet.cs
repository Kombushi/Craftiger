namespace Craftiger.Builder.Models.Dump;

/// <summary>What grows, breaks and dies into items: crops, block drops, and capturable mobs' drops.</summary>
public sealed record DumpCropSet(
    IReadOnlyList<DumpCrop> Crops,
    IReadOnlyList<DumpBlockDrop> BlockDrops,
    IReadOnlySet<string> MobDropItemIds);
