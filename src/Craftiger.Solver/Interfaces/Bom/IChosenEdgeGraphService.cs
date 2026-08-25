using Craftiger.Solver.Models.Bom;
using Craftiger.Solver.Models.Costs;

namespace Craftiger.Solver.Interfaces.Bom;

/// <summary>The graph of chosen edges — each item to the inputs its pinned-or-best recipe picks — and its walks.</summary>
public interface IChosenEdgeGraphService
{
    /// <summary>Tarjan's walk from the roots: components with consumers before producers, or the pinned item of the first loop with no finite plan.</summary>
    (IReadOnlyList<BomComponent> Components, int? CyclePin) Walk(
        BomItems items, CostTable costs, BomPins pins, IReadOnlyList<int> roots, IReadOnlyDictionary<int, LoopSeed> seeds);

    /// <summary>An item's chosen inputs, plus the seed route's inputs where the item seeds its loop.</summary>
    IReadOnlyList<int> Children(BomItems items, CostTable costs, BomPins pins, IReadOnlyDictionary<int, LoopSeed> seeds, int item);

    /// <summary>Whether any of the items reaches a member over chosen edges.</summary>
    bool Reaches(
        BomItems items, CostTable costs, BomPins pins, IReadOnlyDictionary<int, LoopSeed> seeds,
        IEnumerable<int> from, IReadOnlySet<int> members);
}
