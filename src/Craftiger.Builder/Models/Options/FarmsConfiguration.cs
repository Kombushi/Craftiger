namespace Craftiger.Builder.Models.Options;

/// <summary>Pack ids of the farm machines and their consumables; the growth math itself lives in CropGrowth.</summary>
public sealed record FarmsConfiguration
{
    public required string CropManagerMap { get; init; }

    /// <summary>Manager block item ids in tier order, the first being LV.</summary>
    public required IReadOnlyList<string> CropManagerItemIds { get; init; }

    public required string IndustrialFarmMap { get; init; }

    public required string IndustrialFarmItemId { get; init; }

    /// <summary>The seed-bed structure tiers the Industrial Farm accepts.</summary>
    public required int IndustrialFarmMinTier { get; init; }

    public required int IndustrialFarmMaxTier { get; init; }

    public required string EecMap { get; init; }

    public required string EecItemId { get; init; }

    public required string SpawnerItemId { get; init; }

    public required string WaterFluidName { get; init; }

    public required string XpJuiceFluidId { get; init; }

    /// <summary>How much xpjuice one kill yields.</summary>
    public required long XpJuicePerKill { get; init; }

    public required IReadOnlyList<FarmFertilizer> Fertilizers { get; init; }

    /// <summary>The liquid the Industrial Farm feeds its sticks, with its potency per mB.</summary>
    public required string LiquidFertilizerFluidId { get; init; }

    public required int LiquidFertilizerPotency { get; init; }

    /// <summary>The enriched liquid a fertilizer-unit build demands instead.</summary>
    public required string EnrichedFertilizerFluidId { get; init; }

    public required int EnrichedFertilizerPotency { get; init; }
}
