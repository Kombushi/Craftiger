using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Eras;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces.Eras;

/// <summary>Seeds the era table with world-origin items and the drops mining lowers.</summary>
public interface IEraSeedService
{
    EraTable Run(
        IReadOnlyDictionary<string, string> leafClasses, UnifiedItems unified, Dump dump, WorldgenEras worldgen);
}
