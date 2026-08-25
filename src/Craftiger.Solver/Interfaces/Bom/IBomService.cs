using Craftiger.Solver.Models.Bom;
using Craftiger.Solver.Models.Costs;
using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Interfaces.Bom;

/// <summary>Computes the bill of materials on the bestRecipe DAG with pins overlaid.</summary>
public interface IBomService
{
    BomResult Compute(
        SolverGraph graph,
        CostTable costs,
        Garage garage,
        IReadOnlyList<BomTarget> targets,
        IReadOnlyDictionary<string, string> pins);
}
