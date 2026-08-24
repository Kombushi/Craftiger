namespace Craftiger.Builder.Models.Options;

/// <summary>Curated machine constants the dump only states in tooltip prose, audited against
/// the in-game text; an entry naming an item the dump does not list as a machine fails the
/// build so config rot surfaces instead of silently shipping nothing.</summary>
public sealed record MachineOverlayConfiguration
{
    /// <summary>Machine item id to effective parallel factor — the XL turbo turbines run
    /// sixteen large turbines' throughput as one controller.</summary>
    public required IReadOnlyDictionary<string, int> Parallels { get; init; }

    /// <summary>Controllers whose output comes from an installed rotor's stat table. The
    /// dump cannot tell them from other fuel-map multiblocks like the chemical engine.</summary>
    public required IReadOnlyList<string> RotorTurbines { get; init; }
}
