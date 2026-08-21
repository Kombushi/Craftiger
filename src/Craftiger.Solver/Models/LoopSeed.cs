namespace Craftiger.Solver.Models;

/// <summary>The one outside unit that starts a loop: which member, through which recipe, with
/// which input stacks.</summary>
internal sealed record LoopSeed(string ItemId, SolverRecipe Recipe, IReadOnlyList<SolverStack> Inputs);
