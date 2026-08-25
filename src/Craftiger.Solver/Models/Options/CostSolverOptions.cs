namespace Craftiger.Solver.Models.Options;

/// <summary>Tuning of the cost fixpoint.</summary>
public sealed record CostSolverOptions
{
    /// <summary>A recipe wins an item only when it beats the standing cost by more than this.</summary>
    public double Epsilon { get; init; } = 1e-9;

    /// <summary>A duplication loop never settles on its own, so the walk is bounded per recipe.</summary>
    public int MaxPassesPerRecipe { get; init; } = 200;
}
