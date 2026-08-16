namespace Craftiger.Api.Models;

/// <summary>A garage row: whether the map carries a second multiblock switch, a coil
/// dropdown, and whether every garage owns it.</summary>
public sealed record MachineDto(
    string Name, bool HasMultiblockSwitch, bool HeatGated, bool AlwaysOwned);
