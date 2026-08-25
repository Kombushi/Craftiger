using Craftiger.Solver.Models.Costs;

namespace Craftiger.Solver.Interfaces.Costs;

/// <summary>Reroutes exact-cost ties toward the better route after the fixpoint; prices never change, only pointers.</summary>
public interface IRoutePreferenceService
{
    void Apply(CostTableBuilder table, IReadOnlyList<bool> priceable, IReadOnlyDictionary<string, double> leafWeights);
}
