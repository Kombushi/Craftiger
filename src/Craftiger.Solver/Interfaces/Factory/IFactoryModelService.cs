using Craftiger.Solver.Models.Factory;

namespace Craftiger.Solver.Interfaces.Factory;

/// <summary>Assembles the flow LP: balance rows, run/split/buy/generate/supply columns and the layered objectives.</summary>
public interface IFactoryModelService
{
    FactoryModel Build(
        FactoryContext context,
        FactoryRequest request,
        FactoryTargets targets,
        CandidateSet candidates,
        IReadOnlyList<GeneratorLine> generators,
        IReadOnlySet<int> seedItems,
        ICollection<FactoryWarning> warnings);
}
