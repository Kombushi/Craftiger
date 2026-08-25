using Craftiger.Solver.Models.Lp;

namespace Craftiger.Solver.Highs.Models;

/// <summary>How the layer sequence ended: the standing solution in the scaled column space when every layer that mattered was optimal.</summary>
public sealed record LayerOutcome(LpSolveStatus Status, double[]? Standing);
