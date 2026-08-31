namespace Craftiger.Solver.Models.Factory;

/// <summary>One engine mode's per-machine rates: fuel and consumables per second against the raw output.</summary>
public sealed record EngineBurn(
    double FuelPerSecond,
    double RawEuT,
    double LubricantPerSecond,
    double BoosterPerSecond);
