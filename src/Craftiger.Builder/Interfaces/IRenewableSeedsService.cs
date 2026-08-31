using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces;

/// <summary>Marks the auto-infinite primitives; everything grown, farmed or dropped is derived through machine lines, never free.</summary>
public interface IRenewableSeedsService
{
    IReadOnlyList<PlannerRenewableSeed> Run(Dump dump, UnifiedItems unified, IReadOnlySet<string> itemIds);
}
