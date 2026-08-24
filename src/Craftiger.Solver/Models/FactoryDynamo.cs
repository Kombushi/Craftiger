namespace Craftiger.Solver.Models;

/// <summary>A dynamo hatch: the capacity ceiling of a large turbine line. Output beyond
/// voltage times amps is voided, never stored.</summary>
public sealed record FactoryDynamo(string ItemId, int? Era, long EuT, long Amps);
