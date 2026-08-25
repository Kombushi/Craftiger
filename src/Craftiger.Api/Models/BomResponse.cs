using Craftiger.Solver.Models.Bom;

namespace Craftiger.Api.Models;

/// <summary>The BOM plus everything a chain renderer needs: the nodes in topological order, targets first, and a display lookup for every referenced item.</summary>
public sealed record BomResponse(
    IReadOnlyList<BomTargetResult> Targets,
    IReadOnlyList<BomLeaf> Leaves,
    IReadOnlyList<BomWarning> Warnings,
    IReadOnlyList<BomNodeDto> Nodes,
    IReadOnlyDictionary<string, ItemRefDto> Items);
