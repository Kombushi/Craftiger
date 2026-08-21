using Craftiger.Solver.Models;

namespace Craftiger.Api.Models;

/// <summary>The loaded artifact: the solver graph, the display data beside it, and the path
/// of the read-only database that search queries keep using.</summary>
public sealed record PlannerArtifact(
    SolverGraph Graph,
    IReadOnlyDictionary<string, ArtifactItem> Items,
    IReadOnlyList<string> CraftListOrder,
    ArtifactRecipeData Recipes,
    string PackVersion,
    string BuildId,
    IReadOnlyList<string> TierNames,
    IReadOnlyList<CoilDto> Coils,
    IReadOnlyList<MachineDto> Machines,
    AtlasDto? Atlas,
    string DbPath);
