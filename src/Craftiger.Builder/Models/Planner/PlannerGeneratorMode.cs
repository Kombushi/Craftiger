namespace Craftiger.Builder.Models.Planner;

/// <summary>One consumable mode on a boosted generator: a booster or upkeep fluid at a rate, with the output factor the mode applies.</summary>
public sealed record PlannerGeneratorMode(
    string ItemId,
    string Kind,
    string FluidId,
    double PerSecond,
    double Factor);
