namespace Craftiger.Solver.Models;

/// <summary>One rung of the coil ladder: bonus axes scale by coil tier while the garage
/// stores the installed coil's heat, so the ladder translates between the two.</summary>
public sealed record FactoryCoil(int Tier, int MaxHeat);
