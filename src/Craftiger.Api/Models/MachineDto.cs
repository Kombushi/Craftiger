namespace Craftiger.Api.Models;

/// <summary>A garage row: the map's multiblock switch and coil dropdown, whether every garage owns it, the era its cheapest block becomes craftable (null when never), and whether it only runs as a multiblock.</summary>
public sealed record MachineDto(string Name, bool HasMultiblockSwitch, bool HeatGated, bool AlwaysOwned, int? Era, bool MultiblockOnly);
