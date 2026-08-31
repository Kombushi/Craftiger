using Craftiger.Api.Models;

namespace Craftiger.Api.Interfaces;

/// <summary>Runs or reuses factory solves keyed by everything that shapes the plan — pins and the scope toggles included, unlike a cost solve.</summary>
public interface IFactoryCacheService
{
    Task<FactoryResponse> SolveAsync(FactorySolveRequest request);

    Task<GeneratorCatalogResponse> GeneratorsAsync(GeneratorCatalogRequest request);
}
