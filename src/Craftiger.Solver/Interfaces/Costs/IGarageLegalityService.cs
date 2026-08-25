using Craftiger.Solver.Models.Costs;
using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Interfaces.Costs;

/// <summary>Decides which recipes a garage can run.</summary>
public interface IGarageLegalityService
{
    /// <summary>The tier the garage runs this machine at, or null when it is not owned.</summary>
    int? EffectiveTier(string machine, Garage garage);

    bool IsLegal(SolverIndex index, int recipe, Garage garage);

    /// <summary>The heat the garage's coils and hatch bonus reach on a map — what excess-heat overclocks measure against.</summary>
    int HeatCapacity(string machine, Garage garage);
}
