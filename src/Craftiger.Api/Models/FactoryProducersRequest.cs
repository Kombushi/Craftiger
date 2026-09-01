namespace Craftiger.Api.Models;

/// <summary>Asks for every producer of one item across all recipe scopes — the pipeline picker's source, farm rows included.</summary>
public sealed record FactoryProducersRequest(
    GarageDto Garage,
    double B,
    Dictionary<string, double>? Weights,
    string ItemId);
