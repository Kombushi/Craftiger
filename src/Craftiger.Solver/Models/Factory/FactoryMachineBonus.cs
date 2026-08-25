namespace Craftiger.Solver.Models.Factory;

/// <summary>One extracted multiblock bonus; Kind and TierAxis are the artifact's enums, per-tier kinds scale with the axis component's tier at solve time.</summary>
public sealed record FactoryMachineBonus(string Kind, double Bonus, bool Multiplicative, string? TierAxis);
