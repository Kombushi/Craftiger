using Craftiger.Solver.Models.Bom;
using Craftiger.Solver.Models.Costs;

namespace Craftiger.Solver.Interfaces.Bom;

/// <summary>Finds the one outside unit that starts a loop.</summary>
public interface ILoopSeedService
{
    /// <summary>The cheapest garage-legal producer of any loop member that does not itself draw on the loop, or null.</summary>
    LoopSeed? Seed(
        BomItems items, CostTable costs, Garage garage, BomPins pins,
        IReadOnlyDictionary<int, LoopSeed> seeds, IReadOnlyList<int> members);
}
