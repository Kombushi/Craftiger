namespace Craftiger.Builder.Models;

/// <summary>One turbine rotor variant with its computed large-turbine stats.</summary>
public sealed record DumpTurbineRotor(
    string ItemId, string Size, string Material, long Durability, double BaseEfficiency,
    int OverflowTier, IReadOnlyList<DumpRotorFuelStats> FuelStats);
