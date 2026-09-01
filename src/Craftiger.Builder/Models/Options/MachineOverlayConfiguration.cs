namespace Craftiger.Builder.Models.Options;

/// <summary>Curated machine constants the dump only states in tooltip prose; an item the dump knows but lists as no machine fails the build.</summary>
public sealed record MachineOverlayConfiguration
{
    /// <summary>Machine item id to effective parallel factor, like the XL turbo turbines' sixteen large turbines.</summary>
    public required IReadOnlyDictionary<string, int> Parallels { get; init; }

    /// <summary>Rotor-driven controllers by item id, each with its rotor fuel class (GAS, PLASMA or STEAM).</summary>
    public required IReadOnlyDictionary<string, string> RotorTurbines { get; init; }

    /// <summary>The GT++ steam multiblocks: eight parallels at 125 % speed and 62.5 % steam use, per their tooltips.</summary>
    public required IReadOnlyList<string> SteamMultiblocks { get; init; }
}
