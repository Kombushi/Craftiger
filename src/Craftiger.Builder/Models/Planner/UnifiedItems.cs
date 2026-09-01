using System.Collections.Frozen;

namespace Craftiger.Builder.Models.Planner;

/// <summary>Result of oredict unification: raw item id to canonical id, plus the names behind each.</summary>
public sealed record UnifiedItems
{
    private static readonly IReadOnlySet<string> NoNames = FrozenSet<string>.Empty;

    public required IReadOnlyDictionary<string, string> CanonicalByRawId { get; init; }

    public required IReadOnlyDictionary<string, string> PrimaryOredictByCanonical { get; init; }

    public required IReadOnlyDictionary<string, IReadOnlySet<string>> AliasesByCanonical { get; init; }

    /// <summary>Every oredict name of a canonical item, not just its primary.</summary>
    public required IReadOnlyDictionary<string, IReadOnlySet<string>> OredictsByCanonical { get; init; }

    /// <summary>The canonical item behind each oredict name, via its first member.</summary>
    public required IReadOnlyDictionary<string, string> CanonicalByOredict { get; init; }

    public string Canonical(string rawId) => CanonicalByRawId.GetValueOrDefault(rawId, rawId);

    public string? PrimaryOredictOf(string canonicalId) => PrimaryOredictByCanonical.GetValueOrDefault(canonicalId);

    public IReadOnlySet<string> OredictsOf(string canonicalId) => OredictsByCanonical.GetValueOrDefault(canonicalId) ?? NoNames;

    public IReadOnlySet<string> AliasesOf(string canonicalId) => AliasesByCanonical.GetValueOrDefault(canonicalId) ?? NoNames;
}
