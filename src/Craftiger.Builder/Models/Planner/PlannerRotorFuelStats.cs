namespace Craftiger.Builder.Models.Planner;

public sealed record PlannerRotorFuelStats(
    string ItemId, string Fuel, double Efficiency, double LooseEfficiency, double OptimalFlow,
    double LooseOptimalFlow, double OptimalEut, double LooseOptimalEut);
