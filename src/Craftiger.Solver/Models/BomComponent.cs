namespace Craftiger.Solver.Models;

/// <summary>A strongly connected component of the chosen-edge graph: one item, or a loop of
/// items whose chosen recipes consume each other.</summary>
internal sealed record BomComponent(List<string> Items, bool Loop);
