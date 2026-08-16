namespace Craftiger.Solver.Models;

/// <summary>The computed bill of materials: per-target direct inputs, merged leaf totals for
/// the whole cart, and warnings.</summary>
public sealed record BomResult(
    IReadOnlyList<BomTargetResult> Targets,
    IReadOnlyList<BomStack> Leaves,
    IReadOnlyList<BomWarning> Warnings);
