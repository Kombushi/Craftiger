namespace Craftiger.Solver.Models.Factory;

/// <summary>One purchased leaf inflow at the weight the resource layer charged — zero for auto-infinite seeds.</summary>
public sealed record FactoryInflow(string ItemId, double Rate, double Weight, bool AutoInfinite = false);
