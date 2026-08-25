using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Eras;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces.Eras;

/// <summary>Derives each ore item's cheapest generating-dimension era from the dump's worldgen tables.</summary>
public interface IWorldgenErasService
{
    WorldgenEras Run(Dump dump, UnifiedItems unified);
}
