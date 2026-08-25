using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces;

/// <summary>Decides which items the planner never expands, and what fixes their price.</summary>
public interface ILeafTaggingService
{
    IReadOnlyDictionary<string, string> Run(
        IEnumerable<string> canonicalIds, IReadOnlySet<string> produced, Dump dump, UnifiedItems unified);

    /// <summary>The classes without the leaves whose weight cannot be worked out.</summary>
    IReadOnlyDictionary<string, string> Prune(
        IReadOnlyDictionary<string, string> classes, IReadOnlyDictionary<string, int> tiers, UnifiedItems unified,
        Dump dump);

    IReadOnlyDictionary<string, ItemParent> Parents(
        IReadOnlyDictionary<string, string> classes, IReadOnlyDictionary<string, int> tiers,
        UnifiedItems unified, Dump dump);

    IReadOnlyDictionary<string, double> Overrides(Dump dump);
}
