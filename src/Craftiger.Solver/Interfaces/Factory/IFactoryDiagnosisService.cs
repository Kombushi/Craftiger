using Craftiger.Solver.Models.Factory;

namespace Craftiger.Solver.Interfaces.Factory;

/// <summary>Never a bare infeasibility: names the pins or the demands the garage cannot deliver.</summary>
public interface IFactoryDiagnosisService
{
    void Diagnose(FactoryContext context, FactoryModel model, ICollection<FactoryWarning> warnings);
}
