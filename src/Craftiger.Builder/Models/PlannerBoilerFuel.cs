namespace Craftiger.Builder.Models;

/// <summary>How long one unit of a fuel burns in one large-boiler generation; steam produced
/// is the boiler's own rate times this.</summary>
public sealed record PlannerBoilerFuel(string ItemId, string Boiler, double BurnSeconds);
