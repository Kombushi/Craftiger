using Craftiger.Solver.Models;

namespace Craftiger.Api.Models;

/// <summary>A chain node: one expanded item with its chosen recipe's display data, the
/// chosen input stacks per single run, and the recipe's full output rows. Demand and runs
/// come in both accountings — fractional expected values and the whole-run plan. Catalysts
/// are the recipe's tool slots: needed in place, never consumed, one stack per slot. Loop
/// members carry their loop's number; the seed node is the outside unit that starts it.</summary>
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
    IReadOnlyList<BomStack> Catalysts,
    IReadOnlyList<OutputDto> Outputs,
    int? Loop,
    bool Seed);