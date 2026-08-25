namespace Craftiger.Solver.Models.Bom;

/// <summary>A strongly connected component of the chosen-edge graph: one item, or a loop of items whose chosen recipes consume each other.</summary>
public sealed record BomComponent(IReadOnlyList<int> Items, bool Loop)
{
    /// <summary>Loops keep their identity across walks by their smallest member position.</summary>
    public int Key => Items.Min();
}
