namespace Craftiger.Solver.Models.Bom;

/// <summary>The computed bill of materials: per-target direct inputs, merged leaf totals, warnings, and the chain nodes in topological order, targets first.</summary>
public sealed record BomResult(
    IReadOnlyList<BomTargetResult> Targets,
    IReadOnlyList<BomLeaf> Leaves,
    IReadOnlyList<BomWarning> Warnings,
    IReadOnlyList<BomNode> Nodes);
