namespace Craftiger.Solver.Models.Factory;

/// <summary>One reactor mode combination's per-machine rates: fuel in, raw output, spent fuel back, and the mode fluids drained.</summary>
public sealed record ReactorRun(
    double FuelPerSecond,
    double RawEuT,
    double ReturnPerSecond,
    IReadOnlyList<(string FluidId, double PerSecond)> Consumes,
    string? Variant);
