using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces;

/// <summary>Collapses oredict-equivalent items into one canonical item per class.</summary>
public interface IUnificationService
{
    UnifiedItems Run(Dump dump);
}
