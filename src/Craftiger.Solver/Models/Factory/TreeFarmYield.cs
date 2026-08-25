namespace Craftiger.Solver.Models.Factory;

/// <summary>The Tree Growth Simulator's power-tier output multiplier, 2t² − 2t + 5: a fixed-length run whose outputs grow with the hatch tier.</summary>
public static class TreeFarmYield
{
    public static int TierMultiplier(int tier) => 2 * tier * tier - 2 * tier + 5;

    /// <summary>How much more a run yields at a tier than at the tier the recipe's amounts were taken at.</summary>
    public static double Gain(int baseTier, int tier) => (double)TierMultiplier(tier) / TierMultiplier(baseTier);
}
