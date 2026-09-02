namespace Craftiger.Builder.Models.Planner;

/// <summary>The Algae Pond's own rules: one energy hatch sets the tier, power is nine tenths of its voltage, and compost buys the next tier's row.</summary>
public static class AlgaePondRun
{
    /// <summary>GT's voltage table starts at ULV's 8 EU/t, which the Steam-floored ladder cannot express.</summary>
    private const long UlvVoltage = 8;

    public static long EuT(int hatchTier) => (hatchTier == 0 ? UlvVoltage : TierLadder.Voltage(hatchTier)) * 9 / 10;

    /// <summary>Compost per run that lifts a hatch tier's row to the next: one up to LV, then doubling, capped at a stack.</summary>
    public static long CompostFor(int hatchTier) => hatchTier > 1 ? Math.Min(64, 1L << (hatchTier - 1)) : 1;

    /// <summary>A ULV hatch's pond still needs LV power in the ladder; every other hatch tier is its own.</summary>
    public static int LadderTier(int hatchTier) => Math.Max(1, hatchTier);
}
