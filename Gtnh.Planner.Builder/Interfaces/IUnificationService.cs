using Gtnh.Planner.Builder.Models;

namespace Gtnh.Planner.Builder.Interfaces;

/// <summary>Collapses oredict-equivalent items into one canonical item per class.</summary>
public interface IUnificationService
{
    UnifiedItems Run(Dump dump);
}
