using Craftiger.Solver.Models;

namespace Craftiger.Solver.Interfaces;

/// <summary>Decides which recipes a garage can run.</summary>
public interface IGarageLegalityService
{
    /// <summary>The tier the garage runs this machine at, or null when it is not owned.</summary>
    int? EffectiveTier(string machine, Garage garage);

    bool IsLegal(SolverIndex index, int recipe, Garage garage);
}
