namespace Craftiger.Builder.Models.Options;

/// <summary>How each fuel recipe map's special values read. Every map the dump flags as fuel
/// must appear here; an unlisted one fails the build so a new map gets classified, never
/// silently mispriced.</summary>
public sealed record FuelsConfiguration
{
    /// <summary>Unlocalized map name to family: Standard (special is EU per mB, solids count
    /// as 1000 mB), Rtg (special is burn years at the row's voltage), Timed (special is total
    /// EU over the row's duration), Boiler (burn seconds per boiler generation in the info
    /// text), Excluded (ordinary recipes wearing the fuel flag), Empty (must stay empty).</summary>
    public required IReadOnlyDictionary<string, string> MapFamilies { get; init; }
}
