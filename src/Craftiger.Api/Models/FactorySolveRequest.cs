namespace Craftiger.Api.Models;

/// <summary>A factory solve request: the cost-solve settings plus the targets, layer priority, pins and scope toggles that shape the plan.</summary>
public sealed record FactorySolveRequest(
    GarageDto Garage,
    double B,
    Dictionary<string, double>? Weights,
    List<FactoryTargetDto>? Targets,
    List<string>? Priority = null,
    Dictionary<string, string>? Pins = null,
    bool MobFarms = false,
    bool BredSeeds = false);
