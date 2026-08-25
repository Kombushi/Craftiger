namespace Craftiger.Solver.Models.Bom;

/// <summary>One cart target with its chosen recipe's direct inputs scaled by runs; a leaf or unreachable target has neither.</summary>
public sealed record BomTargetResult(string ItemId, long Count, string? RecipeId, IReadOnlyList<BomStack> Inputs);
