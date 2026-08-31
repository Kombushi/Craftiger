namespace Craftiger.Builder.Models.Planner;

/// <summary>An Industrial Farm upgrade build: unit counts filling the bed tier's slots, one slot per structure slice, verified against MTEIndustrialFarm.</summary>
public sealed record FarmBuild(string Suffix, int Accelerators, bool FertilizerUnit, int Harvesters, bool Overclocked)
{
    /// <summary>Each growth acceleration unit adds its full bonus to speed and this share of the base power.</summary>
    private const double AcceleratorPowerBonus = 1.25;

    /// <summary>The fertilizer unit: half again the speed, half a harvest round, half the base power.</summary>
    private const double FertilizerSpeedFactor = 1.5;
    private const double FertilizerRoundBonus = 0.5;
    private const double FertilizerPowerBonus = 0.5;

    /// <summary>Each advanced harvesting unit multiplies rounds by a fifth and adds half the base power.</summary>
    private const double HarvesterRoundFactor = 0.2;
    private const double HarvesterPowerBonus = 0.5;

    /// <summary>The overclocked growth acceleration unit exists from ZPM.</summary>
    private const int OverclockMinTier = 7;

    /// <summary>Seed beds grant a fifth extra harvest rounds per tier.</summary>
    private const double BedRoundBonus = 0.2;

    public static readonly FarmBuild Base = new("", 0, false, 0, false);

    /// <summary>The shipped builds at a bed tier: bare, all-accelerator, the harvest build (fertilizer unit first, then harvesters, accelerators on the rest), and the overclocked build.</summary>
    public static IEnumerable<FarmBuild> Of(int bedTier)
    {
        yield return Base;
        var slots = bedTier - 1;
        if (slots >= 1)
        {
            yield return new FarmBuild("~gau", slots, false, 0, false);
            yield return new FarmBuild("~hrv", Math.Max(0, slots - 3), true, Math.Min(2, slots - 1), false);
        }
        if (bedTier >= OverclockMinTier)
        {
            yield return new FarmBuild("~oc", 0, false, 0, true);
        }
    }

    /// <summary>Whether the build forces fertilized sticks with enriched liquid fertilizer.</summary>
    public bool Enriched => FertilizerUnit;

    /// <summary>Growth speed: accelerators add, the fertilizer unit multiplies.</summary>
    public double SpeedFactor => (1 + Accelerators) * (FertilizerUnit ? FertilizerSpeedFactor : 1);

    /// <summary>Harvest rounds: bed and fertilizer bonuses add, harvesters multiply.</summary>
    public double RoundFactor(int bedTier) =>
        (1 + BedRoundBonus * bedTier + (FertilizerUnit ? FertilizerRoundBonus : 0))
        * (1 + HarvesterRoundFactor * Harvesters);

    /// <summary>The build's draw over the bed tier's base power; the overclocked unit costs nothing until the ladder quadruples it.</summary>
    public long PowerOf(long basePower) => (long)(basePower * (1
        + AcceleratorPowerBonus * Accelerators
        + (FertilizerUnit ? FertilizerPowerBonus : 0)
        + HarvesterPowerBonus * Harvesters));
}
