using Craftiger.Solver.Models.Factory;

namespace Craftiger.Solver.Interfaces.Factory;

/// <summary>The monotone fixpoint over garage-legal recipes: an item is auto-infinite when it is a seed or some legal recipe covers every slot with an auto-infinite alternative.</summary>
public interface IAutoInfiniteService
{
    AutoInfiniteItems Reach(FactoryContext context, IReadOnlySet<int> seedItems);
}
