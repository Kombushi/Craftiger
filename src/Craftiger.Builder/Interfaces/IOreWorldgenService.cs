using Craftiger.Builder.Models;

namespace Craftiger.Builder.Interfaces;

/// <summary>Derives each ore item's cheapest generating-dimension era from the dump's worldgen tables.</summary>
public interface IOreWorldgenService
{
    OreWorldgenEras Run(Dump dump, UnifiedItems unified);
}
