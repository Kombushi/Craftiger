namespace Craftiger.Solver.Models;

/// <summary>A strongly connected component of the chosen-edge graph: one item, or a loop of
/// items whose chosen recipes consume each other. Items are index positions.</summary>
internal sealed record BomComponent(List<int> Items, bool Loop);
