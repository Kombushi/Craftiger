namespace Craftiger.Api.Models;

/// <summary>A garage row: whether the map carries a second multiblock switch, a coil
/// dropdown, whether every garage owns it, and the era its cheapest block becomes
/// craftable — null when it never does. The default garage owns a machine only from its
/// era on (§2).</summary>
public sealed record MachineDto(
    string Name, bool HasMultiblockSwitch, bool HeatGated, bool AlwaysOwned, int? Era);
