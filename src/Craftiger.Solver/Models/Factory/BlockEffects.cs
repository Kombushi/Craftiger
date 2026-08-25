namespace Craftiger.Solver.Models.Factory;

/// <summary>Resolved machine modifiers: factors on base duration and per-tick energy, parallels dividing busy machines, and whether an assumption was made.</summary>
public readonly record struct BlockEffects(double DurationFactor, double EuFactor, double Parallels, bool Estimated)
{
    public static readonly BlockEffects Plain = new(1, 1, 1, Estimated: false);

    public static readonly BlockEffects Anonymous = new(1, 1, 1, Estimated: true);

    /// <summary>A discount below this is a parsing artifact, not a machine.</summary>
    private const double MinimumEuFactor = 0.05;

    /// <summary>Bonuses as the exporter's templates define them: SPEED is the percentage run at, EU_DISCOUNT the percentage drawn, per-tier kinds add per axis tier, absolute speed multiplies by tier, multiplicative parallels compound per tier; an unresolvable axis contributes nothing and flags the line.</summary>
    public static BlockEffects Resolve(FactoryMachineBlock block, int coilTier, int voltageTier)
    {
        var speedPercent = 100.0;
        double? absoluteSpeedPerTier = null;
        var absoluteSpeedTier = 0;
        var euPercent = 100.0;
        var parallelBase = block.BaseParallels;
        var parallels = parallelBase;
        var estimated = block.Multiblock && block.MaxParallel <= 1 && block.Bonuses.Count == 0;

        foreach (var bonus in block.Bonuses)
        {
            var tier = bonus.TierAxis switch
            {
                null => 0,
                "COIL" => coilTier,
                "VOLTAGE" => voltageTier,
                _ => -1,
            };
            if (tier < 0)
            {
                estimated = true;
                continue;
            }
            switch (bonus.Kind)
            {
                case "SPEED":
                    speedPercent = bonus.Bonus;
                    break;
                case "SPEED_BONUS_PER_TIER":
                    speedPercent += bonus.Bonus * tier;
                    break;
                case "SPEED_PER_TIER":
                    if (tier == 0)
                    {
                        // The machine needs the component to run at all; the first tier is assumed.
                        tier = 1;
                        estimated = true;
                    }
                    absoluteSpeedPerTier = bonus.Bonus;
                    absoluteSpeedTier = tier;
                    break;
                case "EU_DISCOUNT":
                    euPercent = bonus.Bonus;
                    break;
                case "EU_DISCOUNT_PER_TIER":
                    euPercent += bonus.Bonus * tier;
                    break;
                case "PARALLEL":
                    parallelBase = bonus.Bonus;
                    parallels = parallelBase;
                    break;
                case "PARALLEL_PER_TIER":
                    parallels = bonus.Multiplicative
                        ? parallelBase * Math.Pow(bonus.Bonus, tier)
                        : parallelBase + bonus.Bonus * tier;
                    break;
                default:
                    estimated = true;
                    break;
            }
        }

        var durationFactor = absoluteSpeedPerTier is { } perTier
            ? 100.0 / (perTier * absoluteSpeedTier)
            : 100.0 / Math.Max(speedPercent, 1);
        var euFactor = euPercent / 100.0;
        if (euFactor < MinimumEuFactor)
        {
            euFactor = MinimumEuFactor;
            estimated = true;
        }
        return new BlockEffects(durationFactor, euFactor, Math.Max(1, parallels), estimated);
    }
}
