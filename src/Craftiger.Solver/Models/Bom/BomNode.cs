namespace Craftiger.Solver.Models.Bom;

/// <summary>One expanded step of the walk in both accountings — fractional expected values and the whole-run plan — with the recipe's chosen input stacks per run; Loop numbers the loop, Seed marks the outside unit that starts it.</summary>
public sealed record BomNode(
    string ItemId,
    double Amount,
    double Runs,
    long WholeAmount,
    long WholeRuns,
    string RecipeId,
    IReadOnlyList<BomStack> InputsPerRun,
    int? Loop,
    bool Seed);
