namespace Craftiger.Solver.Models.Factory;

/// <summary>One consumable mode on a boosted generator block: a fluid drained per second and the factor the mode applies.</summary>
public sealed record GeneratorMode(
    GeneratorModeKind Kind,
    string FluidId,
    double PerSecond,
    double Factor);
