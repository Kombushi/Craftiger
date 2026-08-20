namespace Craftiger.Solver.Models;

/// <summary>One expanded step of the walk: an item's total demand and recipe runs in both
/// accountings — fractional expected values for pricing, and the whole-run plan (demand
/// accumulated across all consumers, then rounded up once) a machine can actually execute —
/// plus that recipe's chosen input stacks per single run. <paramref name="Loop"/> numbers the
/// loop an item belongs to; the <paramref name="Seed"/> node is the one outside unit that
/// starts that loop and shares its item with a loop member.</summary>
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
