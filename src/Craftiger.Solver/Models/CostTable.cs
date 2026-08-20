namespace Craftiger.Solver.Models;

/// <summary>A solved cost table: cheapest known cost per item (absent means unreachable), the
/// recipe that set it (leaves may carry one yet never expand), the input stack that recipe was
/// priced with per slot, and whether the fixpoint settled inside its budget.</summary>
public sealed record CostTable(
    IReadOnlyDictionary<string, double> Costs,
    IReadOnlyDictionary<string, SolverRecipe> BestRecipes,
    IReadOnlyDictionary<string, IReadOnlyList<SolverStack>> ChosenInputs,
    bool Converged);
