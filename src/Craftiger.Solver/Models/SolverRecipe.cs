namespace Craftiger.Solver.Models;

/// <summary>A recipe as the artifact ships it: <paramref name="Tier"/> is what a single-block
/// needs, <paramref name="MultiTier"/> what the map's multiblock needs where owning one lowers
/// the bar, <paramref name="Heat"/> is set on coil-gated recipes only, and
/// <paramref name="ToolSlots"/> counts the catalyst slots holding a wearing tool.</summary>
public sealed record SolverRecipe(
    string Id,
    string Machine,
    int Tier,
    int? MultiTier,
    int? Heat,
    IReadOnlyList<SolverSlot> Slots,
    IReadOnlyList<SolverOutput> Outputs,
    int ToolSlots = 0);
