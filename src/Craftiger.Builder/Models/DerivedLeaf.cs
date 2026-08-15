namespace Craftiger.Builder.Models;

/// <summary>A leaf priced as a fraction of another item's price.</summary>
public sealed record DerivedLeaf(string Prefix, IReadOnlyList<string> ParentPrefixes, double Divisor)
{
    public static IReadOnlyDictionary<string, DerivedLeaf> ByClass { get; } =
        new Dictionary<string, DerivedLeaf>
        {
            ["dust_small"] = new("dustSmall", ["dust"], 4),
            ["dust_tiny"] = new("dustTiny", ["dust"], 9),
            ["nugget"] = new("nugget", ["ingot", "gem"], 9)
        };

    /// <summary>The items this one could be a fraction of, best candidate first. A dust names
    /// only its own material, while a nugget will take either the ingot or the gem.</summary>
    public static IEnumerable<string> ParentsOf(string id, string leafClass, UnifiedItems unified)
    {
        if (!ByClass.TryGetValue(leafClass, out var derived))
        {
            yield break;
        }

        var oredict = unified.PrimaryOredictByCanonical.GetValueOrDefault(id);
        if (oredict is null || !oredict.StartsWith(derived.Prefix, StringComparison.Ordinal))
        {
            yield break;
        }

        var material = oredict[derived.Prefix.Length..];
        foreach (var parentPrefix in derived.ParentPrefixes)
        {
            if (unified.CanonicalByOredict.TryGetValue(parentPrefix + material, out var parentId))
            {
                yield return parentId;
            }
        }
    }
}