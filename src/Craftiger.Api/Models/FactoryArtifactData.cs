using Craftiger.Solver.Models.Factory;

namespace Craftiger.Api.Models;

/// <summary>Everything a factory solve reads from the artifact beyond the graph: rate data per recipe, machine blocks and fuels, the auto-infinite seeds, the steam carrier and the environment walls.</summary>
public sealed record FactoryArtifactData(
    FactoryRecipeData Recipes,
    FactoryMachineData Machines,
    FactorySeedData Seeds,
    FactorySteamRules Steam,
    FactoryEnvironment Environment);
