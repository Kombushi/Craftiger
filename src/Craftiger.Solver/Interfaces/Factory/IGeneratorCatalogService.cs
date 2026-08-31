using Craftiger.Solver.Models.Factory;

namespace Craftiger.Solver.Interfaces.Factory;

/// <summary>Every garage-legal way to turn a fuel into EU, pruned to the competitive band.</summary>
public interface IGeneratorCatalogService
{
    IReadOnlyList<GeneratorLine> Eligible(FactoryContext context, IReadOnlyList<EnergyBand> bands, bool prune = true);
}
