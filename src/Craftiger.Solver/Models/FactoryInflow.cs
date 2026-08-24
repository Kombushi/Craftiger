namespace Craftiger.Solver.Models;

/// <summary>One purchased leaf inflow at its resolved weight.</summary>
public sealed record FactoryInflow(string ItemId, double Rate, double Weight);
