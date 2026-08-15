namespace Craftiger.Builder.Models;

/// <summary>Result of oredict unification: raw item id to canonical id, plus names.</summary>
public sealed class UnifiedItems
{
    public required Dictionary<string, string> CanonicalByRawId { get; init; }
    public required Dictionary<string, string> PrimaryOredictByCanonical { get; init; }
    public required Dictionary<string, HashSet<string>> AliasesByCanonical { get; init; }
    public required Dictionary<string, List<string>> MembersByOredict { get; init; }

    /// <summary>Every oredict name of a canonical item, not just its primary.</summary>
    public required Dictionary<string, HashSet<string>> OredictsByCanonical { get; init; }

    /// <summary>The canonical item behind each oredict name (via its first member).</summary>
    public required Dictionary<string, string> CanonicalByOredict { get; init; }

    public string Canonical(string rawId) => CanonicalByRawId.GetValueOrDefault(rawId, rawId);
}
