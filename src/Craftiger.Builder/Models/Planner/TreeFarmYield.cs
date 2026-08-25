namespace Craftiger.Builder.Models.Planner;

/// <summary>The Tree Growth Simulator's power-tier output multiplier, 2t² − 2t + 5, and the tier its recipes ship at.</summary>
public static class TreeFarmYield
{
    /// <summary>The lowest tier the controller runs at; the dump's amounts scale from here.</summary>
    public const int BaseTier = 1;

    public static int TierMultiplier(int tier) => 2 * tier * tier - 2 * tier + 5;
}
