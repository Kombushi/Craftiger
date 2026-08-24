namespace Craftiger.Solver.Models;

/// <summary>One purchased leaf inflow at the weight the resource layer charged — zero for
/// auto-infinite seeds; <paramref name="AutoInfinite"/> marks fixpoint members.</summary>
public sealed record FactoryInflow(string ItemId, double Rate, double Weight, bool AutoInfinite = false);
