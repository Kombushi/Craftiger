namespace Craftiger.Builder.Models.Planner;

/// <summary>CropsNH growth at the model's baseline: stat-0 seeds on watered, sky-lit sticks in a neutral biome, verified against TileEntityCropSticks.</summary>
public static class CropGrowth
{
    /// <summary>Game ticks between crop stick growth cycles.</summary>
    public const int TicksPerCycle = 256;

    /// <summary>Nutrient points at the baseline: 5 base + 10 water + 2 sky, before the five-fold scale.</summary>
    public const int BaselineNutrients = 17;

    /// <summary>Fertilized sticks add the full 10-point fertilizer bonus.</summary>
    public const int FertilizedNutrients = 27;

    /// <summary>Crops of this tier and above cannot grow unfertilized.</summary>
    public const int FertilizerTier = 9;

    /// <summary>Growth points per cycle for a stat-0 seed, zero when the crop would sicken.</summary>
    public static int Rate(int cropTier, bool fertilized)
    {
        var nutrients = (fertilized ? FertilizedNutrients : BaselineNutrients) * 5;
        var need = cropTier * 10;
        return nutrients >= need
            ? 6 * (100 + nutrients - need) / 100
            : Math.Max(6 * (100 - (need - nutrients) * 4) / 100, 0);
    }

    /// <summary>Game ticks from planting to harvest, zero when the crop cannot grow.</summary>
    public static long MaturationTicks(long growthDuration, int cropTier, bool fertilized)
    {
        var rate = Rate(cropTier, fertilized);
        return rate <= 0 ? 0 : TicksPerCycle * ((growthDuration + rate - 1) / rate);
    }

    /// <summary>Crop sticks a field machine of the tier serves: the manager's square of radius 3 + 2t.</summary>
    public static int FieldSize(int machineTier)
    {
        var side = (3 + 2 * machineTier) * 2 + 1;
        return side * side;
    }

    /// <summary>Extra harvest rounds per machine tier: 5 % on a manager, 20 % on a farm seed bed.</summary>
    public static double RoundBonus(int machineTier, double bonusPerTier) => 1.0 + bonusPerTier * machineTier;

    /// <summary>Water potency one seed drinks over a maturation, at the farm's per-cycle rate.</summary>
    public static long WaterPerSeed(long maturationTicks) =>
        (maturationTicks + TicksPerCycle - 1) / TicksPerCycle;
}
