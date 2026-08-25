namespace Craftiger.Solver.Models.Factory;

/// <summary>GregTech's voltage ladder, V = 8 · 4^tier, and the Enet transfer loss it implies.</summary>
public static class VoltageTier
{
    private const int MaxTier = 14;

    /// <summary>The ladder position of a voltage.</summary>
    public static int Of(long voltage)
    {
        var tier = 0;
        while (voltage > 8L << (2 * tier) && tier < MaxTier)
        {
            tier++;
        }
        return tier;
    }

    /// <summary>EU lost per amp emitted at a tier: 2^max(0, tier − 1).</summary>
    public static double EnetLossPerAmp(int tier) => Math.Pow(2, Math.Max(0, tier - 1));
}
