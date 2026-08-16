using Craftiger.Builder.Models;

namespace Craftiger.Builder.Interfaces;

/// <summary>Decides which items the planner never expands, and what fixes their price.</summary>
public interface ILeafTaggingService
{
    Dictionary<string, string> Run(
        IEnumerable<string> canonicalIds, IReadOnlySet<string> produced, Dump dump, UnifiedItems unified);

    void Prune(
        Dictionary<string, string> classes, IReadOnlyDictionary<string, int> tiers, UnifiedItems unified);

    Dictionary<string, ItemParent> Parents(
        IReadOnlyDictionary<string, string> classes, IReadOnlyDictionary<string, int> tiers,
        UnifiedItems unified);

    Dictionary<string, double> Overrides(Dump dump);
}