namespace Craftiger.Solver.Models;

/// <summary>One cart target with its chosen recipe's direct inputs, scaled by runs. A leaf or
/// unreachable target has no recipe and no inputs.</summary>
public sealed record BomTargetResult(string ItemId, long Count, string? RecipeId, IReadOnlyList<BomStack> Inputs);
