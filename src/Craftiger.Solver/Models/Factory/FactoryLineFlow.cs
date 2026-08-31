namespace Craftiger.Solver.Models.Factory;

/// <summary>One item stream a line consumes or produces, in units per second.</summary>
public sealed record FactoryLineFlow(string ItemId, double PerSecond);
