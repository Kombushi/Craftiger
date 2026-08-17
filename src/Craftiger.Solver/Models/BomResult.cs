namespace Craftiger.Solver.Models;

/// <summary>The computed bill of materials: per-target direct inputs, merged leaf totals for
/// the whole cart, warnings, and one chain node per expanded item in topological order —
/// targets first, so a renderer can lay the chain out in a single pass.</summary>
public sealed record BomResult(
    IReadOnlyList<BomTargetResult> Targets,
    IReadOnlyList<BomLeaf> Leaves,
    IReadOnlyList<BomWarning> Warnings,
    IReadOnlyList<BomNode> Nodes);
