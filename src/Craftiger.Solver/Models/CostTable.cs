namespace Craftiger.Solver.Models;

/// <summary>A solved cost table: cheapest known cost per item (absent means unreachable), the
/// recipe that set it (leaves may carry one yet never expand), and whether the fixpoint
/// settled inside its budget.</summary>
public sealed record CostTable(
    IReadOnlyDictionary<string, double> Costs,
    IReadOnlyDictionary<string, SolverRecipe> BestRecipes,
    bool Converged);
