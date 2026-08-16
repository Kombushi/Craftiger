using Craftiger.Solver.Models;

namespace Craftiger.Api.Models;

/// <summary>The BOM plus everything a chain renderer needs in one response: the nodes in
/// topological order (targets first) and a display lookup for every referenced item.</summary>
public sealed record BomResponse(
    IReadOnlyList<BomTargetResult> Targets,
    IReadOnlyList<BomStack> Leaves,
    IReadOnlyList<BomWarning> Warnings,
    IReadOnlyList<BomNodeDto> Nodes,
    IReadOnlyDictionary<string, ItemRefDto> Items);