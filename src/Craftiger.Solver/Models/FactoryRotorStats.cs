namespace Craftiger.Solver.Models;

/// <summary>One rotor's turbine numbers for one fuel class (GAS, PLASMA, STEAM), tight and
/// loose fit. Flows and outputs are in EU/t at the rotor's optimal flow — the physical mB/t
/// is the flow divided by the fuel's EU per unit.</summary>
public sealed record FactoryRotorStats(
    string ItemId,
    string Fuel,
    double Efficiency,
    double LooseEfficiency,
    double OptimalFlow,
    double LooseOptimalFlow,
    double OptimalEut,
    double LooseOptimalEut);
