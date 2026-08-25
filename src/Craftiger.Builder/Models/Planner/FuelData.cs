namespace Craftiger.Builder.Models.Planner;

public sealed record FuelData(
    IReadOnlyList<PlannerFuel> Fuels,
    IReadOnlyList<PlannerBoilerFuel> BoilerFuels);
