namespace Craftiger.Builder.Models.Dump;

/// <summary>What grows, breaks and dies into items: crops, block drops, capturable mobs' drops, and every mob's drops by mob.</summary>
public sealed record DumpCropSet(
    IReadOnlyList<DumpCrop> Crops,
    IReadOnlyList<DumpBlockDrop> BlockDrops,
    IReadOnlySet<string> MobDropItemIds,
    IReadOnlyDictionary<string, IReadOnlyList<string>> MobDropsByMob);
