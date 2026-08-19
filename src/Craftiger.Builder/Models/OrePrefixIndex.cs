namespace Craftiger.Builder.Models;

/// <summary>Longest-prefix lookup over GT's ore-prefix registry, so an oredict resolves to
/// its true prefix: dustImpureIron belongs to dustImpure, never to dust.</summary>
public sealed class OrePrefixIndex
{
    private readonly IReadOnlyDictionary<string, DumpOrePrefix> _byName;
    private readonly int[] _lengths;

    public OrePrefixIndex(IReadOnlyDictionary<string, DumpOrePrefix> prefixes)
    {
        _byName = prefixes;
        _lengths = prefixes.Keys.Select(name => name.Length).Distinct().OrderDescending().ToArray();
    }

    public (DumpOrePrefix Prefix, string Material)? Match(string oredict)
    {
        foreach (var length in _lengths)
        {
            if (length <= oredict.Length && _byName.TryGetValue(oredict[..length], out var prefix))
            {
                return (prefix, oredict[length..]);
            }
        }
        return null;
    }

    /// <summary>Whether the oredict names one shape of a single material: its whole substance
    /// is the material, so converting it back is matter-neutral. Containers hold their
    /// material inside something else and do not qualify.</summary>
    public bool IsMaterialShape(string oredict) =>
        Match(oredict) is { } match && IsShape(match.Prefix);

    public static bool IsShape(DumpOrePrefix prefix) =>
        prefix is { Unifiable: true, MaterialBased: true, Container: false };

    /// <summary>The prefix's material content, or 0 when GT leaves it undefined.</summary>
    public long AmountOf(string name) =>
        _byName.TryGetValue(name, out var prefix) && prefix.MaterialAmount > 0
            ? prefix.MaterialAmount
            : 0;
}
