namespace Craftiger.Api.Models;

public sealed record SolveRequest(
    GarageDto Garage,
    double B,
    Dictionary<string, double>? Weights);
