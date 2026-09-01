using System.Collections.Frozen;

namespace Craftiger.Builder.Models.Planner;

/// <summary>The tier ladder (Steam = 0, LV = 1, ...) shared by recipes, machines, and artifact metadata.</summary>
public static class TierLadder
{
    public static readonly IReadOnlyList<string> Names =
    [
        "Steam", "LV", "MV", "HV", "EV", "IV", "LuV", "ZPM",
        "UV", "UHV", "UEV", "UIV", "UMV", "UXV", "MAX"
    ];

    private static readonly FrozenDictionary<string, int> LabelTiers = new Dictionary<string, int>
    {
        ["ULV"] = 1, ["LV"] = 1, ["MV"] = 2, ["HV"] = 3, ["EV"] = 4, ["IV"] = 5,
        ["LuV"] = 6, ["ZPM"] = 7, ["UV"] = 8, ["UHV"] = 9, ["UEV"] = 10,
        ["UIV"] = 11, ["UMV"] = 12, ["UXV"] = 13, ["MAX"] = 14
    }.ToFrozenDictionary();

    /// <summary>GT's own per-recipe tier label; it already accounts for machine amperage.</summary>
    public static int? LabelTier(string? label) =>
        label is not null && LabelTiers.TryGetValue(label, out var tier) ? tier : null;

    /// <summary>EU/t per amp a tier's machines run at; Steam machines draw no EU.</summary>
    public static long Voltage(int tier) => tier <= 0 ? 0 : 32L << (2 * (tier - 1));

    /// <summary>GT's practical voltage, 30/32 of the tier's voltage: what a recipe run at the tier draws.</summary>
    public static long PracticalVoltage(int tier) => Voltage(tier) * 30 / 32;

    /// <summary>Fallback when the dump carries no tier label.</summary>
    public static int VoltageTier(long euT)
    {
        if (euT <= 0)
        {
            return 0;
        }
        var tier = 1;
        long cap = 32;
        while (euT > cap)
        {
            tier++;
            cap *= 4;
        }
        return tier;
    }
}
