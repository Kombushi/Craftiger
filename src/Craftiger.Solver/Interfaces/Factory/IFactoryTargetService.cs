using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Interfaces.Factory;

/// <summary>Resolves a request's targets to positions.</summary>
public interface IFactoryTargetService
{
    /// <summary>The normalized targets, or null when a target cannot enter the model at all.</summary>
    FactoryTargets? Normalize(SolverIndex index, FactoryRequest request, ICollection<FactoryWarning> warnings);
}
