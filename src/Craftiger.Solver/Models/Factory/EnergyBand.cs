namespace Craftiger.Solver.Models.Factory;

/// <summary>A quality band of an energy target: this much net EU/t must come from generators at or above the tier.</summary>
public readonly record struct EnergyBand(int Tier, double Rate);
