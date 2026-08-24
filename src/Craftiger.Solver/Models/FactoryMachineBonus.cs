namespace Craftiger.Solver.Models;

/// <summary>One extracted multiblock bonus: <paramref name="Kind"/> and
/// <paramref name="TierAxis"/> are the artifact's enums (PARALLEL, SPEED, EU_DISCOUNT… over
/// COIL, VOLTAGE, SOLENOID…); per-tier bonuses scale with the axis component's tier, resolved
/// against garage state at solve time.</summary>
public sealed record FactoryMachineBonus(string Kind, double Bonus, bool Multiplicative, string? TierAxis);
