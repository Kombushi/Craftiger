namespace Craftiger.Solver.Models.Factory;

/// <summary>How one generator block burns one fuel: units consumed per second and raw EU/t before the Enet loss.</summary>
public readonly record struct FuelBurn(double UnitsPerSecond, double RawEuT);
