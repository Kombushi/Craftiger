using Craftiger.Solver.Models.Graph;

namespace Craftiger.Api.Models;

/// <summary>The loaded artifact: the solver graph, the display data beside it, the factory data, and the path of the read-only database search keeps querying.</summary>
public sealed record PlannerArtifact(
    SolverGraph Graph,
    IReadOnlyDictionary<string, ArtifactItem> Items,
    IReadOnlyList<string> CraftListOrder,
    ArtifactRecipeData Recipes,
    FactoryArtifactData Factory,
    string PackVersion,
    string BuildId,
    IReadOnlyList<string> TierNames,
    IReadOnlyList<long> TierVoltages,
    IReadOnlyList<CoilDto> Coils,
    IReadOnlyList<MachineDto> Machines,
    AtlasDto? Atlas,
    string DbPath);
