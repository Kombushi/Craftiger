namespace Craftiger.Solver.Models.Factory;

/// <summary>One rung of the coil ladder, translating the garage's installed heat into the tier bonus axes scale by.</summary>
public sealed record FactoryCoil(int Tier, int MaxHeat);
