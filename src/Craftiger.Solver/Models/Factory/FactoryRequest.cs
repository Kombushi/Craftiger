using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Models.Factory;

/// <summary>A factory solve request: an empty priority means resource, energy, machines; pins zero every other deterministic producer of the item; zero time limit means none. Steps or supplies turn the solve into a pipeline: the candidate set is exactly the steps, supplies buy free, and pins are ignored — the steps are the pins.</summary>
public sealed record FactoryRequest(
    IReadOnlyList<FactoryTarget> Targets,
    IReadOnlyList<FactoryObjective> Priority,
    IReadOnlyDictionary<string, string> Pins,
    bool MobFarms = false,
    bool BredSeeds = false,
    double TimeLimitSeconds = 0,
    IReadOnlyList<FactoryStep>? Steps = null,
    IReadOnlyList<string>? Supplies = null)
{
    public bool IsPipeline => Steps is { Count: > 0 } || Supplies is { Count: > 0 };

    private static readonly IReadOnlyList<FactoryObjective> DefaultPriority = [FactoryObjective.Resource, FactoryObjective.Energy, FactoryObjective.Machines];

    /// <summary>The layers in the order they run, duplicates dropped.</summary>
    public IReadOnlyList<FactoryObjective> Layers => Priority.Count > 0 ? Priority.Distinct().ToList() : DefaultPriority;

    /// <summary>Whether the request's toggles admit a recipe scope.</summary>
    public bool Admits(RecipeScope scope) => scope switch
    {
        RecipeScope.FactoryMob => MobFarms,
        RecipeScope.FactoryBred => BredSeeds,
        _ => true,
    };
}
