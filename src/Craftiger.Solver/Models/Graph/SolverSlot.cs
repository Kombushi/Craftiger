namespace Craftiger.Solver.Models.Graph;

/// <summary>One input slot; the recipe accepts any one alternative, each at its own amount.</summary>
public sealed record SolverSlot(IReadOnlyList<SolverStack> Alternatives);
