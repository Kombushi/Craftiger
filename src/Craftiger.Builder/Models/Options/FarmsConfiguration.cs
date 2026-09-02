namespace Craftiger.Builder.Models.Options;

/// <summary>The farm maps' synthesized names and the pack ids no export carries; the growth math itself lives in CropGrowth.</summary>
public sealed record FarmsConfiguration
{
    public required string CropManagerMap { get; init; }

    public required string IndustrialFarmMap { get; init; }

    public required string EecMap { get; init; }

    public required string SpawnerItemId { get; init; }

    public required string WaterFluidName { get; init; }

    public required string XpJuiceFluidId { get; init; }

    public required string CompostItemId { get; init; }
}
