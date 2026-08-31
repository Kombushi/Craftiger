namespace Craftiger.Api.Models;

/// <summary>Asks for every generator line the garage could run; the same cost-solve settings price the fuels behind them.</summary>
public sealed record GeneratorCatalogRequest(GarageDto Garage, double B, Dictionary<string, double>? Weights);
