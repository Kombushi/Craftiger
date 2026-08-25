namespace Craftiger.Solver.Models.Factory;

/// <summary>A factory solve request: an empty priority means resource, energy, machines; pins zero every other deterministic producer of the item; zero time limit means none.</summary>
public sealed record FactoryRequest(
    IReadOnlyList<FactoryTarget> Targets,
    IReadOnlyList<FactoryObjective> Priority,
    IReadOnlyDictionary<string, string> Pins,
    bool MobFarms = false,
    double TimeLimitSeconds = 0)
{
    private static readonly IReadOnlyList<FactoryObjective> _defaultPriority = [FactoryObjective.Resource, FactoryObjective.Energy, FactoryObjective.Machines];

    /// <summary>The layers in the order they run, duplicates dropped.</summary>
    public IReadOnlyList<FactoryObjective> Layers => Priority.Count > 0 ? Priority.Distinct().ToList() : _defaultPriority;
}
