namespace Craftiger.Builder.Models;

/// <summary>Per-fuel-class rotor stats; flow is L/t for steam and EU/t of fuel value
/// otherwise, efficiencies are fractions.</summary>
public sealed record DumpRotorFuelStats(
    string Fuel, double Efficiency, double LooseEfficiency, double OptimalFlow,
    double LooseOptimalFlow, double OptimalEut, double LooseOptimalEut);
