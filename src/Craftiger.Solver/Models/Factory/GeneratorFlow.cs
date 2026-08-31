namespace Craftiger.Solver.Models.Factory;

/// <summary>One extra per-machine flow on a generator line, in units per second.</summary>
public readonly record struct GeneratorFlow(int Item, double PerSecond);
