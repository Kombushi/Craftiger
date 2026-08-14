using Craftiger.Builder.Models;

namespace Craftiger.Builder.Interfaces;

/// <summary>Collapses oredict-equivalent items into one canonical item per class.</summary>
public interface IUnificationService
{
    UnifiedItems Run(Dump dump);
}
