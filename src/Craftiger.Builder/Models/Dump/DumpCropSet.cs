namespace Craftiger.Builder.Models.Dump;

/// <summary>What grows, breaks and dies into items: crops, block drops, capturable mobs with their drops, and every mob's drop ids.</summary>
public sealed record DumpCropSet(
    IReadOnlyList<DumpCrop> Crops,
    IReadOnlyList<DumpBlockDrop> BlockDrops,
    IReadOnlyList<DumpMob> Mobs,
    IReadOnlyDictionary<string, IReadOnlyList<string>> MobDropsByMob,
    IReadOnlyList<DumpFertilizer> Fertilizers,
    IReadOnlyList<DumpFluidFertilizer> FluidFertilizers,
    IReadOnlyList<DumpFarmComponent> FarmComponents);
