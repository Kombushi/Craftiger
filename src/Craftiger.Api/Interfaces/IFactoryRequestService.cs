using Craftiger.Api.Models;
using Craftiger.Solver.Models.Factory;

namespace Craftiger.Api.Interfaces;

/// <summary>Validates a factory request into the solver's model and derives its content-hash cache id.</summary>
public interface IFactoryRequestService
{
    FactoryRequest Translate(FactorySolveRequest request);

    string FactoryIdOf(FactorySolveRequest request);
}
