using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces;

/// <summary>Marks the auto-infinite primitives; derivation through recipes is the solver's job at run time.</summary>
public interface IRenewableSeedsService
{
    IReadOnlyList<PlannerRenewableSeed> Run(
        Dump dump, UnifiedItems unified, IReadOnlyDictionary<string, string> leafClasses,
        IReadOnlySet<string> itemIds);
}
