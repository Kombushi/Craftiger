namespace Craftiger.Solver.Models;

/// <summary>One input slot; the recipe accepts any one of the alternatives, each at its own
/// amount, and costs the slot at the cheapest of them.</summary>
public sealed record SolverSlot(IReadOnlyList<SolverStack> Alternatives);
