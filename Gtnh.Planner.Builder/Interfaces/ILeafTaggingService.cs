using Gtnh.Planner.Builder.Models;

namespace Gtnh.Planner.Builder.Interfaces;

/// <summary>Assigns leaf classes to canonical items by oredict and config lists.</summary>
public interface ILeafTaggingService
{
    Dictionary<string, string> Run(IEnumerable<string> canonicalIds, Dump dump, UnifiedItems unified);
}
