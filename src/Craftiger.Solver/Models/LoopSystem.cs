namespace Craftiger.Solver.Models;

/// <summary>A loop's recipes and the gain matrix between its members: how many units of member
/// <c>i</c> one unit of member <c>j</c> consumes through <c>j</c>'s recipe. <paramref name="Row"/>
/// maps a member position to its row.</summary>
internal sealed record LoopSystem(IReadOnlyDictionary<int, int> Row, int[] Recipes, double[] Yields, int[][] Picks, double[,] Gain);
