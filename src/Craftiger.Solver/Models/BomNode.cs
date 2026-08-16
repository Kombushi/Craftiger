namespace Craftiger.Solver.Models;

/// <summary>One expanded step of the walk: an item's total demand, the fractional runs of
/// its chosen recipe, and that recipe's chosen input stacks per single run.</summary>
public sealed record BomNode(
    string ItemId,
    double Amount,
    double Runs,
    string RecipeId,
    IReadOnlyList<BomStack> InputsPerRun);
