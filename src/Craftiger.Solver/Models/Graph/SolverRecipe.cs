namespace Craftiger.Solver.Models.Graph;

/// <summary>A recipe as the artifact ships it; MultiTier is set where the map's multiblock lowers the bar, Heat on coil-gated recipes only.</summary>
public sealed record SolverRecipe(
    string Id,
    string Machine,
    int Tier,
    int? MultiTier,
    int? Heat,
    IReadOnlyList<SolverSlot> Slots,
    IReadOnlyList<SolverOutput> Outputs,
    int ToolSlots = 0);
