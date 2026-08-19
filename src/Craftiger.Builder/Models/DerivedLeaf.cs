namespace Craftiger.Builder.Models;

/// <summary>A leaf priced as a fraction of another item's price. The divisor is the ratio of
/// GT's material amounts, so a nugget is a ninth of its ingot and a flawless gem twice one.</summary>
public sealed record DerivedLeaf(string Prefix, IReadOnlyList<string> ParentPrefixes)
{
    public static IReadOnlyDictionary<string, DerivedLeaf> ByClass { get; } =
        new Dictionary<string, DerivedLeaf>
        {
            ["dust_small"] = new("dustSmall", ["dust"]),
            ["dust_tiny"] = new("dustTiny", ["dust"]),
            ["nugget"] = new("nugget", ["ingot", "gem"]),
            ["gem_chipped"] = new("gemChipped", ["gem"]),
            ["gem_flawed"] = new("gemFlawed", ["gem"]),
            ["gem_flawless"] = new("gemFlawless", ["gem"]),
            ["gem_exquisite"] = new("gemExquisite", ["gem"])
        };

    /// <summary>The items this one could be a fraction of, best candidate first. A dust names
    /// only its own material, while a nugget will take either the ingot or the gem.</summary>
    public static IEnumerable<(string ParentId, double Divisor)> ParentsOf(
        string id, string leafClass, UnifiedItems unified, OrePrefixIndex prefixes)
    {
        if (!ByClass.TryGetValue(leafClass, out var derived))
        {
            yield break;
        }

        var oredict = unified.PrimaryOredictByCanonical.GetValueOrDefault(id);
        if (oredict is null
            || prefixes.Match(oredict) is not { } match
            || match.Prefix.Name != derived.Prefix
            || match.Prefix.MaterialAmount <= 0)
        {
            yield break;
        }

        foreach (var parentPrefix in derived.ParentPrefixes)
        {
            var parentAmount = prefixes.AmountOf(parentPrefix);
            if (parentAmount > 0
                && unified.CanonicalByOredict.TryGetValue(parentPrefix + match.Material, out var parentId))
            {
                yield return (parentId, (double)parentAmount / match.Prefix.MaterialAmount);
            }
        }
    }
}
