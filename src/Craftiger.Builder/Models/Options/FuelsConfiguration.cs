namespace Craftiger.Builder.Models.Options;

/// <summary>How each fuel recipe map's special values read; an unlisted fuel map fails the build rather than mispricing silently.</summary>
public sealed record FuelsConfiguration
{
    /// <summary>Unlocalized map name to family: Standard, Rtg, Timed, Boiler, Excluded or Empty.</summary>
    public required IReadOnlyDictionary<string, string> MapFamilies { get; init; }
}
