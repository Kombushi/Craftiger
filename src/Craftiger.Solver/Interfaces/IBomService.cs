using Craftiger.Solver.Models;

namespace Craftiger.Solver.Interfaces;

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
