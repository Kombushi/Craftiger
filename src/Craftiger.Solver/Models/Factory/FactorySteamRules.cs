namespace Craftiger.Solver.Models.Factory;

/// <summary>The steam carrier as the artifact ships it: which fluids are steam, what turbines condense it into, and its energy content.</summary>
public sealed record FactorySteamRules(
    IReadOnlyList<string> SteamFluidIds,
    string? DistilledWaterId,
    double EuPerLiter,
    long WaterPerSteam)
{
    /// <summary>Bronze steam machines run a recipe over twice its duration; high pressure ones double the rate and the speed alike.</summary>
    private const double BronzeDurationFactor = 2.0;

    public static readonly FactorySteamRules Empty = new([], null, 0.5, 160);

    /// <summary>Liters a steam machine swallows per EU of the electric recipe, fit or high pressure alike.</summary>
    public double LitersPerRecipeEu => BronzeDurationFactor / EuPerLiter;

    /// <summary>Liters of distilled water a turbine returns per liter of steam.</summary>
    public double CondensatePerLiter => 1.0 / WaterPerSteam;

    /// <summary>The duration factor a steam block applies: bronze doubles it, high pressure keeps base speed.</summary>
    public static double DurationFactor(FactoryMachineBlock block) => block.Tier == 2 ? 1.0 : BronzeDurationFactor;
}
