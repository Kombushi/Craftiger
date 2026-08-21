namespace Craftiger.Solver.Models;

/// <summary>A loop's recipes and the gain matrix between its members: how many units of member
/// <c>i</c> one unit of member <c>j</c> consumes through <c>j</c>'s recipe.</summary>
internal sealed record LoopSystem(
    IReadOnlyDictionary<string, int> Index, SolverRecipe[] Recipes, double[] Yields,
    IReadOnlyList<SolverStack>[] Inputs, double[,] Gain);
