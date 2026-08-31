namespace Craftiger.Builder.Models.Dump;

/// <summary>A CropsNH crop: what plants it, what it drops and how fast, and what must lie under it to grow.</summary>
public sealed record DumpCrop(
    string Id,
    string CropId,
    string Name,
    string? SeedId,
    bool Hidden,
    int Tier,
    long GrowthDuration,
    double DropChance,
    int MinSeedBedTier,
    IReadOnlyList<DumpCropDrop> Drops,
    IReadOnlyList<string> UnderBlocks);
