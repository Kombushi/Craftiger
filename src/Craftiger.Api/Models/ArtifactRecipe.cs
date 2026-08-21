using Craftiger.Solver.Models;

namespace Craftiger.Api.Models;

/// <summary>Display data for one recipe as planner.sqlite ships it. Catalysts are the tool,
/// mold, and circuit slots the recipe needs in place but never consumes — display only.</summary>
public sealed record ArtifactRecipe(
    string Id,
    string Machine,
    int Tier,
    int? MultiTier,
    int? Heat,
    long DurationTicks,
    long EuT,
    IReadOnlyList<SolverSlot> Catalysts);
