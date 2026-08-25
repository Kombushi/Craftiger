namespace Craftiger.Builder.Models.Planner;

/// <summary>How long one unit of a fuel burns in one large-boiler generation; steam produced is the boiler's rate times this.</summary>
public sealed record PlannerBoilerFuel(string ItemId, string Boiler, double BurnSeconds);
