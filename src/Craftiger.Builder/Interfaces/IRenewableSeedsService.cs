using Craftiger.Builder.Models;

namespace Craftiger.Builder.Interfaces;

public interface IRenewableSeedsService
{
    IReadOnlyList<PlannerRenewableSeed> Run(
        Dump dump, UnifiedItems unified, IReadOnlyDictionary<string, string> leafClasses,
        IReadOnlySet<string> itemIds);
}
