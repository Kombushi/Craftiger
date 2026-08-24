namespace Craftiger.Solver.Models;

/// <summary>A factory solve request: every target constrains the one shared network. An empty
/// <paramref name="Priority"/> means the default order (resource, energy, machines); pins are
/// itemId → recipeId as in BOMs, but here they zero the flow on every other deterministic
/// producer of the item. <paramref name="TimeLimitSeconds"/> bounds the whole solve; zero
/// means no limit.</summary>
public sealed record FactoryRequest(
    IReadOnlyList<FactoryTarget> Targets,
    IReadOnlyList<FactoryObjective> Priority,
    IReadOnlyDictionary<string, string> Pins,
    double TimeLimitSeconds = 0);
