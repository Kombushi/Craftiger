namespace Craftiger.Builder.Models;

public sealed record FuelData(
    IReadOnlyList<PlannerFuel> Fuels,
    IReadOnlyList<PlannerBoilerFuel> BoilerFuels);
