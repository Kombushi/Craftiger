using Craftiger.Solver.Models.Bom;

namespace Craftiger.Api.Models;

/// <summary>A chain node in both accountings with its recipe's display data, one catalyst stack per tool slot, and the shaped grid as nine cells indexing InputsPerRun then Catalysts (null without a shape).</summary>
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
    bool Seed,
    IReadOnlyList<int?>? Grid);
