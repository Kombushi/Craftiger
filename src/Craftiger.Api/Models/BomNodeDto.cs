using Craftiger.Solver.Models;

namespace Craftiger.Api.Models;

/// <summary>A chain node: one expanded item with its chosen recipe's display data, the
/// chosen input stacks per single run, and the recipe's full output rows. Demand and runs
/// come in both accountings — fractional expected values and the whole-run plan.</summary>
public sealed record BomNodeDto(
    string ItemId,
    double Amount,
    double Runs,
    long WholeAmount,
    long WholeRuns,
    string RecipeId,
    string Machine,
    int Tier,
    int? MultiTier,
    int? Heat,
    long DurationTicks,
    long EuT,
    IReadOnlyList<BomStack> InputsPerRun,
    IReadOnlyList<OutputDto> Outputs);